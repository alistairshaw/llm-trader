using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Proposals;

namespace Trading.Data;

public sealed class ProposalQueries(TradingDbContext db) : IProposalQueries
{
    public async Task<IReadOnlyList<ProposalQueueItem>> GetQueueAsync(ProposalQueryPrincipal principal,
        ProposalQueueFilter filter, ProposalPageRequest page, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal); ArgumentNullException.ThrowIfNull(filter);
        var instant = Milliseconds(at);
        var query = AuthorizedProposals(principal);
        if (filter.TradingBotId is not null) query = query.Where(x => x.TradingBotId == filter.TradingBotId.ToString());
        if (filter.PortfolioId is not null) query = query.Where(x => x.PortfolioId == filter.PortfolioId.ToString());
        if (filter.BrokerAccountId is not null) query = query.Where(x => db.Portfolios.Any(p => p.Id == x.PortfolioId && p.BrokerAccountId == filter.BrokerAccountId.ToString()));
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status.ToString());
        if (!filter.IncludeExpired) query = query.Where(x => x.ValidUntil > instant);

        var rows = await query.OrderBy(x => x.ValidUntil).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var visible = new List<ProposalQueueItem>();
        foreach (var row in rows)
        {
            var proposal = await VisibleProposalAsync(principal, row.Id, cancellationToken).ConfigureAwait(false);
            if (proposal is null || filter.ExecutionMode is not null && proposal.ExecutionMode != filter.ExecutionMode) continue;
            var portfolio = await db.Portfolios.AsNoTracking().SingleAsync(x => x.Id == row.PortfolioId, cancellationToken).ConfigureAwait(false);
            var reservation = await db.CapitalReservations.AsNoTracking().SingleOrDefaultAsync(x => x.TradeProposalId == row.Id, cancellationToken).ConfigureAwait(false);
            visible.Add(new(proposal.Id, proposal.TradingBotId, proposal.PortfolioId, BrokerAccountId.Parse(portfolio.BrokerAccountId!),
                proposal.InstrumentId, proposal.ProposalType, proposal.Status, proposal.ExecutionMode, proposal.ContentVersion,
                proposal.ConfigurationVersionId, proposal.PortfolioSnapshotId, proposal.CreatedAt, proposal.ValidUntil,
                proposal.ValidUntil <= at, proposal.GuardrailEvaluations.Count, proposal.ApprovalHistory.Count,
                reservation is null ? null : CanonicalEnumeration.Parse<CapitalReservationStatus>(reservation.Status)));
        }
        return visible.Skip(page.Offset).Take(page.Size).ToArray();
    }

    public async Task<ProposalDetailProjection?> GetDetailAsync(ProposalQueryPrincipal principal,
        TradeProposalId proposalId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal); ArgumentNullException.ThrowIfNull(proposalId); _ = Milliseconds(at);
        var row = await AuthorizedProposals(principal).SingleOrDefaultAsync(x => x.Id == proposalId.ToString(), cancellationToken).ConfigureAwait(false);
        if (row is null) return null;
        var proposal = await VisibleProposalAsync(principal, row.Id, cancellationToken).ConfigureAwait(false);
        if (proposal is null) return null;
        var portfolio = await db.Portfolios.AsNoTracking().SingleAsync(x => x.Id == row.PortfolioId, cancellationToken).ConfigureAwait(false);
        var reservationRow = await db.CapitalReservations.AsNoTracking().SingleOrDefaultAsync(x => x.TradeProposalId == row.Id, cancellationToken).ConfigureAwait(false);
        var reservation = ProposalPersistenceMapper.ToDomain(reservationRow);
        return new(proposal.Id, proposal.TradingBotId, proposal.BotRunId, proposal.PortfolioId,
            BrokerAccountId.Parse(portfolio.BrokerAccountId!), proposal.ConfigurationVersionId, proposal.PortfolioSnapshotId,
            proposal.InstrumentId, proposal.ProposalType, proposal.RequestedAction, proposal.Rationale, proposal.ContentVersion,
            proposal.ExecutionMode, proposal.Status, proposal.CreatedAt, proposal.ValidUntil, proposal.ValidUntil <= at,
            proposal.HypothesisEvidence, proposal.ReportEvidence,
            proposal.GuardrailEvaluations.Select(x => new ProposalEvaluationProjection(x.Id, x.Sequence, x.EvaluationStage,
                x.Outcome, x.ContentHash!, x.EvaluatedAt, x.StateSnapshotId, x.FreshState, x.ProposalContentVersion,
                x.ConfigurationVersionId, x.EvaluatedPolicies, x.RuleResults, x.DiagnosticCode)).ToArray(),
            proposal.ApprovalHistory.Select(x => new ProposalDecisionProjection(x.Id, x.Decision, x.Actor, x.Reason,
                x.DecidedAt, x.ProposalVersion, x.StateSnapshotId, x.ReviewedContentVersion, x.ReviewedState)).ToArray(),
            reservation is null ? null : new(reservation.Id, reservation.Amount, reservation.Status, reservation.CreatedAt,
                reservation.ExpiresAt, reservation.Status == CapitalReservationStatus.Expired || reservation.ExpiresAt <= at,
                reservation.ConsumedAt, reservation.ReleasedAt, reservation.Version));
    }

    private IQueryable<TradeProposalEntity> AuthorizedProposals(ProposalQueryPrincipal principal)
    {
        var bots = principal.TradingBotIds.Select(x => x.ToString()).ToArray();
        var portfolios = principal.PortfolioIds.Select(x => x.ToString()).ToArray();
        var accounts = principal.BrokerAccountIds.Select(x => x.ToString()).ToArray();
        return db.TradeProposals.AsNoTracking().Where(proposal => db.Portfolios.Any(portfolio =>
            portfolio.Id == proposal.PortfolioId && portfolio.BrokerAccountId != null &&
            portfolio.AssignedTradingBotId == proposal.TradingBotId && (principal.IsAdministrator ||
                bots.Contains(proposal.TradingBotId) && portfolios.Contains(proposal.PortfolioId) &&
                accounts.Contains(portfolio.BrokerAccountId))));
    }

    private async Task<TradeProposal?> VisibleProposalAsync(ProposalQueryPrincipal principal, string proposalId, CancellationToken token)
    {
        var evidence = await (from link in db.TradeProposalEvidenceReports.AsNoTracking()
                              join report in db.ResearchReports.AsNoTracking() on link.ResearchReportId equals report.Id
                              join request in db.ResearchRequests.AsNoTracking() on report.ResearchRequestId equals request.Id
                              where link.TradeProposalId == proposalId
                              select new { report.Visibility, request.RequestingBotId, request.RequestJson }).ToListAsync(token).ConfigureAwait(false);
        if (!principal.IsAdministrator && evidence.Any(x => !Visible(principal, x.Visibility, x.RequestingBotId, x.RequestJson))) return null;
        return await ProposalPersistenceMapper.LoadProposalAsync(db, proposalId, token).ConfigureAwait(false);
    }

    private static bool Visible(ProposalQueryPrincipal principal, string visibility, string? ownerBotId, string requestJson) =>
        visibility == "Shared" || visibility == "BotPrivate" && principal.TradingBotIds.Any(x => x.ToString() == ownerBotId) ||
        visibility == "Restricted" && principal.RestrictedReportGroups.Contains(ResearchPersistenceMapper.RestrictedGroup(requestJson)!, StringComparer.Ordinal);

    private static long Milliseconds(DateTimeOffset value) => value.Offset == TimeSpan.Zero
        ? value.ToUnixTimeMilliseconds() : throw new ArgumentException("Projection timestamps must be UTC.", nameof(value));

}
