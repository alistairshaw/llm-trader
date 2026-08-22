using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Identifiers;
using Trading.Core.Orders;

namespace Trading.Data;

public sealed class OrderExecutionQueries(TradingDbContext db) : IOrderExecutionQueries
{
    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(ExecutionQueryPrincipal principal, OrderQueryFilter filter,
        ExecutionPageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal); ArgumentNullException.ThrowIfNull(filter);
        var query = Authorized(principal);
        if (!principal.IsAdministrator)
        {
            var proposals = await query.Select(x => x.Proposal.Id).Distinct().ToArrayAsync(cancellationToken).ConfigureAwait(false);
            var invisible = await InvisibleProposalsAsync(principal, proposals, cancellationToken).ConfigureAwait(false);
            query = query.Where(x => !invisible.Contains(x.Proposal.Id));
        }
        if (filter.TradingBotId is not null) query = query.Where(x => x.Proposal.TradingBotId == filter.TradingBotId.ToString());
        if (filter.PortfolioId is not null) query = query.Where(x => x.Order.PortfolioId == filter.PortfolioId.ToString());
        if (filter.BrokerAccountId is not null) query = query.Where(x => x.Order.BrokerAccountId == filter.BrokerAccountId.ToString());
        if (filter.ProposalId is not null) query = query.Where(x => x.Order.TradeProposalId == filter.ProposalId.ToString());
        if (filter.Status is not null) query = query.Where(x => x.Order.Status == filter.Status);
        if (filter.Environment is not null) query = query.Where(x => x.Environment == filter.Environment);
        if (filter.From is not null) query = query.Where(x => x.Order.CreatedAt >= Milliseconds(filter.From.Value));
        if (filter.Through is not null) query = query.Where(x => x.Order.CreatedAt <= Milliseconds(filter.Through.Value));
        var rows = await query.OrderByDescending(x => x.Order.CreatedAt).ThenBy(x => x.Order.Id)
            .Skip(page.Offset).Take(page.Size).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(x => Item(x.Order, x.Proposal.TradingBotId)).ToArray();
    }

    public async Task<OrderExecutionDetail?> GetOrderAsync(ExecutionQueryPrincipal principal, OrderId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal); ArgumentNullException.ThrowIfNull(id);
        var root = await Authorized(principal).SingleOrDefaultAsync(x => x.Order.Id == id.ToString(), cancellationToken).ConfigureAwait(false);
        if (root is null) return null;
        if (!principal.IsAdministrator && (await InvisibleProposalsAsync(principal, [root.Proposal.Id], cancellationToken).ConfigureAwait(false)).Length != 0) return null;
        var fills = await db.Fills.AsNoTracking().Where(x => x.OrderId == root.Order.Id)
            .OrderBy(x => x.ExecutedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var transitions = await db.OrderTransitions.AsNoTracking().Where(x => x.OrderId == root.Order.Id)
            .OrderBy(x => x.SequenceNumber).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var attempts = await db.BrokerSubmissionAttempts.AsNoTracking().Where(x => x.OrderId == root.Order.Id)
            .OrderBy(x => x.StartedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var work = await db.OutboxMessages.AsNoTracking().Where(x => x.OrderId == root.Order.Id)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var reservation = await db.CapitalReservations.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == root.Order.Id, cancellationToken).ConfigureAwait(false);
        var evaluations = await db.GuardrailEvaluations.AsNoTracking().Where(x => x.TradeProposalId == root.Proposal.Id).OrderBy(x => x.EvaluatedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var approvals = await db.ProposalApprovals.AsNoTracking().Where(x => x.TradeProposalId == root.Proposal.Id).OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var reports = await db.TradeProposalEvidenceReports.AsNoTracking().Where(x => x.TradeProposalId == root.Proposal.Id)
            .Join(db.ResearchReports.AsNoTracking(), x => x.ResearchReportId, x => x.Id, (_, x) => x).OrderBy(x => x.GeneratedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var reconciliations = await db.BrokerReconciliations.AsNoTracking().Where(x => x.BrokerAccountId == root.Order.BrokerAccountId && x.CorrelationId == root.Order.CorrelationId)
            .OrderBy(x => x.StartedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var positions = await db.Positions.AsNoTracking().Where(x => x.PortfolioId == root.Order.PortfolioId && x.InstrumentId == root.Order.InstrumentId)
            .OrderBy(x => x.OpenedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var ledger = await db.PortfolioLedgerEntries.AsNoTracking().Where(x => x.PortfolioId == root.Order.PortfolioId &&
            (x.SourceId == root.Order.Id || fills.Select(f => f.Id).Contains(x.SourceId)))
            .OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var audit = transitions.Select(x => new ExecutionAuditEvent("order.transition", x.Id, Time(x.OccurredAt), x.NewStatus.ToString(), x.CorrelationId, x.ReasonCode, Redact(x.ReasonDetail)))
            .Concat(attempts.Select(x => new ExecutionAuditEvent("broker.submission", x.Id, Time(x.StartedAt), x.Outcome, x.CorrelationId, x.ResultCode, Redact(x.DiagnosticCode))))
            .Concat(work.Select(x => new ExecutionAuditEvent("broker.work", x.Id, Time(x.CreatedAt), x.Status, x.CorrelationId, x.WorkKind, Redact(x.LastError))))
            .Concat(new[] { new ExecutionAuditEvent("bot.run", root.Proposal.BotRunId, Time(root.Proposal.CreatedAt), "Completed", root.Order.CorrelationId, null, null),
                new ExecutionAuditEvent("proposal", root.Proposal.Id, Time(root.Proposal.CreatedAt), root.Proposal.Status, root.Order.CorrelationId, "proposal.converted", null) })
            .Concat(reports.Select(x => new ExecutionAuditEvent("research.report", x.Id, Time(x.GeneratedAt), x.Status, root.Order.CorrelationId, x.ContentHash, null)))
            .Concat(evaluations.Select(x => new ExecutionAuditEvent("guardrail.evaluation", x.Id, Time(x.EvaluatedAt), x.Outcome, root.Order.CorrelationId, x.ContentHash, null)))
            .Concat(approvals.Select(x => new ExecutionAuditEvent("proposal.approval", x.Id, Time(x.DecidedAt), x.Decision, root.Order.CorrelationId, x.ActorType, null)))
            .Concat(reservation is null ? [] : new[] { new ExecutionAuditEvent("capital.reservation", reservation.Id, Time(reservation.CreatedAt), reservation.Status, root.Order.CorrelationId, null, null) })
            .Concat(reconciliations.Select(x => new ExecutionAuditEvent("broker.reconciliation", x.Id, Time(x.StartedAt), x.Status, x.CorrelationId, x.ContentHash, null)))
            .Concat(fills.Select(x => new ExecutionAuditEvent("fill", x.Id, Time(x.ExecutedAt), "Applied", root.Order.CorrelationId, "execution.applied", x.BrokerExecutionId)))
            .Concat(positions.Select(x => new ExecutionAuditEvent("position", x.Id, Time(x.UpdatedAt), "Updated", root.Order.CorrelationId, x.QuantityUnit, x.Quantity)))
            .Concat(ledger.Select(x => new ExecutionAuditEvent("ledger", x.Id, Time(x.EffectiveAt), x.EntryType, root.Order.CorrelationId, x.SourceType, Redact(x.Description))))
            .OrderBy(x => x.At).ThenBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var projectedFills = fills.Select(x => new FillProjection(FillId.Parse(x.Id), x.BrokerExecutionId, Decimal(x.Quantity),
            Decimal(x.Price), x.Currency, Decimal(x.FeeAmount), Time(x.ExecutedAt), Time(x.ReceivedAt))).ToArray();
        decimal? reserved = reservation is null ? null : Decimal(reservation.Amount);
        var spent = projectedFills.Sum(x => x.Quantity * x.Price + x.Fee);
        var positionEffects = positions.Select(x => new PositionEffectProjection(PositionId.Parse(x.Id), Decimal(x.Quantity),
            x.QuantityUnit, Decimal(x.AverageCostAmount), Decimal(x.RealizedPnlAmount), x.AverageCostCurrency,
            Time(x.UpdatedAt))).ToArray();
        var ledgerEffects = ledger.Select(x => new LedgerEffectProjection(PortfolioLedgerEntryId.Parse(x.Id),
            x.EntryType, x.Amount is null ? null : Decimal(x.Amount), x.Currency,
            x.Quantity is null ? null : Decimal(x.Quantity), x.SourceType, x.SourceId, Time(x.EffectiveAt))).ToArray();
        return new(Item(root.Order, root.Proposal.TradingBotId), root.Order.BrokerOrderId,
            projectedFills.Sum(x => x.Quantity), projectedFills.Sum(x => x.Quantity * x.Price), projectedFills.Sum(x => x.Fee),
            reservation?.Status, reserved is null ? null : Math.Max(0, reserved.Value - spent), projectedFills,
            positionEffects, ledgerEffects, audit);
    }

    private IQueryable<AuthorizedOrder> Authorized(ExecutionQueryPrincipal principal)
    {
        var bots = principal.TradingBotIds.Select(x => x.ToString()).ToArray();
        var portfolios = principal.PortfolioIds.Select(x => x.ToString()).ToArray();
        var accounts = principal.BrokerAccountIds.Select(x => x.ToString()).ToArray();
        return from order in db.Orders.AsNoTracking()
               join proposal in db.TradeProposals.AsNoTracking() on order.TradeProposalId equals proposal.Id
               join portfolio in db.Portfolios.AsNoTracking() on order.PortfolioId equals portfolio.Id
               join account in db.BrokerAccounts.AsNoTracking() on order.BrokerAccountId equals account.Id
               join connection in db.BrokerConnections.AsNoTracking() on account.BrokerConnectionId equals connection.Id
               where portfolio.AssignedTradingBotId == proposal.TradingBotId && portfolio.BrokerAccountId == account.Id &&
                     order.PortfolioId == proposal.PortfolioId && (principal.IsAdministrator ||
                     bots.Contains(proposal.TradingBotId) && portfolios.Contains(order.PortfolioId) && accounts.Contains(order.BrokerAccountId))
               select new AuthorizedOrder { Order = order, Proposal = proposal, Environment = connection.Environment };
    }

    private async Task<string[]> InvisibleProposalsAsync(ExecutionQueryPrincipal principal, string[] proposalIds, CancellationToken token)
    {
        if (proposalIds.Length == 0) return [];
        var evidence = await (from link in db.TradeProposalEvidenceReports.AsNoTracking()
                              join report in db.ResearchReports.AsNoTracking() on link.ResearchReportId equals report.Id
                              join request in db.ResearchRequests.AsNoTracking() on report.ResearchRequestId equals request.Id
                              where proposalIds.Contains(link.TradeProposalId)
                              select new { link.TradeProposalId, report.Visibility, request.RequestingBotId, request.RequestJson })
            .ToArrayAsync(token).ConfigureAwait(false);
        var bots = principal.TradingBotIds.Select(x => x.ToString()).ToHashSet(StringComparer.Ordinal);
        var groups = (principal.RestrictedReportGroups ?? []).ToHashSet(StringComparer.Ordinal);
        return evidence.GroupBy(x => x.TradeProposalId, StringComparer.Ordinal)
            .Where(group => group.Any(x => x.Visibility != "Shared" &&
                (x.Visibility != "BotPrivate" || x.RequestingBotId is null || !bots.Contains(x.RequestingBotId)) &&
                (x.Visibility != "Restricted" || ResearchPersistenceMapper.RestrictedGroup(x.RequestJson) is not string restricted || !groups.Contains(restricted))))
            .Select(x => x.Key).ToArray();
    }

    private static OrderListItem Item(OrderEntity x, string bot) => new(OrderId.Parse(x.Id), x.ClientOrderId,
        TradingBotId.Parse(bot), PortfolioId.Parse(x.PortfolioId), BrokerAccountId.Parse(x.BrokerAccountId),
        TradeProposalId.Parse(x.TradeProposalId), InstrumentId.Parse(x.InstrumentId), Enum.Parse<OrderSide>(x.Side),
        Decimal(x.Quantity), x.QuantityUnit, x.Currency, x.Status, x.CorrelationId, Time(x.CreatedAt), x.CompletedAt is null ? null : Time(x.CompletedAt.Value));
    private static decimal Decimal(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
    private static DateTimeOffset Time(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);
    private static long Milliseconds(DateTimeOffset value) => value.Offset == TimeSpan.Zero ? value.ToUnixTimeMilliseconds() : throw new ArgumentException("Timestamp must be UTC.");
    private static string? Redact(string? value) => value is null ? null : value.Length <= 200 ? value : value[..197] + "...";
    private sealed class AuthorizedOrder
    {
        public required OrderEntity Order { get; init; }
        public required TradeProposalEntity Proposal { get; init; }
        public required string Environment { get; init; }
    }
}
