using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Data;

public sealed class PaperExecutionRecoveryRepository(TradingDbContext db) : IPaperExecutionRecoveryRepository
{
    public async Task<PaperExecutionRecoveryResult> RecoverAsync(
        PaperExecutionRecoveryRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RecoveredAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Recovery time must be UTC.", nameof(request));

        var recoveredAt = request.RecoveredAt.ToUnixTimeMilliseconds();
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);

        var expiredSubmissions = await db.OutboxMessages
            .Where(x => x.Status == "Claimed" && x.WorkKind == "Submit" && x.LeaseExpiresAt <= recoveredAt)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(token).ConfigureAwait(false);
        if (request.TransitionIds.Count < expiredSubmissions.Count * 2 ||
            request.ReconciliationWorkItemIds.Count < expiredSubmissions.Count)
            throw new InvalidOperationException("The recovery identity budget is smaller than the expired submission set.");

        var scopes = new List<PaperExecutionRecoveryScope>();
        for (var index = 0; index < expiredSubmissions.Count; index++)
        {
            var submission = expiredSubmissions[index];
            var orderRow = await db.Orders.SingleAsync(x => x.Id == submission.OrderId, token).ConfigureAwait(false);
            var scope = new PaperExecutionRecoveryScope(BrokerAccountId.Parse(orderRow.BrokerAccountId),
                PortfolioId.Parse(orderRow.PortfolioId), OrderId.Parse(orderRow.Id));
            scopes.Add(scope);

            // A claimed submission crossed the durable broker-I/O boundary. Its outcome cannot be inferred
            // after restart, so reconciliation by the original stable client identity is mandatory.
            if (orderRow.Status == OrderStatus.Created)
            {
                var order = await new OrderRepository(db).GetAsync(scope.OrderId, scope.BrokerAccountId,
                    scope.PortfolioId, token).ConfigureAwait(false) ??
                    throw new InvalidOperationException("The recovered Order no longer exists.");
                var at = request.RecoveredAt < order.CreatedAt ? order.CreatedAt : request.RecoveredAt;
                order.BeginSubmission(request.TransitionIds[index * 2], at);
                order.MarkUnknown(request.TransitionIds[(index * 2) + 1],
                    PaperExecutionRecoveryCodes.SubmissionOutcomeUnknown, at);
                foreach (var transition in order.Transitions.Where(x => x.Sequence > orderRow.Version))
                {
                    db.OrderTransitions.Add(OrderRepository.TransitionForSubmission(new(order,
                        orderRow.CapitalReservationId is null ? null : CapitalReservationId.Parse(orderRow.CapitalReservationId),
                        new CorrelationIdentity(orderRow.CorrelationId)), transition));
                    orderRow.Status = transition.NewStatus;
                    orderRow.Version = transition.Sequence;
                    await db.SaveChangesAsync(token).ConfigureAwait(false);
                }
            }

            submission.Status = "Completed";
            submission.LastError = PaperExecutionRecoveryCodes.SubmissionOutcomeUnknown;
            submission.CompletedAt = recoveredAt;
            submission.LeaseOwner = null;
            submission.LeaseExpiresAt = null;
            submission.Version++;

            var reconciliationKey = $"reconcile:{orderRow.ClientOrderId}";
            var reconciliation = await db.OutboxMessages.SingleOrDefaultAsync(
                x => x.IdempotencyKey == reconciliationKey, token).ConfigureAwait(false);
            if (reconciliation is null)
            {
                db.OutboxMessages.Add(new OutboxMessageEntity
                {
                    Id = request.ReconciliationWorkItemIds[index].ToString(),
                    OrderId = orderRow.Id,
                    WorkKind = "Reconcile",
                    IdempotencyKey = reconciliationKey,
                    PayloadJson = submission.PayloadJson,
                    PayloadHash = submission.PayloadHash,
                    CorrelationId = submission.CorrelationId,
                    Status = "Pending",
                    AvailableAt = recoveredAt,
                    CreatedAt = recoveredAt,
                    Version = 1
                });
            }
            else if (reconciliation.Status is not ("Completed" or "Failed"))
            {
                reconciliation.Status = "Pending";
                reconciliation.AvailableAt = recoveredAt;
                reconciliation.LeaseOwner = null;
                reconciliation.LeaseExpiresAt = null;
                reconciliation.LastError = PaperExecutionRecoveryCodes.ExpiredLease;
                reconciliation.Version++;
            }
        }

        await db.SaveChangesAsync(token).ConfigureAwait(false);

        var outboxReleased = await db.OutboxMessages
            .Where(x => x.Status == "Claimed" && x.LeaseExpiresAt <= recoveredAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Pending")
                .SetProperty(x => x.AvailableAt, recoveredAt)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseExpiresAt, (long?)null)
                .SetProperty(x => x.LastError, PaperExecutionRecoveryCodes.ExpiredLease)
                .SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false);
        var inboxReleased = await db.InboxMessages
            .Where(x => x.Status == "Claimed" && x.LeaseExpiresAt <= recoveredAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Pending")
                .SetProperty(x => x.AvailableAt, recoveredAt)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseExpiresAt, (long?)null)
                .SetProperty(x => x.LastError, PaperExecutionRecoveryCodes.ExpiredLease)
                .SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false);
        var failedOutbox = await db.OutboxMessages.CountAsync(x => x.Status == "Failed", token).ConfigureAwait(false);
        var failedInbox = await db.InboxMessages.CountAsync(x => x.Status == "Failed", token).ConfigureAwait(false);
        var incompleteScopes = await (from work in db.OutboxMessages.AsNoTracking()
                                      join order in db.Orders.AsNoTracking() on work.OrderId equals order.Id
                                      where work.Status == "Pending" || work.Status == "Claimed"
                                      select new { order.BrokerAccountId, order.PortfolioId, OrderId = order.Id })
            .ToListAsync(token).ConfigureAwait(false);
        scopes.AddRange(incompleteScopes.Select(x => new PaperExecutionRecoveryScope(
            BrokerAccountId.Parse(x.BrokerAccountId), PortfolioId.Parse(x.PortfolioId), OrderId.Parse(x.OrderId))));

        await transaction.CommitAsync(token).ConfigureAwait(false);
        return new(expiredSubmissions.Count, outboxReleased, inboxReleased, failedOutbox, failedInbox,
            scopes.Distinct().OrderBy(x => x.BrokerAccountId.ToString(), StringComparer.Ordinal)
                .ThenBy(x => x.PortfolioId.ToString(), StringComparer.Ordinal)
                .ThenBy(x => x.OrderId.ToString(), StringComparer.Ordinal).ToArray());
    }
}
