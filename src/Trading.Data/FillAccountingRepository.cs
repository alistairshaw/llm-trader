using Microsoft.EntityFrameworkCore;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Data;

public static class FillAccountingCodes
{
    public const string Applied = "order_execution.fill_applied";
    public const string Duplicate = "order_execution.duplicate_fill";
    public const string Overfill = "order_execution.overfill";
    public const string IdentityMismatch = "order_execution.identity_mismatch";
    public const string CurrencyMismatch = "order_execution.currency_mismatch";
    public const string UnitMismatch = "order_execution.unit_mismatch";
    public const string InvalidState = "order_execution.invalid_state";
    public const string InsufficientPosition = "order_execution.insufficient_position";
    public const string Contention = "order_execution.contention";
}

public sealed class FillAccountingRepository(TradingDbContext db) : IFillAccountingRepository
{
    public async Task<FillAccountingWriteResult> ApplyAsync(ApplyFillAccountingCommand command, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token).ConfigureAwait(false);
        var inbox = await db.InboxMessages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.Message.Id.ToString(), token).ConfigureAwait(false);
        if (inbox is null || inbox.Status != "Claimed" || inbox.LeaseOwner != command.LeaseOwner)
            return new(FillAccountingWriteDisposition.Contention, FillAccountingCodes.Contention);

        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.ClientOrderId == command.ClientOrderId.Value, token).ConfigureAwait(false);
        if (order is null) return new(FillAccountingWriteDisposition.Deferred, "order_execution.unknown_order");
        if (order.BrokerAccountId != command.BrokerAccountId.ToString() || order.BrokerOrderId != command.BrokerOrderId || command.Environment != "Paper")
            return await RejectAsync(command, FillAccountingCodes.IdentityMismatch, transaction, token).ConfigureAwait(false);

        var execution = command.Execution;
        var duplicate = await db.Fills.AsNoTracking().SingleOrDefaultAsync(x => x.BrokerAccountId == order.BrokerAccountId && x.BrokerExecutionId == execution.ExecutionId, token).ConfigureAwait(false);
        if (duplicate is not null)
        {
            var exact = duplicate.OrderId == order.Id && ExactDecimalText.FromProvider(duplicate.Quantity) == execution.Quantity.Amount &&
                ExactDecimalText.FromProvider(duplicate.Price) == execution.Price.Amount && duplicate.Currency == execution.Price.Currency.Code &&
                ExactDecimalText.FromProvider(duplicate.FeeAmount) == execution.Fee.Amount && duplicate.FeeCurrency == execution.Fee.Currency.Code &&
                duplicate.ExecutedAt == UtcUnixMilliseconds.ToProvider(execution.ExecutedAt);
            return exact
                ? await FinalizeAsync(command, FillAccountingWriteDisposition.Duplicate, FillAccountingCodes.Duplicate, transaction, token).ConfigureAwait(false)
                : await RejectAsync(command, FillAccountingCodes.IdentityMismatch, transaction, token).ConfigureAwait(false);
        }
        if (execution.Quantity.Unit != order.QuantityUnit)
            return await RejectAsync(command, FillAccountingCodes.UnitMismatch, transaction, token).ConfigureAwait(false);
        if (execution.Price.Currency.Code != order.Currency || execution.Fee.Currency.Code != order.Currency)
            return await RejectAsync(command, FillAccountingCodes.CurrencyMismatch, transaction, token).ConfigureAwait(false);
        if (order.Status is not (OrderStatus.Acknowledged or OrderStatus.PartiallyFilled or OrderStatus.CancelPending))
            return await RejectAsync(command, FillAccountingCodes.InvalidState, transaction, token).ConfigureAwait(false);

        var priorQuantity = await db.Fills.AsNoTracking().Where(x => x.OrderId == order.Id).Select(x => x.Quantity).ToListAsync(token).ConfigureAwait(false);
        var cumulative = priorQuantity.Sum(ExactDecimalText.FromProvider);
        if (cumulative + execution.Quantity.Amount > ExactDecimalText.FromProvider(order.Quantity))
            return await RejectAsync(command, FillAccountingCodes.Overfill, transaction, token).ConfigureAwait(false);

        var position = await db.Positions.SingleOrDefaultAsync(x => x.PortfolioId == order.PortfolioId && x.InstrumentId == order.InstrumentId, token).ConfigureAwait(false);
        var oldQuantity = position is null ? 0 : ExactDecimalText.FromProvider(position.Quantity);
        var oldAverage = position is null ? 0 : ExactDecimalText.FromProvider(position.AverageCostAmount);
        var delta = order.Side == "Buy" ? execution.Quantity.Amount : -execution.Quantity.Amount;
        var nextQuantity = oldQuantity + delta;
        if (nextQuantity < 0) return await RejectAsync(command, FillAccountingCodes.InsufficientPosition, transaction, token).ConfigureAwait(false);
        var realized = position is null ? 0 : ExactDecimalText.FromProvider(position.RealizedPnlAmount);
        var nextAverage = order.Side == "Buy"
            ? checked((oldAverage * oldQuantity + execution.Price.Amount * execution.Quantity.Amount) / nextQuantity)
            : nextQuantity == 0 ? 0 : oldAverage;
        if (order.Side == "Sell") realized = checked(realized + (execution.Price.Amount - oldAverage) * execution.Quantity.Amount - execution.Fee.Amount);

        var fillId = FillId.New();
        db.Fills.Add(new FillEntity
        {
            Id = fillId.ToString(),
            OrderId = order.Id,
            BrokerAccountId = order.BrokerAccountId,
            BrokerExecutionId = execution.ExecutionId,
            Quantity = ExactDecimalText.ToProvider(execution.Quantity.Amount),
            Price = ExactDecimalText.ToProvider(execution.Price.Amount),
            Currency = order.Currency,
            FeeAmount = ExactDecimalText.ToProvider(execution.Fee.Amount),
            FeeCurrency = order.Currency,
            ExecutedAt = UtcUnixMilliseconds.ToProvider(execution.ExecutedAt),
            ReceivedAt = UtcUnixMilliseconds.ToProvider(command.Message.ReceivedAt)
        });
        var at = UtcUnixMilliseconds.ToProvider(command.ProcessedAt);
        if (position is null)
        {
            if (order.Side != "Buy") return await RejectAsync(command, FillAccountingCodes.InsufficientPosition, transaction, token).ConfigureAwait(false);
            position = new PositionEntity
            {
                Id = PositionId.New().ToString(),
                PortfolioId = order.PortfolioId,
                InstrumentId = order.InstrumentId,
                QuantityUnit = order.QuantityUnit,
                Quantity = ExactDecimalText.ToProvider(nextQuantity),
                AverageCostAmount = ExactDecimalText.ToProvider(nextAverage),
                AverageCostCurrency = order.Currency,
                RealizedPnlAmount = "0",
                RealizedPnlCurrency = order.Currency,
                OpenedAt = at,
                UpdatedAt = at,
                Version = 1
            };
            db.Positions.Add(position);
        }
        else
        {
            if (position.QuantityUnit != order.QuantityUnit || position.AverageCostCurrency != order.Currency)
                return await RejectAsync(command, FillAccountingCodes.IdentityMismatch, transaction, token).ConfigureAwait(false);
            position.Quantity = ExactDecimalText.ToProvider(nextQuantity); position.AverageCostAmount = ExactDecimalText.ToProvider(nextAverage);
            position.RealizedPnlAmount = ExactDecimalText.ToProvider(realized); position.UpdatedAt = at;
            position.ClosedAt = nextQuantity == 0 ? at : null; position.Version++;
        }
        db.PositionAppliedFills.Add(new() { PositionId = position.Id, FillId = fillId.ToString(), AppliedAt = at });

        var gross = checked(execution.Quantity.Amount * execution.Price.Amount);
        var signedGross = order.Side == "Buy" ? -gross : gross;
        db.PortfolioLedgerEntries.AddRange(
            Ledger(order, execution, signedGross, "Settlement", execution.ExecutionId + ":trade", delta, "Broker execution trade"),
            Ledger(order, execution, -execution.Fee.Amount, "Fee", execution.ExecutionId + ":fee", null, "Broker execution fee"));

        var final = cumulative + execution.Quantity.Amount == ExactDecimalText.FromProvider(order.Quantity);
        var target = final ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
        var candidates = db.Orders.Where(x => x.Id == order.Id && x.Version == order.Version);
        var changed = final
            ? await candidates.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, target)
                .SetProperty(x => x.CompletedAt, at).SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false)
            : await candidates.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, target)
                .SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false);
        if (changed != 1) { await transaction.RollbackAsync(token).ConfigureAwait(false); return new(FillAccountingWriteDisposition.Contention, FillAccountingCodes.Contention); }
        db.OrderTransitions.Add(new()
        {
            Id = OrderTransitionId.New().ToString(),
            OrderId = order.Id,
            SequenceNumber = checked((int)order.Version + 1),
            PreviousStatus = order.Status,
            NewStatus = target,
            ReasonCode = FillAccountingCodes.Applied,
            Source = "Broker",
            OccurredAt = at,
            ReceivedAt = UtcUnixMilliseconds.ToProvider(command.Message.ReceivedAt),
            CorrelationId = command.Message.CorrelationId.Value
        });
        if (final && order.CapitalReservationId is not null)
            await db.CapitalReservations.Where(x => x.Id == order.CapitalReservationId && x.Status == "Active")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Consumed").SetProperty(x => x.ConsumedAt, at)
                    .SetProperty(x => x.Version, x => x.Version + 1), token).ConfigureAwait(false);

        if (await CompleteInboxAsync(command, FillAccountingCodes.Applied, token).ConfigureAwait(false) != 1)
        { await transaction.RollbackAsync(token).ConfigureAwait(false); return new(FillAccountingWriteDisposition.Contention, FillAccountingCodes.Contention); }
        await db.SaveChangesAsync(token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return new(FillAccountingWriteDisposition.Applied, FillAccountingCodes.Applied);
    }

    private static PortfolioLedgerEntryEntity Ledger(OrderEntity order, Trading.Core.Brokers.BrokerExecution execution,
        decimal amount, string type, string sourceId, decimal? quantity, string description) => new()
        {
            Id = PortfolioLedgerEntryId.New().ToString(),
            PortfolioId = order.PortfolioId,
            EntryType = type,
            Amount = ExactDecimalText.ToProvider(amount),
            Currency = order.Currency,
            InstrumentId = quantity is null ? null : order.InstrumentId,
            Quantity = quantity is null ? null : ExactDecimalText.ToProvider(quantity.Value),
            EffectiveAt = UtcUnixMilliseconds.ToProvider(execution.ExecutedAt),
            RecordedAt = UtcUnixMilliseconds.ToProvider(execution.ExecutedAt),
            SourceType = "BrokerExecution",
            SourceId = sourceId,
            Description = description,
            MetadataJson = CanonicalJsonSerializer.Serialize(1, new { executionId = execution.ExecutionId })
        };

    private Task<FillAccountingWriteResult> RejectAsync(ApplyFillAccountingCommand command, string code,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken token) =>
        FinalizeAsync(command, FillAccountingWriteDisposition.Rejected, code, transaction, token);

    private async Task<FillAccountingWriteResult> FinalizeAsync(ApplyFillAccountingCommand command,
        FillAccountingWriteDisposition disposition, string code,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken token)
    {
        if (await CompleteInboxAsync(command, code, token).ConfigureAwait(false) != 1)
        { await transaction.RollbackAsync(token).ConfigureAwait(false); return new(FillAccountingWriteDisposition.Contention, FillAccountingCodes.Contention); }
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return new(disposition, code);
    }

    private Task<int> CompleteInboxAsync(ApplyFillAccountingCommand command, string code, CancellationToken token) =>
        db.InboxMessages.Where(x => x.Id == command.Message.Id.ToString() && x.Status == "Claimed" && x.LeaseOwner == command.LeaseOwner)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Completed").SetProperty(x => x.LastError, code)
                .SetProperty(x => x.CompletedAt, UtcUnixMilliseconds.ToProvider(command.ProcessedAt))
                .SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseExpiresAt, (long?)null)
                .SetProperty(x => x.Version, x => x.Version + 1), token);
}
