using System.Collections.ObjectModel;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Proposals;

public sealed record ProposalQueryPrincipal
{
    public ProposalQueryPrincipal(string actorId, bool isAdministrator,
        IEnumerable<TradingBotId>? tradingBotIds = null, IEnumerable<PortfolioId>? portfolioIds = null,
        IEnumerable<BrokerAccountId>? brokerAccountIds = null, IEnumerable<string>? restrictedReportGroups = null)
    {
        ActorId = Required(actorId, nameof(actorId));
        IsAdministrator = isAdministrator;
        TradingBotIds = Frozen(tradingBotIds);
        PortfolioIds = Frozen(portfolioIds);
        BrokerAccountIds = Frozen(brokerAccountIds);
        RestrictedReportGroups = Array.AsReadOnly((restrictedReportGroups ?? []).Select(x => Required(x, nameof(restrictedReportGroups)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public string ActorId { get; }
    public bool IsAdministrator { get; }
    public IReadOnlyList<TradingBotId> TradingBotIds { get; }
    public IReadOnlyList<PortfolioId> PortfolioIds { get; }
    public IReadOnlyList<BrokerAccountId> BrokerAccountIds { get; }
    public IReadOnlyList<string> RestrictedReportGroups { get; }

    private static ReadOnlyCollection<T> Frozen<T>(IEnumerable<T>? values) where T : notnull =>
        Array.AsReadOnly((values ?? []).Distinct().OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray());
    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 200 ? throw new ArgumentException("A bounded actor or group identifier is required.", name) : value.Trim();
}

public sealed record ProposalQueueFilter(TradingBotId? TradingBotId = null, PortfolioId? PortfolioId = null,
    BrokerAccountId? BrokerAccountId = null, ProposalStatus? Status = ProposalStatus.AwaitingHumanApproval,
    ExecutionMode? ExecutionMode = null, bool IncludeExpired = false);

public readonly record struct ProposalPageRequest
{
    public const int MaximumSize = 100;
    public ProposalPageRequest(int offset, int size)
    {
        if (offset < 0 || size is < 1 or > MaximumSize) throw new ArgumentOutOfRangeException(nameof(size), "Proposal pages are limited to 100 rows.");
        Offset = offset; Size = size;
    }
    public int Offset { get; }
    public int Size { get; }
}

public sealed record ProposalQueueItem(TradeProposalId Id, TradingBotId TradingBotId, PortfolioId PortfolioId,
    BrokerAccountId BrokerAccountId, InstrumentId InstrumentId, ProposalType ProposalType, ProposalStatus Status,
    ExecutionMode ExecutionMode, ProposalContentVersion ContentVersion, TradingBotConfigurationVersionId ConfigurationVersionId,
    PortfolioDecisionSnapshotId SnapshotId, DateTimeOffset CreatedAt, DateTimeOffset ValidUntil, bool IsExpired,
    int EvaluationCount, int DecisionCount, CapitalReservationStatus? ReservationStatus);

public sealed record ProposalEvaluationProjection(GuardrailEvaluationId Id, int Sequence, string Stage,
    GuardrailOutcome Outcome, string ContentHash, DateTimeOffset EvaluatedAt, PortfolioDecisionSnapshotId StateSnapshotId,
    FreshStateReference? FreshState, ProposalContentVersion? ProposalContentVersion,
    TradingBotConfigurationVersionId? ConfigurationVersionId, IReadOnlyList<GuardrailPolicyReference> Policies,
    IReadOnlyList<GuardrailRuleResult> RuleResults, string? DiagnosticCode);

public sealed record ProposalDecisionProjection(ProposalApprovalId Id, ApprovalDecision Decision, DecisionActor Actor,
    string? Reason, DateTimeOffset DecidedAt, long ProposalVersion, PortfolioDecisionSnapshotId StateSnapshotId,
    ProposalContentVersion? ReviewedContentVersion, FreshStateReference? ReviewedState);

public sealed record CapitalReservationProjection(CapitalReservationId Id, Money Amount, CapitalReservationStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool IsExpired, DateTimeOffset? ConsumedAt,
    DateTimeOffset? ReleasedAt, long Version);

public sealed record ProposalDetailProjection(TradeProposalId Id, TradingBotId TradingBotId, BotRunId BotRunId,
    PortfolioId PortfolioId, BrokerAccountId BrokerAccountId, TradingBotConfigurationVersionId ConfigurationVersionId,
    PortfolioDecisionSnapshotId SnapshotId, InstrumentId InstrumentId, ProposalType ProposalType,
    RequestedAction RequestedAction, string Rationale, ProposalContentVersion ContentVersion, ExecutionMode ExecutionMode,
    ProposalStatus Status, DateTimeOffset CreatedAt, DateTimeOffset ValidUntil, bool IsExpired,
    HypothesisEvidenceReference? HypothesisEvidence, IReadOnlyList<ReportEvidenceReference> ReportEvidence,
    IReadOnlyList<ProposalEvaluationProjection> Evaluations, IReadOnlyList<ProposalDecisionProjection> Decisions,
    CapitalReservationProjection? Reservation);

public interface IProposalQueries
{
    Task<IReadOnlyList<ProposalQueueItem>> GetQueueAsync(ProposalQueryPrincipal principal, ProposalQueueFilter filter,
        ProposalPageRequest page, DateTimeOffset at, CancellationToken cancellationToken);
    Task<ProposalDetailProjection?> GetDetailAsync(ProposalQueryPrincipal principal, TradeProposalId proposalId,
        DateTimeOffset at, CancellationToken cancellationToken);
}
