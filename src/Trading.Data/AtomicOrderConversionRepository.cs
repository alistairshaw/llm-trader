using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Core.Proposals;

namespace Trading.Data;

public sealed class AtomicOrderConversionRepository(TradingDbContext db) : IAtomicOrderConversionRepository
{
    public async Task<AtomicOrderConversionWriteResult> TryConvertAsync(
        AtomicOrderConversionRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.At.Offset != TimeSpan.Zero) throw new ArgumentException("Conversion time must be UTC.", nameof(request));
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, token).ConfigureAwait(false);

            var existingRow = await db.Orders.AsNoTracking().SingleOrDefaultAsync(
                x => x.TradeProposalId == request.ProposalId.ToString(), token).ConfigureAwait(false);
            if (existingRow is not null)
            {
                var existing = await new OrderRepository(db).FindByClientOrderIdAsync(
                    new ClientOrderIdentity(existingRow.ClientOrderId),
                    BrokerAccountId.Parse(existingRow.BrokerAccountId), token).ConfigureAwait(false);
                return existing is not null && existing.ClientOrderId == request.ClientOrderId.Value
                    ? new AtomicOrderConversionWriteResult.AlreadyCreated(existing)
                    : Reject(AtomicOrderConversionCodes.ReservationMismatch);
            }

            var proposal = await ProposalPersistenceMapper.LoadProposalAsync(
                db, request.ProposalId.ToString(), token).ConfigureAwait(false);
            if (proposal is null) return new AtomicOrderConversionWriteResult.NotFound();
            if (proposal.Status != ProposalStatus.Approved)
                return Reject(AtomicOrderConversionCodes.ProposalNotApproved);
            if (proposal.ValidUntil <= request.At)
                return Reject(AtomicOrderConversionCodes.ProposalExpired);
            if (proposal.ExecutionMode == ExecutionMode.ResearchOnly)
                return Reject(AtomicOrderConversionCodes.ResearchOnly);
            if (proposal.ExecutionMode != ExecutionMode.PaperTrading)
                return Reject(AtomicOrderConversionCodes.EnvironmentMismatch);

            var approval = proposal.ApprovalHistory.Count == 0 ? null : proposal.ApprovalHistory[^1];
            if (approval?.ReviewedContentVersion != proposal.ContentVersion)
                return Reject(AtomicOrderConversionCodes.ApprovalMismatch);
            var evaluation = proposal.GuardrailEvaluations.Count == 0 ? null : proposal.GuardrailEvaluations[^1];
            if (evaluation is null || evaluation.Outcome != GuardrailOutcome.Passed ||
                evaluation.ProposalContentVersion != proposal.ContentVersion ||
                evaluation.ConfigurationVersionId != proposal.ConfigurationVersionId ||
                evaluation.FreshState is null || evaluation.ContentHash is null)
                return Reject(AtomicOrderConversionCodes.EvaluationMismatch);

            var snapshot = await db.PortfolioDecisionSnapshots.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == evaluation.FreshState.SnapshotId.ToString(), token).ConfigureAwait(false);
            if (snapshot is null || snapshot.PortfolioId != proposal.PortfolioId.ToString() ||
                snapshot.TradingBotId != proposal.TradingBotId.ToString() ||
                snapshot.ConfigurationVersionId != proposal.ConfigurationVersionId.ToString() ||
                snapshot.ContentHash != evaluation.FreshState.ContentHash ||
                snapshot.AsOf != evaluation.FreshState.ObservedAt.ToUnixTimeMilliseconds())
                return Reject(AtomicOrderConversionCodes.SnapshotMismatch);

            var reservation = await db.CapitalReservations.SingleOrDefaultAsync(
                x => x.Id == request.ReservationId.ToString(), token).ConfigureAwait(false);
            if (reservation is null || reservation.TradeProposalId != proposal.Id.ToString() ||
                reservation.PortfolioId != proposal.PortfolioId.ToString() || reservation.Status != "Active" ||
                reservation.ExpiresAt <= request.At.ToUnixTimeMilliseconds() || reservation.OrderId is not null)
                return Reject(AtomicOrderConversionCodes.ReservationMismatch);

            var portfolio = await db.Portfolios.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == proposal.PortfolioId.ToString(), token).ConfigureAwait(false);
            if (portfolio?.AssignedTradingBotId != proposal.TradingBotId.ToString() || portfolio.BrokerAccountId is null)
                return Reject(AtomicOrderConversionCodes.PortfolioMismatch);
            var account = await db.BrokerAccounts.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == portfolio.BrokerAccountId, token).ConfigureAwait(false);
            if (account is null || account.Status != "Active")
                return Reject(AtomicOrderConversionCodes.AccountRestricted);
            if (account.LastReconciledAt is null)
                return Reject(AtomicOrderConversionCodes.AccountUnreconciled);
            var connection = await db.BrokerConnections.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == account.BrokerConnectionId, token).ConfigureAwait(false);
            if (connection is null || connection.Environment != "Paper" || connection.Status != "Enabled")
                return Reject(AtomicOrderConversionCodes.EnvironmentMismatch);

            var instrument = await db.Instruments.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == proposal.InstrumentId.ToString(), token).ConfigureAwait(false);
            if (instrument is null || instrument.Status != "Active")
                return Reject(AtomicOrderConversionCodes.InstrumentUnavailable);
            var at = request.At.ToUnixTimeMilliseconds();
            var mapping = await db.InstrumentBrokerMappings.AsNoTracking().SingleOrDefaultAsync(
                x => x.InstrumentId == instrument.Id && x.BrokerConnectionId == connection.Id &&
                    x.EffectiveFrom <= at && (x.EffectiveTo == null || x.EffectiveTo > at), token).ConfigureAwait(false);
            if (mapping is null) return Reject(AtomicOrderConversionCodes.InstrumentMappingUnavailable);
            if (instrument.Currency != reservation.Currency || account.BaseCurrency != reservation.Currency ||
                portfolio.BaseCurrency != reservation.Currency)
                return Reject(AtomicOrderConversionCodes.CurrencyMismatch);
            if (proposal.RequestedAction is not DirectTradeAction action)
                return Reject(AtomicOrderConversionCodes.UnsupportedAction);

            if (!Enum.TryParse<OrderType>(action.OrderType, false, out var orderType) ||
                !Enum.TryParse<TimeInForce>(action.TimeInForce, false, out var timeInForce))
                return Reject(AtomicOrderConversionCodes.UnsupportedAction);
            var side = action.Side == TradeSide.Buy ? OrderSide.Buy : OrderSide.Sell;
            var currency = new Currency(reservation.Currency);
            if (action.LimitPrice is not null && action.LimitPrice.Currency != currency)
                return Reject(AtomicOrderConversionCodes.CurrencyMismatch);

            var order = new Order(request.OrderId, request.ClientOrderId.Value, proposal.PortfolioId,
                BrokerAccountId.Parse(account.Id), proposal.Id, proposal.InstrumentId, side, action.Quantity,
                currency, orderType, action.LimitPrice, timeInForce, request.At);
            var payload = CanonicalJsonSerializer.Serialize(1, new SubmitOrderAuthorization(
                order.Id.ToString(), request.ClientOrderId.Value, proposal.Id.ToString(),
                proposal.ContentVersion.Version, proposal.ContentVersion.ContentHash,
                proposal.ConfigurationVersionId.ToString(), evaluation.Id.ToString(), evaluation.ContentHash,
                evaluation.FreshState.SnapshotId.ToString(), evaluation.FreshState.ContentHash,
                approval.Id.ToString(), reservation.Id, account.Id, connection.Id, mapping.Id,
                instrument.Id, "Paper", side.ToString(), CanonicalDecimal.Format(action.Quantity.Amount),
                action.Quantity.Unit, reservation.Currency, orderType.ToString(),
                action.LimitPrice is null ? null : CanonicalDecimal.Format(action.LimitPrice.Amount),
                timeInForce.ToString(), request.CorrelationId.Value));

            db.Orders.Add(ToOrderEntity(order, request.ReservationId, request.CorrelationId));
            db.OutboxMessages.Add(new OutboxMessageEntity
            {
                Id = request.WorkItemId.ToString(),
                OrderId = order.Id.ToString(),
                WorkKind = "Submit",
                IdempotencyKey = $"submit:{request.ClientOrderId.Value}",
                PayloadJson = payload,
                PayloadHash = CanonicalJsonSerializer.Sha256(payload),
                CorrelationId = request.CorrelationId.Value,
                Status = "Pending",
                AttemptCount = 0,
                AvailableAt = at,
                CreatedAt = at,
                Version = 1
            });
            proposal.ConvertToOrder(request.At);
            var proposalRow = await db.TradeProposals.SingleAsync(x => x.Id == proposal.Id.ToString(), token).ConfigureAwait(false);
            proposalRow.Status = "ConvertedToOrder"; proposalRow.Version = proposal.Version;
            reservation.OrderId = order.Id.ToString(); reservation.Version++;

            await db.SaveChangesAsync(token).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return new AtomicOrderConversionWriteResult.Created(order);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException
        { SqliteExtendedErrorCode: 1555 or 2067 })
        {
            db.ChangeTracker.Clear();
            return new AtomicOrderConversionWriteResult.Contention();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
        {
            db.ChangeTracker.Clear();
            return new AtomicOrderConversionWriteResult.Contention();
        }
    }

    private static AtomicOrderConversionWriteResult.Rejected Reject(string code) => new(code);

    private static OrderEntity ToOrderEntity(Order order, CapitalReservationId reservationId,
        CorrelationIdentity correlationId) => new()
        {
            Id = order.Id.ToString(),
            ClientOrderId = order.ClientOrderId,
            PortfolioId = order.PortfolioId.ToString(),
            BrokerAccountId = order.BrokerAccountId.ToString(),
            TradeProposalId = order.TradeProposalId.ToString(),
            CapitalReservationId = reservationId.ToString(),
            InstrumentId = order.InstrumentId.ToString(),
            Side = order.Side.ToString(),
            Quantity = CanonicalDecimal.Format(order.Quantity.Amount),
            QuantityUnit = order.Quantity.Unit,
            Currency = order.Currency.Code,
            OrderType = order.OrderType.ToString(),
            LimitPrice = order.LimitPrice is null ? null : CanonicalDecimal.Format(order.LimitPrice.Amount),
            TimeInForce = order.TimeInForce,
            Status = order.Status,
            CorrelationId = correlationId.Value,
            CreatedAt = UtcUnixMilliseconds.ToProvider(order.CreatedAt),
            Version = order.Version
        };

}
