using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Data;

public sealed class OrderReconciliationRepository(TradingDbContext db) : IOrderReconciliationRepository
{
    public async Task<PrepareOrderReconciliationResult> PrepareAsync(OrderWorkEnvelope work,
        BrokerCapabilities gatewayCapabilities, CancellationToken token)
    {
        if (work.Kind != OrderWorkKind.Reconcile ||
            !gatewayCapabilities.HasFlag(BrokerCapabilities.LookupByClientOrderId)) return Reject();
        SubmitOrderAuthorization authorization;
        try { authorization = CanonicalJsonSerializer.Deserialize<SubmitOrderAuthorization>(1, work.CanonicalPayload); }
        catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException) { return Reject(); }
        var row = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == work.Id.ToString(), token).ConfigureAwait(false);
        if (row is null || row.OrderId != work.OrderId.ToString() || row.Status == "Failed") return Reject();
        if (row.Status == "Completed") return new PrepareOrderReconciliationResult.AlreadyCompleted(row.LastError ?? OrderReconciliationCodes.Found);
        if (row.Status != "Claimed" || row.LeaseOwner != work.LeaseOwner || work.LeaseExpiresAt is null ||
            row.LeaseExpiresAt != work.LeaseExpiresAt.Value.ToUnixTimeMilliseconds() ||
            row.PayloadHash != CanonicalJsonSerializer.Sha256(work.CanonicalPayload)) return Reject();
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == authorization.OrderId, token).ConfigureAwait(false);
        if (order is null || order.Id != row.OrderId || order.Status != OrderStatus.Unknown ||
            order.ClientOrderId != authorization.ClientOrderId || order.BrokerAccountId != authorization.BrokerAccountId) return Reject();
        var connection = await db.BrokerConnections.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == authorization.BrokerConnectionId && x.Environment == "Paper" && x.Status == "Enabled", token).ConfigureAwait(false);
        if (connection is null || authorization.Environment != "Paper") return Reject();
        var unknown = await db.OrderTransitions.AsNoTracking().Where(x => x.OrderId == order.Id && x.NewStatus == OrderStatus.Unknown)
            .OrderByDescending(x => x.SequenceNumber).Select(x => x.OccurredAt).FirstAsync(token).ConfigureAwait(false);
        return new PrepareOrderReconciliationResult.Ready(new(work.Id, work.OrderId,
            BrokerAccountId.Parse(order.BrokerAccountId), BrokerConnectionId.Parse(connection.Id), connection.DisplayName,
            new ClientOrderIdentity(order.ClientOrderId), work.CorrelationId, work.LeaseOwner!, order.Version,
            work.Attempt, DateTimeOffset.FromUnixTimeMilliseconds(unknown)));
    }

    public async Task<PersistenceWriteResult> CompleteAsync(CompleteOrderReconciliationCommand command,
        CancellationToken token)
    {
        var value = command.Reconciliation;
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var row = await db.OutboxMessages.SingleOrDefaultAsync(x => x.Id == value.WorkItemId.ToString() &&
            x.Status == "Claimed" && x.LeaseOwner == value.LeaseOwner && x.LeaseExpiresAt >= command.CompletedAt.ToUnixTimeMilliseconds(), token).ConfigureAwait(false);
        var orderRow = await db.Orders.SingleOrDefaultAsync(x => x.Id == value.OrderId.ToString() && x.Version == value.ExpectedOrderVersion, token).ConfigureAwait(false);
        if (row is null || orderRow is null) return new PersistenceWriteResult.ConcurrencyConflict(value.ExpectedOrderVersion, orderRow?.Version);
        var snapshot = CanonicalJsonSerializer.Serialize(1, new ReconciliationSnapshot(value.ClientOrderId.Value,
            command.Result.Outcome.ToString(), command.Result.Code, command.Result.BrokerOrderId,
            command.Result.Status?.ToString(), command.Result.ObservedAt));
        var resolution = CanonicalJsonSerializer.Serialize(1, new ReconciliationResolution(command.ResolutionCode));
        db.BrokerReconciliations.Add(new BrokerReconciliationEntity
        {
            Id = $"{value.WorkItemId}-{value.Attempt}",
            BrokerAccountId = value.BrokerAccountId.ToString(),
            Status = command.ResolutionCode is OrderReconciliationCodes.Found or OrderReconciliationCodes.AbsenceConfirmed ? "Matched" : "Failed",
            StartedAt = command.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt = command.CompletedAt.ToUnixTimeMilliseconds(),
            BrokerSnapshotJson = snapshot,
            DifferencesJson = "{}",
            ResolutionJson = resolution,
            CorrelationId = $"{value.CorrelationId.Value}:reconcile:{value.Attempt}",
            ContentHash = CanonicalJsonSerializer.Sha256(CanonicalJsonSerializer.Serialize(1,
                new ReconciliationAudit(snapshot, resolution)))
        });
        if (command.ResolutionCode == OrderReconciliationCodes.Found)
        {
            var status = command.Result.Status ?? OrderStatus.Submitted;
            var domain = await new OrderRepository(db).GetAsync(value.OrderId, value.BrokerAccountId,
                PortfolioId.Parse(orderRow.PortfolioId), token).ConfigureAwait(false);
            if (domain is null || command.Result.BrokerOrderId is null || status is OrderStatus.Unknown or OrderStatus.Created or OrderStatus.Submitting)
                return new PersistenceWriteResult.ConcurrencyConflict(value.ExpectedOrderVersion, orderRow.Version);
            domain.Reconcile(command.TransitionId, status, command.Result.Code, command.CompletedAt,
                status == OrderStatus.Acknowledged ? command.Result.BrokerOrderId : null);
            orderRow.Status = status; orderRow.Version = domain.Version; orderRow.BrokerOrderId = command.Result.BrokerOrderId;
            if (status is OrderStatus.Filled or OrderStatus.Rejected or OrderStatus.Cancelled or OrderStatus.Expired)
                orderRow.CompletedAt = command.CompletedAt.ToUnixTimeMilliseconds();
            db.OrderTransitions.Add(OrderRepository.TransitionForSubmission(new(domain,
                orderRow.CapitalReservationId is null ? null : CapitalReservationId.Parse(orderRow.CapitalReservationId), value.CorrelationId), domain.Transitions[^1]));
        }
        else if (command.ResolutionCode == OrderReconciliationCodes.AbsenceConfirmed)
        {
            var submit = await db.OutboxMessages.Where(x => x.OrderId == orderRow.Id && x.WorkKind == "Submit")
                .OrderBy(x => x.CreatedAt).FirstAsync(token).ConfigureAwait(false);
            submit.Status = "Pending"; submit.AvailableAt = command.CompletedAt.ToUnixTimeMilliseconds();
            submit.CompletedAt = null; submit.LastError = null; submit.LeaseOwner = null; submit.LeaseExpiresAt = null;
            submit.Version++;
        }
        row.Status = "Completed"; row.LastError = command.ResolutionCode; row.CompletedAt = command.CompletedAt.ToUnixTimeMilliseconds();
        row.LeaseOwner = null; row.LeaseExpiresAt = null; row.Version++;
        await db.SaveChangesAsync(token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return new PersistenceWriteResult.Succeeded();
    }

    private static PrepareOrderReconciliationResult.Rejected Reject() => new(OrderReconciliationCodes.InvalidWork);
    private sealed record ReconciliationSnapshot(string ClientOrderId, string Outcome, string Code,
        string? BrokerOrderId, string? Status, DateTimeOffset ObservedAt);
    private sealed record ReconciliationResolution(string Code);
    private sealed record ReconciliationAudit(string Snapshot, string Resolution);
}
