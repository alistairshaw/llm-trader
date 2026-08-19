using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Proposals;

public enum ProposalType { DirectTrade, TargetAllocation }
public enum TradeSide { Buy, Sell }
public enum ProposalStatus { Recorded, Validating, Rejected, AwaitingHumanApproval, Approved, Expired, Cancelled, ConvertedToOrder }
public enum GuardrailOutcome { Passed, Failed }
public enum ApprovalDecision { Approved, Rejected }
public enum ApprovalActorType { User, AuthorizedPolicy }

public abstract record RequestedAction;

public sealed record DirectTradeAction : RequestedAction
{
    public DirectTradeAction(TradeSide side, Quantity quantity, string orderType, Price? limitPrice, string timeInForce)
    {
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        Side = side;
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
        if (quantity.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        OrderType = ProposalValidation.Required(orderType, nameof(orderType), 50);
        LimitPrice = limitPrice;
        TimeInForce = ProposalValidation.Required(timeInForce, nameof(timeInForce), 50);
    }
    public TradeSide Side { get; }
    public Quantity Quantity { get; }
    public string OrderType { get; }
    public Price? LimitPrice { get; }
    public string TimeInForce { get; }
}

public sealed record TargetAllocationAction : RequestedAction
{
    public TargetAllocationAction(Percentage targetPercentage)
    {
        TargetPercentage = targetPercentage ?? throw new ArgumentNullException(nameof(targetPercentage));
        if (targetPercentage.Value < 0 || targetPercentage.Value > 100)
            throw new ArgumentOutOfRangeException(nameof(targetPercentage), "Target allocation must be between zero and one hundred percent.");
    }
    public Percentage TargetPercentage { get; }
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
        PortfolioDecisionSnapshotId stateSnapshotId)
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
    }
    public GuardrailEvaluationId Id { get; }
    public int Sequence { get; }
    public string EvaluationStage { get; }
    public string PolicyVersion { get; }
    public GuardrailOutcome Outcome { get; }
    public IReadOnlyList<GuardrailRuleResult> RuleResults { get; }
    public DateTimeOffset EvaluatedAt { get; }
    public PortfolioDecisionSnapshotId StateSnapshotId { get; }
}

public sealed class ProposalApproval
{
    internal ProposalApproval(ProposalApprovalId id, ApprovalDecision decision, ApprovalActorType actorType,
        string actorId, string? reason, DateTimeOffset decidedAt, long proposalVersion,
        PortfolioDecisionSnapshotId stateSnapshotId)
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
    }
    public ProposalApprovalId Id { get; }
    public ApprovalDecision Decision { get; }
    public ApprovalActorType ActorType { get; }
    public string ActorId { get; }
    public string? Reason { get; }
    public DateTimeOffset DecidedAt { get; }
    public long ProposalVersion { get; }
    public PortfolioDecisionSnapshotId StateSnapshotId { get; }
}

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
