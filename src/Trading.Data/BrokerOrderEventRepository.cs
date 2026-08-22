using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Data;

public sealed class BrokerOrderEventRepository(TradingDbContext db) : IBrokerOrderEventRepository
{
    public async Task<BrokerOrderEventWriteResult> ApplyAsync(
        ApplyBrokerOrderEventCommand command, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token).ConfigureAwait(false);
        var inbox = await db.InboxMessages.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == command.Message.Id.ToString(), token).ConfigureAwait(false);
        if (inbox is null || inbox.Status != "Claimed" || inbox.LeaseOwner != command.LeaseOwner)
            return new(BrokerOrderEventWriteDisposition.Contention, "broker_event.contention");

        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(
            x => x.ClientOrderId == command.ClientOrderId.Value, token).ConfigureAwait(false);
        if (order is null)
        {
            await transaction.RollbackAsync(token).ConfigureAwait(false);
            return new(BrokerOrderEventWriteDisposition.Deferred, "broker_event.unknown_order");
        }

        if (order.BrokerAccountId != command.BrokerAccountId.ToString())
            return await FinalizeAsync(command, BrokerOrderEventWriteDisposition.Rejected,
                "broker_event.identity_mismatch", order, transaction, token).ConfigureAwait(false);

        var environment = await (from account in db.BrokerAccounts.AsNoTracking()
                                 join connection in db.BrokerConnections.AsNoTracking()
                                     on account.BrokerConnectionId equals connection.Id
                                 where account.Id == order.BrokerAccountId
                                 select connection.Environment).SingleOrDefaultAsync(token).ConfigureAwait(false);
        if (!string.Equals(environment, command.Environment, StringComparison.Ordinal) || environment != "Paper")
            return await FinalizeAsync(command, BrokerOrderEventWriteDisposition.Rejected,
                "broker_event.environment_mismatch", order, transaction, token).ConfigureAwait(false);

        if (order.BrokerOrderId is not null && command.BrokerOrderId is not null &&
            !string.Equals(order.BrokerOrderId, command.BrokerOrderId, StringComparison.Ordinal))
            return await FinalizeAsync(command, BrokerOrderEventWriteDisposition.Rejected,
                "broker_event.identity_mismatch", order, transaction, token).ConfigureAwait(false);

        var lastAt = await db.OrderTransitions.AsNoTracking().Where(x => x.OrderId == order.Id)
            .MaxAsync(x => (long?)x.OccurredAt, token).ConfigureAwait(false) ?? order.CreatedAt;
        if (UtcUnixMilliseconds.ToProvider(command.OccurredAt) < lastAt)
            return await FinalizeAsync(command, BrokerOrderEventWriteDisposition.Reconcile,
                "broker_event.stale", order, transaction, token).ConfigureAwait(false);

        var target = Target(command.Kind);
        if (IsDuplicate(order.Status, target))
            return await FinalizeAsync(command, BrokerOrderEventWriteDisposition.Duplicate,
                "broker_event.duplicate", order, transaction, token).ConfigureAwait(false);

        if (!Allowed(order.Status, target))
        {
            var disposition = IsTerminal(order.Status)
                ? BrokerOrderEventWriteDisposition.Rejected
                : BrokerOrderEventWriteDisposition.Deferred;
            var code = order.Status is OrderStatus.PartiallyFilled or OrderStatus.CancelPending
                ? "broker_event.fill_conflict"
                : "broker_event.impossible_transition";
            if (disposition == BrokerOrderEventWriteDisposition.Deferred)
            {
                await transaction.RollbackAsync(token).ConfigureAwait(false);
                return new(disposition, code);
            }
            return await FinalizeAsync(command, disposition, code, order, transaction, token).ConfigureAwait(false);
        }

        var occurredAt = UtcUnixMilliseconds.ToProvider(command.OccurredAt);
        var completedAt = IsTerminal(target) ? occurredAt : (long?)null;
        var changed = await db.Orders.Where(x => x.Id == order.Id && x.Version == order.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, target)
                .SetProperty(x => x.BrokerOrderId, x => x.BrokerOrderId ?? command.BrokerOrderId)
                .SetProperty(x => x.CompletedAt, x => completedAt ?? x.CompletedAt)
                .SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false);
        if (changed != 1)
        {
            await transaction.RollbackAsync(token).ConfigureAwait(false);
            return new(BrokerOrderEventWriteDisposition.Contention, "broker_event.contention");
        }

        db.OrderTransitions.Add(new OrderTransitionEntity
        {
            Id = command.Message.Id.ToString(),
            OrderId = order.Id,
            SequenceNumber = checked((int)order.Version + 1),
            PreviousStatus = order.Status,
            NewStatus = target,
            ReasonCode = command.Code,
            Source = target == OrderStatus.CancelPending ? "Platform" : "Broker",
            OccurredAt = occurredAt,
            ReceivedAt = UtcUnixMilliseconds.ToProvider(command.Message.ReceivedAt),
            CorrelationId = command.Message.CorrelationId.Value
        });

        if (target is OrderStatus.Rejected or OrderStatus.Cancelled or OrderStatus.Expired &&
            order.CapitalReservationId is not null)
        {
            await db.CapitalReservations.Where(x => x.Id == order.CapitalReservationId && x.Status == "Active")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, "Released")
                    .SetProperty(x => x.ReleasedAt, UtcUnixMilliseconds.ToProvider(command.ProcessedAt))
                    .SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false);
        }

        var completed = await CompleteInboxAsync(command, "broker_event.applied", token).ConfigureAwait(false);
        if (completed != 1)
        {
            await transaction.RollbackAsync(token).ConfigureAwait(false);
            return new(BrokerOrderEventWriteDisposition.Contention, "broker_event.contention");
        }
        await db.SaveChangesAsync(token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return new(BrokerOrderEventWriteDisposition.Applied, "broker_event.applied");
    }

    private async Task<BrokerOrderEventWriteResult> FinalizeAsync(ApplyBrokerOrderEventCommand command,
        BrokerOrderEventWriteDisposition disposition, string code, OrderEntity? order,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken token)
    {
        if (disposition == BrokerOrderEventWriteDisposition.Reconcile && order is not null)
        {
            var payload = JsonSerializer.Serialize(new { schemaVersion = 1, orderId = order.Id, reason = code });
            db.OutboxMessages.Add(new OutboxMessageEntity
            {
                Id = command.Message.Id.ToString(),
                OrderId = order.Id,
                WorkKind = "Reconcile",
                IdempotencyKey = $"broker-event-reconcile:{command.Message.Id}",
                PayloadJson = payload,
                PayloadHash = CanonicalJsonSerializer.Sha256(payload),
                CorrelationId = command.Message.CorrelationId.Value,
                Status = "Pending",
                AvailableAt = UtcUnixMilliseconds.ToProvider(command.ProcessedAt),
                CreatedAt = UtcUnixMilliseconds.ToProvider(command.ProcessedAt),
                Version = 1
            });
        }
        if (await CompleteInboxAsync(command, code, token).ConfigureAwait(false) != 1)
        {
            await transaction.RollbackAsync(token).ConfigureAwait(false);
            return new(BrokerOrderEventWriteDisposition.Contention, "broker_event.contention");
        }
        await db.SaveChangesAsync(token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return new(disposition, code);
    }

    private Task<int> CompleteInboxAsync(ApplyBrokerOrderEventCommand command, string code, CancellationToken token) =>
        db.InboxMessages.Where(x => x.Id == command.Message.Id.ToString() && x.Status == "Claimed" &&
            x.LeaseOwner == command.LeaseOwner).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Completed")
                .SetProperty(x => x.LastError, code)
                .SetProperty(x => x.CompletedAt, UtcUnixMilliseconds.ToProvider(command.ProcessedAt))
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseExpiresAt, (long?)null)
                .SetProperty(x => x.Version, x => x.Version + 1), token);

    private static OrderStatus Target(BrokerOrderEventKind kind) => kind switch
    {
        BrokerOrderEventKind.Acknowledged => OrderStatus.Acknowledged,
        BrokerOrderEventKind.Rejected => OrderStatus.Rejected,
        BrokerOrderEventKind.CancelRequested => OrderStatus.CancelPending,
        BrokerOrderEventKind.Cancelled => OrderStatus.Cancelled,
        BrokerOrderEventKind.Expired => OrderStatus.Expired,
        _ => throw new InvalidOperationException("Execution events are handled by fill accounting.")
    };

    private static bool Allowed(OrderStatus from, OrderStatus to) => (from, to) switch
    {
        (OrderStatus.Submitted, OrderStatus.Acknowledged or OrderStatus.Rejected or OrderStatus.Expired) => true,
        (OrderStatus.Acknowledged or OrderStatus.PartiallyFilled, OrderStatus.CancelPending) => true,
        (OrderStatus.CancelPending, OrderStatus.Cancelled) => true,
        _ => false
    };

    private static bool IsDuplicate(OrderStatus current, OrderStatus target) => current == target ||
        target == OrderStatus.Acknowledged && current is OrderStatus.PartiallyFilled or OrderStatus.CancelPending or OrderStatus.Filled;
    private static bool IsTerminal(OrderStatus status) => status is OrderStatus.Filled or OrderStatus.Cancelled or
        OrderStatus.Rejected or OrderStatus.Expired;
}
