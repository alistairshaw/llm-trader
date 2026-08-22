using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Data;

public sealed class OrderSubmissionRepository(TradingDbContext db) : IOrderSubmissionRepository
{
    public async Task<PrepareOrderSubmissionResult> PrepareAsync(OrderWorkEnvelope work, DateTimeOffset at,
        BrokerCapabilities gatewayCapabilities, CancellationToken token)
    {
        if (work.Kind != OrderWorkKind.Submit) return Reject(OrderSubmissionCodes.InvalidWork);
        SubmitOrderAuthorization authorization;
        try { authorization = CanonicalJsonSerializer.Deserialize<SubmitOrderAuthorization>(1, work.CanonicalPayload); }
        catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException)
        { return Reject(OrderSubmissionCodes.InvalidWork); }

        var outbox = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == work.Id.ToString(), token).ConfigureAwait(false);
        if (outbox is null || outbox.OrderId != work.OrderId.ToString() || outbox.PayloadHash != CanonicalJsonSerializer.Sha256(work.CanonicalPayload) ||
            outbox.IdempotencyKey != $"submit:{authorization.ClientOrderId}" || outbox.CorrelationId != authorization.CorrelationId)
            return Reject(OrderSubmissionCodes.AuthorizationMismatch);
        if (outbox.Status == "Completed") return new PrepareOrderSubmissionResult.AlreadyCompleted(outbox.LastError ?? BrokerExecutionCodes.Accepted);
        if (outbox.Status != "Claimed" || string.IsNullOrWhiteSpace(work.LeaseOwner) || outbox.LeaseOwner != work.LeaseOwner ||
            work.LeaseExpiresAt is null || outbox.LeaseExpiresAt != work.LeaseExpiresAt.Value.ToUnixTimeMilliseconds())
            return Reject(OrderSubmissionCodes.InvalidWork);

        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == authorization.OrderId, token).ConfigureAwait(false);
        if (order is null || order.Id != work.OrderId.ToString() || order.ClientOrderId != authorization.ClientOrderId ||
            order.BrokerAccountId != authorization.BrokerAccountId || order.InstrumentId != authorization.InstrumentId ||
            order.Side.ToString() != authorization.Side || order.Quantity != authorization.Quantity || order.QuantityUnit != authorization.QuantityUnit ||
            order.Currency != authorization.Currency || order.OrderType != authorization.OrderType || order.LimitPrice != authorization.LimitPrice ||
            order.TimeInForce.ToString() != authorization.TimeInForce || order.CorrelationId != authorization.CorrelationId)
            return Reject(OrderSubmissionCodes.AuthorizationMismatch);
        if (order.Status != OrderStatus.Created) return Reject(OrderSubmissionCodes.OrderState);

        var account = await db.BrokerAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.BrokerAccountId, token).ConfigureAwait(false);
        if (account?.Status != "Active") return Reject(OrderSubmissionCodes.AccountRestricted);
        if (account.LastReconciledAt is null) return Reject(OrderSubmissionCodes.AccountUnreconciled);
        var connection = await db.BrokerConnections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == authorization.BrokerConnectionId && x.Id == account.BrokerConnectionId, token).ConfigureAwait(false);
        if (connection?.Status != "Enabled") return Reject(OrderSubmissionCodes.ConnectionDisabled);
        if (connection.Environment != "Paper" || authorization.Environment != "Paper") return Reject(OrderSubmissionCodes.EnvironmentMismatch);
        var mapping = await db.InstrumentBrokerMappings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == authorization.InstrumentMappingId &&
            x.InstrumentId == order.InstrumentId && x.BrokerConnectionId == connection.Id && x.EffectiveFrom <= at.ToUnixTimeMilliseconds() &&
            (x.EffectiveTo == null || x.EffectiveTo > at.ToUnixTimeMilliseconds()), token).ConfigureAwait(false);
        if (mapping is null) return Reject(OrderSubmissionCodes.InstrumentMappingUnavailable);
        var required = order.OrderType == "Market" ? BrokerCapabilities.SubmitMarketOrders : BrokerCapabilities.SubmitLimitOrders;
        if (!gatewayCapabilities.HasFlag(required)) return Reject(OrderSubmissionCodes.CapabilityUnavailable);

        var request = new BrokerOrderRequest(new ClientOrderIdentity(order.ClientOrderId), InstrumentId.Parse(order.InstrumentId),
            mapping.ExternalInstrumentId, CanonicalEnumeration.Parse<OrderSide>(order.Side),
            new Quantity(CanonicalDecimal.Parse(order.Quantity), order.QuantityUnit), new Currency(order.Currency),
            CanonicalEnumeration.Parse<OrderType>(order.OrderType), order.LimitPrice is null ? null : new Price(CanonicalDecimal.Parse(order.LimitPrice), new Currency(order.Currency)), order.TimeInForce);
        return new PrepareOrderSubmissionResult.Ready(new PreparedOrderSubmission(work.Id, work.OrderId,
            BrokerAccountId.Parse(account.Id), BrokerConnectionId.Parse(connection.Id), connection.DisplayName,
            work.CorrelationId, request, outbox.PayloadHash, connection.BrokerType, work.LeaseOwner, order.Version));
    }

    public async Task<PersistenceWriteResult> CompleteAsync(CompleteOrderSubmissionCommand command, CancellationToken token)
    {
        var value = command.Submission;
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == value.OrderId.ToString() && x.Version == value.ExpectedOrderVersion, token).ConfigureAwait(false);
        var work = await db.OutboxMessages.SingleOrDefaultAsync(x => x.Id == value.WorkItemId.ToString() &&
            x.Status == "Claimed" && x.LeaseOwner == value.LeaseOwner && x.LeaseExpiresAt >= command.CompletedAt.ToUnixTimeMilliseconds(), token).ConfigureAwait(false);
        if (order is null || work is null) return new PersistenceWriteResult.ConcurrencyConflict(value.ExpectedOrderVersion, order?.Version);
        var domain = await new OrderRepository(db).GetAsync(value.OrderId, value.BrokerAccountId, PortfolioId.Parse(order.PortfolioId), token).ConfigureAwait(false);
        if (domain is null || command.TransitionIds.Count < 2) return new PersistenceWriteResult.ConcurrencyConflict(value.ExpectedOrderVersion, order.Version);
        db.BrokerSubmissionAttempts.Add(new BrokerSubmissionAttemptEntity
        {
            Id = command.TransitionIds[0].ToString(),
            OrderId = order.Id,
            WorkItemId = work.Id,
            AttemptNumber = work.AttemptCount,
            ClientOrderId = value.Request.ClientOrderId.Value,
            CommandHash = value.CommandHash,
            AdapterIdentity = value.AdapterIdentity,
            Environment = value.EnvironmentName,
            StartedAt = command.StartedAt.ToUnixTimeMilliseconds(),
            CompletedAt = command.CompletedAt.ToUnixTimeMilliseconds(),
            Outcome = command.Result.Outcome.ToString(),
            ResultCode = command.Result.Code,
            BrokerOrderId = command.Result.BrokerOrderId,
            DiagnosticCode = command.DiagnosticCode,
            CorrelationId = value.CorrelationId.Value
        });
        domain.BeginSubmission(command.TransitionIds[0], command.StartedAt);
        switch (command.Result.Outcome)
        {
            case BrokerSubmissionOutcome.Accepted:
            case BrokerSubmissionOutcome.Duplicate:
                domain.MarkSubmitted(command.TransitionIds[1], command.CompletedAt);
                if (command.Result.BrokerOrderId is not null && command.TransitionIds.Count > 2)
                    domain.Acknowledge(command.TransitionIds[2], command.Result.BrokerOrderId, command.CompletedAt, OrderTransitionSource.Platform);
                break;
            case BrokerSubmissionOutcome.Rejected:
            case BrokerSubmissionOutcome.TerminalFailure:
                domain.MarkSubmitted(command.TransitionIds[1], command.CompletedAt);
                domain.Reject(command.TransitionIds[2], command.Result.Code, command.CompletedAt, OrderTransitionSource.Platform);
                break;
            case BrokerSubmissionOutcome.Unknown:
                domain.MarkUnknown(command.TransitionIds[1], command.Result.Code, command.CompletedAt);
                break;
            default:
                return new PersistenceWriteResult.ConcurrencyConflict(value.ExpectedOrderVersion, order.Version);
        }
        var existing = await db.OrderTransitions.CountAsync(x => x.OrderId == order.Id, token).ConfigureAwait(false);
        var envelope = new OrderPersistenceEnvelope(domain,
            order.CapitalReservationId is null ? null : CapitalReservationId.Parse(order.CapitalReservationId), value.CorrelationId);
        foreach (var transition in domain.Transitions.Skip(existing))
        {
            order.Status = transition.NewStatus;
            order.Version = transition.Sequence;
            if (transition.NewStatus == OrderStatus.Submitted)
                order.SubmittedAt = transition.OccurredAt.ToUnixTimeMilliseconds();
            if (transition.NewStatus is OrderStatus.Rejected or OrderStatus.Cancelled or OrderStatus.Expired or OrderStatus.Filled)
                order.CompletedAt = transition.OccurredAt.ToUnixTimeMilliseconds();
            if (transition.NewStatus == OrderStatus.Acknowledged)
                order.BrokerOrderId = domain.BrokerOrderId;
            db.OrderTransitions.Add(OrderRepository.TransitionForSubmission(envelope, transition));
            await db.SaveChangesAsync(token).ConfigureAwait(false);
        }
        work.Status = command.Result.Outcome == BrokerSubmissionOutcome.RetryableFailure ? "Pending" : "Completed";
        work.LastError = command.DiagnosticCode; work.CompletedAt = work.Status == "Completed" ? command.CompletedAt.ToUnixTimeMilliseconds() : null;
        work.LeaseOwner = null; work.LeaseExpiresAt = null; work.Version++;
        await db.SaveChangesAsync(token).ConfigureAwait(false); await transaction.CommitAsync(token).ConfigureAwait(false);
        return new PersistenceWriteResult.Succeeded();
    }

    private static PrepareOrderSubmissionResult.Rejected Reject(string code) => new(code);
}
