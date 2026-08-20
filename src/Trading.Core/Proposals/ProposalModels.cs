using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Proposals;

public enum ProposalType { DirectTrade, TargetAllocation }
public enum TradeSide { Buy, Sell }
public enum ProposedOrderType { Market, Limit }
public enum ProposedTimeInForce { Day, GoodTillCancelled }
public enum ProposalStatus { Recorded, Validating, Rejected, AwaitingHumanApproval, Approved, Expired, Cancelled, ConvertedToOrder }
public enum GuardrailOutcome { Passed, Failed }
public enum ApprovalDecision { Approved, Rejected }
public enum ApprovalActorType { User, AuthorizedPolicy }
public enum GuardrailPolicyLevel { Platform, Account, Portfolio, TradingBot }

public static class ProposalGovernanceCodes
{
    public const string Succeeded = "proposal_governance.succeeded";
    public const string InvalidTransition = "proposal_governance.invalid_transition";
    public const string Expired = "proposal_governance.expired";
    public const string VersionMismatch = "proposal_governance.version_mismatch";
    public const string StateMismatch = "proposal_governance.state_mismatch";
    public const string UnauthorizedActor = "proposal_governance.unauthorized_actor";
    public const string PortfolioNotAssigned = "proposal_governance.portfolio_not_assigned";
    public const string EvidenceNotVisible = "proposal_governance.evidence_not_visible";
    public const string PolicyRejected = "proposal_governance.policy_rejected";
    public const string InsufficientCapital = "proposal_governance.insufficient_capital";
    public const string ConcurrencyConflict = "proposal_governance.concurrency_conflict";
    public const string ResearchOnly = "proposal_governance.research_only";
    public const string Cancelled = "proposal_governance.cancelled";
}

public sealed record ProposalContentVersion
{
    public ProposalContentVersion(int version, string contentHash)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        Version = version;
        ContentHash = ProposalValidation.Required(contentHash, nameof(contentHash), 256);
    }
    public int Version { get; }
    public string ContentHash { get; }
}

public sealed record ReportEvidenceReference
{
    public ReportEvidenceReference(ResearchReportId reportId, string reportSeriesId, int versionNumber, string contentHash)
    {
        ReportId = reportId ?? throw new ArgumentNullException(nameof(reportId));
        ReportSeriesId = ProposalValidation.Required(reportSeriesId, nameof(reportSeriesId), 200);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        VersionNumber = versionNumber;
        ContentHash = ProposalValidation.Required(contentHash, nameof(contentHash), 256);
    }
    public ResearchReportId ReportId { get; }
    public string ReportSeriesId { get; }
    public int VersionNumber { get; }
    public string ContentHash { get; }
}

public sealed record HypothesisEvidenceReference
{
    public HypothesisEvidenceReference(HypothesisVersionId versionId, string contentHash)
    {
        VersionId = versionId ?? throw new ArgumentNullException(nameof(versionId));
        ContentHash = ProposalValidation.Required(contentHash, nameof(contentHash), 256);
    }
    public HypothesisVersionId VersionId { get; }
    public string ContentHash { get; }
}

public sealed record GuardrailPolicyReference
{
    public GuardrailPolicyReference(GuardrailPolicyLevel level, string policyId, string version)
    {
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level));
        Level = level;
        PolicyId = ProposalValidation.Required(policyId, nameof(policyId), 200);
        Version = ProposalValidation.Required(version, nameof(version), 100);
    }
    public GuardrailPolicyLevel Level { get; }
    public string PolicyId { get; }
    public string Version { get; }
}

public sealed record FreshStateReference
{
    public FreshStateReference(PortfolioDecisionSnapshotId snapshotId, DateTimeOffset observedAt, string contentHash)
    {
        SnapshotId = snapshotId ?? throw new ArgumentNullException(nameof(snapshotId));
        ObservedAt = ProposalValidation.Utc(observedAt, nameof(observedAt));
        ContentHash = ProposalValidation.Required(contentHash, nameof(contentHash), 256);
    }
    public PortfolioDecisionSnapshotId SnapshotId { get; }
    public DateTimeOffset ObservedAt { get; }
    public string ContentHash { get; }
}

public sealed record DecisionActor
{
    public DecisionActor(ApprovalActorType type, string id)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Type = type;
        Id = ProposalValidation.Required(id, nameof(id), 200);
    }
    public ApprovalActorType Type { get; }
    public string Id { get; }
}

public abstract record RequestedAction;

public sealed record DirectTradeAction : RequestedAction
{
    public const int CurrentSchemaVersion = 1;
    public DirectTradeAction(TradeSide side, Quantity quantity, string orderType, Price? limitPrice, string timeInForce)
    {
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        Side = side;
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
        if (quantity.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        OrderType = ProposalValidation.Required(orderType, nameof(orderType), 50);
        LimitPrice = limitPrice;
        TimeInForce = ProposalValidation.Required(timeInForce, nameof(timeInForce), 50);
        SchemaVersion = CurrentSchemaVersion;
    }
    public DirectTradeAction(TradeSide side, Quantity quantity, ProposedOrderType orderType, Price? limitPrice,
        ProposedTimeInForce timeInForce)
        : this(side, quantity, orderType.ToString(), limitPrice, timeInForce.ToString())
    {
        if (!Enum.IsDefined(orderType)) throw new ArgumentOutOfRangeException(nameof(orderType));
        if (!Enum.IsDefined(timeInForce)) throw new ArgumentOutOfRangeException(nameof(timeInForce));
        if (orderType == ProposedOrderType.Limit && limitPrice is null)
            throw new ArgumentException("A limit proposal requires a limit price.", nameof(limitPrice));
        if (orderType == ProposedOrderType.Market && limitPrice is not null)
            throw new ArgumentException("A market proposal cannot specify a limit price.", nameof(limitPrice));
    }
    public int SchemaVersion { get; }
    public TradeSide Side { get; }
    public Quantity Quantity { get; }
    public string OrderType { get; }
    public Price? LimitPrice { get; }
    public string TimeInForce { get; }
}

public sealed record TargetAllocationAction : RequestedAction
{
    public const int CurrentSchemaVersion = 1;
    public TargetAllocationAction(Percentage targetPercentage)
    {
        TargetPercentage = targetPercentage ?? throw new ArgumentNullException(nameof(targetPercentage));
        if (targetPercentage.Value < 0 || targetPercentage.Value > 100)
            throw new ArgumentOutOfRangeException(nameof(targetPercentage), "Target allocation must be between zero and one hundred percent.");
        SchemaVersion = CurrentSchemaVersion;
    }
    public Percentage TargetPercentage { get; }
    public int SchemaVersion { get; }
}

public sealed record GuardrailRuleResult
{
    public GuardrailRuleResult(string rule, GuardrailOutcome outcome, string reason)
    {
        Rule = ProposalValidation.Required(rule, nameof(rule), 200);
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        Outcome = outcome;
        Reason = ProposalValidation.Required(reason, nameof(reason), 1000);
    }
    public string Rule { get; }
    public GuardrailOutcome Outcome { get; }
    public string Reason { get; }
}

public sealed class GuardrailEvaluation
{
    internal GuardrailEvaluation(GuardrailEvaluationId id, int sequence, string evaluationStage, string policyVersion,
        GuardrailOutcome outcome, IEnumerable<GuardrailRuleResult> ruleResults, DateTimeOffset evaluatedAt,
        PortfolioDecisionSnapshotId stateSnapshotId, GuardrailPolicyReference? policyReference = null,
        FreshStateReference? freshState = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Sequence = sequence;
        EvaluationStage = ProposalValidation.Required(evaluationStage, nameof(evaluationStage), 100);
        PolicyVersion = ProposalValidation.Required(policyVersion, nameof(policyVersion), 100);
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        Outcome = outcome;
        RuleResults = Array.AsReadOnly((ruleResults ?? throw new ArgumentNullException(nameof(ruleResults))).ToArray());
        EvaluatedAt = ProposalValidation.Utc(evaluatedAt, nameof(evaluatedAt));
        StateSnapshotId = stateSnapshotId ?? throw new ArgumentNullException(nameof(stateSnapshotId));
        PolicyReference = policyReference;
        FreshState = freshState;
    }
    public GuardrailEvaluationId Id { get; }
    public int Sequence { get; }
    public string EvaluationStage { get; }
    public string PolicyVersion { get; }
    public GuardrailOutcome Outcome { get; }
    public IReadOnlyList<GuardrailRuleResult> RuleResults { get; }
    public DateTimeOffset EvaluatedAt { get; }
    public PortfolioDecisionSnapshotId StateSnapshotId { get; }
    public GuardrailPolicyReference? PolicyReference { get; }
    public FreshStateReference? FreshState { get; }
    internal static GuardrailEvaluation Rehydrate(GuardrailEvaluationState state) => new(state.Id, state.Sequence,
        state.EvaluationStage, state.PolicyVersion, state.Outcome, state.RuleResults, state.EvaluatedAt,
        state.StateSnapshotId, state.PolicyReference, state.FreshState);
}

public sealed record GuardrailEvaluationState(GuardrailEvaluationId Id, int Sequence, string EvaluationStage,
    string PolicyVersion, GuardrailOutcome Outcome, IReadOnlyList<GuardrailRuleResult> RuleResults,
    DateTimeOffset EvaluatedAt, PortfolioDecisionSnapshotId StateSnapshotId,
    GuardrailPolicyReference? PolicyReference = null, FreshStateReference? FreshState = null);

public sealed class ProposalApproval
{
    internal ProposalApproval(ProposalApprovalId id, ApprovalDecision decision, ApprovalActorType actorType,
        string actorId, string? reason, DateTimeOffset decidedAt, long proposalVersion,
        PortfolioDecisionSnapshotId stateSnapshotId, ProposalContentVersion? reviewedContentVersion = null,
        FreshStateReference? reviewedState = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision));
        if (!Enum.IsDefined(actorType)) throw new ArgumentOutOfRangeException(nameof(actorType));
        Decision = decision; ActorType = actorType;
        ActorId = ProposalValidation.Required(actorId, nameof(actorId), 200);
        Reason = ProposalValidation.Optional(reason, nameof(reason), 1000);
        DecidedAt = ProposalValidation.Utc(decidedAt, nameof(decidedAt));
        ProposalVersion = proposalVersion;
        StateSnapshotId = stateSnapshotId ?? throw new ArgumentNullException(nameof(stateSnapshotId));
        ReviewedContentVersion = reviewedContentVersion;
        ReviewedState = reviewedState;
    }
    public ProposalApprovalId Id { get; }
    public ApprovalDecision Decision { get; }
    public ApprovalActorType ActorType { get; }
    public string ActorId { get; }
    public string? Reason { get; }
    public DateTimeOffset DecidedAt { get; }
    public long ProposalVersion { get; }
    public PortfolioDecisionSnapshotId StateSnapshotId { get; }
    public ProposalContentVersion? ReviewedContentVersion { get; }
    public FreshStateReference? ReviewedState { get; }
    public DecisionActor Actor => new(ActorType, ActorId);
    internal static ProposalApproval Rehydrate(ProposalApprovalState state) => new(state.Id, state.Decision,
        state.ActorType, state.ActorId, state.Reason, state.DecidedAt, state.ProposalVersion,
        state.StateSnapshotId, state.ReviewedContentVersion, state.ReviewedState);
}

public sealed record ProposalApprovalState(ProposalApprovalId Id, ApprovalDecision Decision,
    ApprovalActorType ActorType, string ActorId, string? Reason, DateTimeOffset DecidedAt,
    long ProposalVersion, PortfolioDecisionSnapshotId StateSnapshotId,
    ProposalContentVersion? ReviewedContentVersion = null, FreshStateReference? ReviewedState = null);

internal static class ProposalValidation
{
    public static string Required(string? value, string name, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        var trimmed = value.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Value is required.", name);
        if (trimmed.Length > maximumLength) throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", name);
        return trimmed;
    }
    public static string? Optional(string? value, string name, int maximumLength) =>
        value is null ? null : Required(value, name, maximumLength);
    public static DateTimeOffset Utc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be expressed in UTC.", name);
        return value;
    }
}
