using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Data;

public sealed class OrderRepository(TradingDbContext db) : IOrderRepository
{
    public async Task<PersistenceWriteResult> AddAsync(OrderPersistenceEnvelope value, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        db.Orders.Add(ToEntity(value)); db.OrderTransitions.AddRange(value.Order.Transitions.Select(x => ToEntity(value, x)));
        db.Fills.AddRange(value.Order.Fills.Select(x => ToEntity(value.Order, x)));
        var result = await RepositoryWrites.SaveAsync(db, "order_identity", token).ConfigureAwait(false);
        if (result is PersistenceWriteResult.Succeeded) await tx.CommitAsync(token).ConfigureAwait(false); return result;
    }
    public async Task<PersistenceWriteResult> SaveAsync(OrderPersistenceEnvelope value, long expectedVersion, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var entity = await db.Orders.SingleOrDefaultAsync(x => x.Id == value.Order.Id.ToString(), token).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion) return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        var transitions = await db.OrderTransitions.CountAsync(x => x.OrderId == entity.Id, token).ConfigureAwait(false);
        var fills = await db.Fills.Where(x => x.OrderId == entity.Id).Select(x => x.Id).ToListAsync(token).ConfigureAwait(false);
        Copy(value, entity); db.Entry(entity).Property(x => x.Version).OriginalValue = expectedVersion;
        db.OrderTransitions.AddRange(value.Order.Transitions.Skip(transitions).Select(x => ToEntity(value, x)));
        db.Fills.AddRange(value.Order.Fills.Where(x => !fills.Contains(x.Id.ToString())).Select(x => ToEntity(value.Order, x)));
        var result = await RepositoryWrites.SaveAsync(db, "order_execution_identity", token).ConfigureAwait(false);
        if (result is PersistenceWriteResult.Succeeded) await tx.CommitAsync(token).ConfigureAwait(false); return result;
    }
    public Task<Order?> GetAsync(OrderId id, BrokerAccountId account, PortfolioId portfolio, CancellationToken token) =>
        Load(x => x.Id == id.ToString() && x.BrokerAccountId == account.ToString() && x.PortfolioId == portfolio.ToString(), token);
    public Task<Order?> FindByProposalAsync(TradeProposalId proposal, BrokerAccountId account, PortfolioId portfolio, CancellationToken token) =>
        Load(x => x.TradeProposalId == proposal.ToString() && x.BrokerAccountId == account.ToString() && x.PortfolioId == portfolio.ToString(), token);
    public Task<Order?> FindByClientOrderIdAsync(ClientOrderIdentity clientOrderId, BrokerAccountId account, CancellationToken token) =>
        Load(x => x.ClientOrderId == clientOrderId.Value && x.BrokerAccountId == account.ToString(), token);
    public Task<Order?> FindByBrokerOrderIdAsync(string brokerOrderId, BrokerAccountId account, CancellationToken token) =>
        Load(x => x.BrokerOrderId == brokerOrderId && x.BrokerAccountId == account.ToString(), token);
    public async Task<Fill?> FindFillAsync(string executionId, BrokerAccountId account, OrderId order, CancellationToken token)
    {
        var owner = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.ToString() && x.BrokerAccountId == account.ToString(), token).ConfigureAwait(false);
        if (owner is null) return null;
        var fill = await db.Fills.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == owner.Id && x.BrokerExecutionId == executionId, token).ConfigureAwait(false);
        return fill is null ? null : ToDomain(fill, owner.QuantityUnit);
    }
    private async Task<Order?> Load(System.Linq.Expressions.Expression<Func<OrderEntity, bool>> predicate, CancellationToken token)
    {
        var x = await db.Orders.AsNoTracking().SingleOrDefaultAsync(predicate, token).ConfigureAwait(false); if (x is null) return null;
        var transitions = await db.OrderTransitions.AsNoTracking().Where(y => y.OrderId == x.Id).OrderBy(y => y.SequenceNumber).ToListAsync(token).ConfigureAwait(false);
        var fills = await db.Fills.AsNoTracking().Where(y => y.OrderId == x.Id).OrderBy(y => y.ExecutedAt).ThenBy(y => y.Id).ToListAsync(token).ConfigureAwait(false);
        return Order.Rehydrate(OrderId.Parse(x.Id), x.ClientOrderId, PortfolioId.Parse(x.PortfolioId), BrokerAccountId.Parse(x.BrokerAccountId),
            TradeProposalId.Parse(x.TradeProposalId), InstrumentId.Parse(x.InstrumentId), CanonicalEnumeration.Parse<OrderSide>(x.Side),
            new Quantity(CanonicalDecimal.Parse(x.Quantity), x.QuantityUnit), new Currency(x.Currency), CanonicalEnumeration.Parse<OrderType>(x.OrderType),
            x.LimitPrice is null ? null : new Price(CanonicalDecimal.Parse(x.LimitPrice), new Currency(x.Currency)), x.TimeInForce,
            UtcUnixMilliseconds.FromProvider(x.CreatedAt), x.BrokerOrderId, x.SubmittedAt is null ? null : UtcUnixMilliseconds.FromProvider(x.SubmittedAt.Value),
            x.CompletedAt is null ? null : UtcUnixMilliseconds.FromProvider(x.CompletedAt.Value), x.Version,
            transitions.Select(y => new OrderTransition(OrderTransitionId.Parse(y.Id), y.SequenceNumber, y.PreviousStatus, y.NewStatus,
                y.ReasonDetail ?? y.ReasonCode, CanonicalEnumeration.Parse<OrderTransitionSource>(y.Source), UtcUnixMilliseconds.FromProvider(y.OccurredAt))).ToArray(),
            fills.Select(y => ToDomain(y, x.QuantityUnit)).ToArray());
    }
    private static OrderEntity ToEntity(OrderPersistenceEnvelope x) { var entity = new OrderEntity(); Copy(x, entity); return entity; }
    private static void Copy(OrderPersistenceEnvelope value, OrderEntity e)
    {
        var x = value.Order; e.Id = x.Id.ToString(); e.ClientOrderId = x.ClientOrderId; e.PortfolioId = x.PortfolioId.ToString(); e.BrokerAccountId = x.BrokerAccountId.ToString();
        e.TradeProposalId = x.TradeProposalId.ToString(); e.CapitalReservationId = value.ReservationId?.ToString(); e.InstrumentId = x.InstrumentId.ToString(); e.Side = CanonicalEnumeration.Format(x.Side);
        e.Quantity = CanonicalDecimal.Format(x.Quantity.Amount); e.QuantityUnit = x.Quantity.Unit; e.Currency = x.Currency.Code; e.OrderType = CanonicalEnumeration.Format(x.OrderType);
        e.LimitPrice = x.LimitPrice is null ? null : CanonicalDecimal.Format(x.LimitPrice.Amount); e.TimeInForce = x.TimeInForce; e.Status = x.Status; e.BrokerOrderId = x.BrokerOrderId;
        e.CorrelationId = value.CorrelationId.Value; e.CreatedAt = UtcUnixMilliseconds.ToProvider(x.CreatedAt); e.SubmittedAt = x.SubmittedAt is null ? null : UtcUnixMilliseconds.ToProvider(x.SubmittedAt.Value);
        e.CompletedAt = x.CompletedAt is null ? null : UtcUnixMilliseconds.ToProvider(x.CompletedAt.Value); e.Version = x.Version;
    }
    private static OrderTransitionEntity ToEntity(OrderPersistenceEnvelope v, OrderTransition x) => new()
    {
        Id = x.Id.ToString(),
        OrderId = v.Order.Id.ToString(),
        SequenceNumber = x.Sequence,
        PreviousStatus = x.PreviousStatus,
        NewStatus = x.NewStatus,
        ReasonCode = x.Reason,
        Source = CanonicalEnumeration.Format(x.Source),
        OccurredAt = UtcUnixMilliseconds.ToProvider(x.OccurredAt),
        ReceivedAt = UtcUnixMilliseconds.ToProvider(x.OccurredAt),
        CorrelationId = v.CorrelationId.Value
    };
    private static FillEntity ToEntity(Order order, Fill x) => new()
    {
        Id = x.Id.ToString(),
        OrderId = order.Id.ToString(),
        BrokerAccountId = order.BrokerAccountId.ToString(),
        BrokerExecutionId = x.BrokerExecutionId,
        Quantity = CanonicalDecimal.Format(x.Quantity.Amount),
        Price = CanonicalDecimal.Format(x.Price.Amount),
        Currency = x.Price.Currency.Code,
        FeeAmount = CanonicalDecimal.Format(x.Fee.Amount),
        FeeCurrency = x.Fee.Currency.Code,
        ExecutedAt = UtcUnixMilliseconds.ToProvider(x.ExecutedAt),
        ReceivedAt = UtcUnixMilliseconds.ToProvider(x.ReceivedAt)
    };
    private static Fill ToDomain(FillEntity x, string unit) => new(FillId.Parse(x.Id), x.BrokerExecutionId, new Quantity(CanonicalDecimal.Parse(x.Quantity), unit),
        new Price(CanonicalDecimal.Parse(x.Price), new Currency(x.Currency)), new Money(CanonicalDecimal.Parse(x.FeeAmount), new Currency(x.FeeCurrency)),
        UtcUnixMilliseconds.FromProvider(x.ExecutedAt), UtcUnixMilliseconds.FromProvider(x.ReceivedAt));
}

public sealed class BrokerReconciliationRepository(TradingDbContext db) : IBrokerReconciliationRepository
{
    public Task<PersistenceWriteResult> AppendAsync(BrokerReconciliationRecord value, CancellationToken token) => RepositoryWrites.AddAsync(db,
        new BrokerReconciliationEntity { Id = value.Id, BrokerAccountId = value.AccountId.ToString(), Status = value.Status, StartedAt = UtcUnixMilliseconds.ToProvider(value.StartedAt), CompletedAt = value.CompletedAt is null ? null : UtcUnixMilliseconds.ToProvider(value.CompletedAt.Value), BrokerSnapshotJson = value.SnapshotJson, DifferencesJson = value.DifferencesJson, ResolutionJson = value.ResolutionJson, CorrelationId = value.CorrelationId.Value, ContentHash = value.ContentHash }, "broker_reconciliation_identity", token);
    public async Task<IReadOnlyList<BrokerReconciliationRecord>> ListAsync(BrokerAccountId account, CancellationToken token) =>
        (await db.BrokerReconciliations.AsNoTracking().Where(x => x.BrokerAccountId == account.ToString()).OrderBy(x => x.StartedAt).ThenBy(x => x.Id).ToListAsync(token).ConfigureAwait(false))
        .Select(x => new BrokerReconciliationRecord(x.Id, account, x.Status, UtcUnixMilliseconds.FromProvider(x.StartedAt), x.CompletedAt is null ? null : UtcUnixMilliseconds.FromProvider(x.CompletedAt.Value), x.BrokerSnapshotJson, x.DifferencesJson, x.ResolutionJson, new CorrelationIdentity(x.CorrelationId), x.ContentHash)).ToArray();
}

public sealed class OrderWorkRepository(TradingDbContext db) : IOrderWorkRepository
{
    public Task<PersistenceWriteResult> EnqueueAsync(OrderWorkEnvelope value, CancellationToken token) => RepositoryWrites.AddAsync(db, new OutboxMessageEntity
    { Id = value.Id.ToString(), OrderId = value.OrderId.ToString(), WorkKind = CanonicalEnumeration.Format(value.Kind), IdempotencyKey = value.IdempotencyKey, PayloadJson = value.CanonicalPayload, PayloadHash = CanonicalJsonSerializer.Sha256(value.CanonicalPayload), CorrelationId = value.CorrelationId.Value, Status = "Pending", AvailableAt = UtcUnixMilliseconds.ToProvider(value.AvailableAt), CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt), AttemptCount = value.Attempt, Version = 1 }, "outbox_idempotency_key", token);
    public async Task<IReadOnlyList<OrderWorkEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1); var at = UtcUnixMilliseconds.ToProvider(now); var expiry = UtcUnixMilliseconds.ToProvider(lease.ExpiresAt);
        await using var tx = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var ids = await db.OutboxMessages.Where(x => (x.Status == "Pending" && x.AvailableAt <= at) || (x.Status == "Claimed" && x.LeaseExpiresAt <= at)).OrderBy(x => x.AvailableAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(limit).Select(x => x.Id).ToListAsync(token).ConfigureAwait(false);
        await db.OutboxMessages.Where(x => ids.Contains(x.Id) && ((x.Status == "Pending" && x.AvailableAt <= at) || (x.Status == "Claimed" && x.LeaseExpiresAt <= at))).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Claimed").SetProperty(x => x.LeaseOwner, lease.Owner).SetProperty(x => x.LeaseExpiresAt, expiry).SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false);
        var rows = await db.OutboxMessages.AsNoTracking().Where(x => ids.Contains(x.Id) && x.LeaseOwner == lease.Owner && x.LeaseExpiresAt == expiry).OrderBy(x => x.AvailableAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(token).ConfigureAwait(false); await tx.CommitAsync(token).ConfigureAwait(false); return rows.Select(ToDomain).ToArray();
    }
    public Task<PersistenceWriteResult> CompleteAsync(OrderWorkItemId id, string owner, string result, DateTimeOffset at, CancellationToken token) => Update(id, owner, result, at, true, token);
    public Task<PersistenceWriteResult> RetryAsync(OrderWorkItemId id, string owner, string errorCode, DateTimeOffset availableAt, CancellationToken token) => Update(id, owner, errorCode, availableAt, false, token);
    public Task<PersistenceWriteResult> RenewAsync(OrderWorkItemId id, string owner, DateTimeOffset expiresAt, CancellationToken token) => Renew(id, owner, expiresAt, token);
    public Task<PersistenceWriteResult> FailAsync(OrderWorkItemId id, string owner, string errorCode, DateTimeOffset failedAt, CancellationToken token) => UpdateTerminal(id, owner, errorCode, failedAt, token);
    private async Task<PersistenceWriteResult> Update(OrderWorkItemId id, string owner, string detail, DateTimeOffset at, bool complete, CancellationToken token)
    { var timestamp = UtcUnixMilliseconds.ToProvider(at); var changed = await db.OutboxMessages.Where(x => x.Id == id.ToString() && x.Status == "Claimed" && x.LeaseOwner == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, complete ? "Completed" : "Pending").SetProperty(x => x.LastError, detail).SetProperty(x => x.CompletedAt, complete ? timestamp : null).SetProperty(x => x.AvailableAt, x => complete ? x.AvailableAt : timestamp).SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseExpiresAt, (long?)null).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false); return changed == 1 ? new PersistenceWriteResult.Succeeded() : new PersistenceWriteResult.ConcurrencyConflict(0, null); }
    private async Task<PersistenceWriteResult> Renew(OrderWorkItemId id, string owner, DateTimeOffset expiresAt, CancellationToken token)
    { var expiry = UtcUnixMilliseconds.ToProvider(expiresAt); var changed = await db.OutboxMessages.Where(x => x.Id == id.ToString() && x.Status == "Claimed" && x.LeaseOwner == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseExpiresAt, expiry).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false); return changed == 1 ? new PersistenceWriteResult.Succeeded() : new PersistenceWriteResult.ConcurrencyConflict(0, null); }
    private async Task<PersistenceWriteResult> UpdateTerminal(OrderWorkItemId id, string owner, string detail, DateTimeOffset at, CancellationToken token)
    { var timestamp = UtcUnixMilliseconds.ToProvider(at); var changed = await db.OutboxMessages.Where(x => x.Id == id.ToString() && x.Status == "Claimed" && x.LeaseOwner == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Failed").SetProperty(x => x.LastError, detail).SetProperty(x => x.CompletedAt, timestamp).SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseExpiresAt, (long?)null).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false); return changed == 1 ? new PersistenceWriteResult.Succeeded() : new PersistenceWriteResult.ConcurrencyConflict(0, null); }
    private static OrderWorkEnvelope ToDomain(OutboxMessageEntity x) => new(OrderWorkItemId.Parse(x.Id), OrderId.Parse(x.OrderId), CanonicalEnumeration.Parse<OrderWorkKind>(x.WorkKind), x.IdempotencyKey, x.PayloadJson, new CorrelationIdentity(x.CorrelationId), x.AttemptCount, UtcUnixMilliseconds.FromProvider(x.AvailableAt), UtcUnixMilliseconds.FromProvider(x.CreatedAt));
}

public sealed class BrokerInboxRepository(TradingDbContext db) : IBrokerInboxRepository
{
    public Task<PersistenceWriteResult> ReceiveAsync(BrokerInboxEnvelope value, CancellationToken token) => RepositoryWrites.AddAsync(db, new InboxMessageEntity { Id = value.Id.ToString(), IdempotencyKey = value.IdempotencyKey, CorrelationId = value.CorrelationId.Value, ReceivedAt = UtcUnixMilliseconds.ToProvider(value.ReceivedAt), AvailableAt = UtcUnixMilliseconds.ToProvider(value.ReceivedAt), Status = "Pending", PayloadJson = value.CanonicalPayload, PayloadHash = CanonicalJsonSerializer.Sha256(value.CanonicalPayload), Version = 1 }, "inbox_idempotency_key", token);
    public async Task<IReadOnlyList<BrokerInboxEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease, CancellationToken token)
    { ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1); var at = UtcUnixMilliseconds.ToProvider(now); var expiry = UtcUnixMilliseconds.ToProvider(lease.ExpiresAt); await using var tx = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false); var ids = await db.InboxMessages.Where(x => (x.Status == "Pending" && x.AvailableAt <= at) || (x.Status == "Claimed" && x.LeaseExpiresAt <= at)).OrderBy(x => x.AvailableAt).ThenBy(x => x.ReceivedAt).ThenBy(x => x.Id).Take(limit).Select(x => x.Id).ToListAsync(token).ConfigureAwait(false); await db.InboxMessages.Where(x => ids.Contains(x.Id) && ((x.Status == "Pending" && x.AvailableAt <= at) || (x.Status == "Claimed" && x.LeaseExpiresAt <= at))).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Claimed").SetProperty(x => x.LeaseOwner, lease.Owner).SetProperty(x => x.LeaseExpiresAt, expiry).SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false); var rows = await db.InboxMessages.AsNoTracking().Where(x => ids.Contains(x.Id) && x.LeaseOwner == lease.Owner && x.LeaseExpiresAt == expiry).OrderBy(x => x.AvailableAt).ThenBy(x => x.ReceivedAt).ThenBy(x => x.Id).ToListAsync(token).ConfigureAwait(false); await tx.CommitAsync(token).ConfigureAwait(false); return rows.Select(x => new BrokerInboxEnvelope(BrokerMessageId.Parse(x.Id), x.IdempotencyKey, x.PayloadJson, new CorrelationIdentity(x.CorrelationId), UtcUnixMilliseconds.FromProvider(x.ReceivedAt), x.AttemptCount)).ToArray(); }
    public Task<PersistenceWriteResult> CompleteAsync(BrokerMessageId id, string owner, string result, DateTimeOffset at, CancellationToken token) => Update(id, owner, result, at, true, token);
    public Task<PersistenceWriteResult> RetryAsync(BrokerMessageId id, string owner, string errorCode, DateTimeOffset availableAt, CancellationToken token) => Update(id, owner, errorCode, availableAt, false, token);
    public Task<PersistenceWriteResult> RenewAsync(BrokerMessageId id, string owner, DateTimeOffset expiresAt, CancellationToken token) => Renew(id, owner, expiresAt, token);
    public Task<PersistenceWriteResult> FailAsync(BrokerMessageId id, string owner, string errorCode, DateTimeOffset failedAt, CancellationToken token) => UpdateTerminal(id, owner, errorCode, failedAt, token);
    private async Task<PersistenceWriteResult> Update(BrokerMessageId id, string owner, string detail, DateTimeOffset at, bool complete, CancellationToken token) { var timestamp = UtcUnixMilliseconds.ToProvider(at); var changed = await db.InboxMessages.Where(x => x.Id == id.ToString() && x.Status == "Claimed" && x.LeaseOwner == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, complete ? "Completed" : "Pending").SetProperty(x => x.LastError, detail).SetProperty(x => x.CompletedAt, complete ? timestamp : null).SetProperty(x => x.AvailableAt, x => complete ? x.AvailableAt : timestamp).SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseExpiresAt, (long?)null).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false); return changed == 1 ? new PersistenceWriteResult.Succeeded() : new PersistenceWriteResult.ConcurrencyConflict(0, null); }
    private async Task<PersistenceWriteResult> Renew(BrokerMessageId id, string owner, DateTimeOffset expiresAt, CancellationToken token) { var expiry = UtcUnixMilliseconds.ToProvider(expiresAt); var changed = await db.InboxMessages.Where(x => x.Id == id.ToString() && x.Status == "Claimed" && x.LeaseOwner == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseExpiresAt, expiry).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false); return changed == 1 ? new PersistenceWriteResult.Succeeded() : new PersistenceWriteResult.ConcurrencyConflict(0, null); }
    private async Task<PersistenceWriteResult> UpdateTerminal(BrokerMessageId id, string owner, string detail, DateTimeOffset at, CancellationToken token) { var timestamp = UtcUnixMilliseconds.ToProvider(at); var changed = await db.InboxMessages.Where(x => x.Id == id.ToString() && x.Status == "Claimed" && x.LeaseOwner == owner).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Failed").SetProperty(x => x.LastError, detail).SetProperty(x => x.CompletedAt, timestamp).SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseExpiresAt, (long?)null).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false); return changed == 1 ? new PersistenceWriteResult.Succeeded() : new PersistenceWriteResult.ConcurrencyConflict(0, null); }
}
