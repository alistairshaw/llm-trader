using Trading.Core.Identifiers;

namespace Trading.Core.Proposals;

public sealed class TradeProposal
{
    private readonly List<GuardrailEvaluation> _evaluations = [];
    private readonly List<ProposalApproval> _approvals = [];

    public TradeProposal(TradeProposalId id, TradingBotId tradingBotId, BotRunId botRunId, PortfolioId portfolioId,
        TradingBotConfigurationVersionId configurationVersionId, PortfolioDecisionSnapshotId portfolioSnapshotId,
        InstrumentId instrumentId, RequestedAction requestedAction, string rationale, ProposalContentVersion contentVersion,
        HypothesisEvidenceReference? hypothesisEvidence, IEnumerable<ReportEvidenceReference> reportEvidence,
        DateTimeOffset createdAt, DateTimeOffset validUntil)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        TradingBotId = tradingBotId ?? throw new ArgumentNullException(nameof(tradingBotId));
        BotRunId = botRunId ?? throw new ArgumentNullException(nameof(botRunId));
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        ConfigurationVersionId = configurationVersionId ?? throw new ArgumentNullException(nameof(configurationVersionId));
        PortfolioSnapshotId = portfolioSnapshotId ?? throw new ArgumentNullException(nameof(portfolioSnapshotId));
        InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
        RequestedAction = requestedAction ?? throw new ArgumentNullException(nameof(requestedAction));
        ProposalType = requestedAction switch { DirectTradeAction => ProposalType.DirectTrade, TargetAllocationAction => ProposalType.TargetAllocation, _ => throw new ArgumentException("Unsupported requested action.", nameof(requestedAction)) };
        Rationale = ProposalValidation.Required(rationale, nameof(rationale), 4000);
        ContentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
        HypothesisEvidence = hypothesisEvidence;
        HypothesisVersionId = hypothesisEvidence?.VersionId;
        ArgumentNullException.ThrowIfNull(reportEvidence);
        var exactReports = reportEvidence.ToArray();
        if (exactReports.Any(item => item is null) || exactReports.Select(item => item.ReportId).Distinct().Count() != exactReports.Length)
            throw new ArgumentException("Report evidence must contain unique, non-null exact versions.", nameof(reportEvidence));
        ReportEvidence = Array.AsReadOnly(exactReports);
        EvidenceReportIds = Array.AsReadOnly(exactReports.Select(item => item.ReportId).ToArray());
        CreatedAt = ProposalValidation.Utc(createdAt, nameof(createdAt));
        ValidUntil = ProposalValidation.Utc(validUntil, nameof(validUntil));
        if (validUntil <= createdAt) throw new ArgumentException("Proposal validity must end after creation.", nameof(validUntil));
        Status = ProposalStatus.Recorded;
    }
    private TradeProposal(TradeProposalState state)
        : this(state.Id, state.TradingBotId, state.BotRunId, state.PortfolioId, state.ConfigurationVersionId,
            state.PortfolioSnapshotId, state.InstrumentId, state.RequestedAction, state.Rationale,
            state.ContentVersion, state.HypothesisEvidence, state.ReportEvidence, state.CreatedAt, state.ValidUntil)
    {
        Status = state.Status; Version = state.Version;
        _evaluations.AddRange(state.Evaluations.OrderBy(x => x.Sequence).Select(GuardrailEvaluation.Rehydrate));
        _approvals.AddRange(state.Approvals.OrderBy(x => x.DecidedAt).ThenBy(x => x.Id.ToString(), StringComparer.Ordinal)
            .Select(ProposalApproval.Rehydrate));
    }
    public static TradeProposal Rehydrate(TradeProposalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Version < 0) throw new ArgumentException("Proposal version cannot be negative.", nameof(state));
        return new TradeProposal(state);
    }

    public TradeProposalId Id { get; }
    public TradingBotId TradingBotId { get; }
    public BotRunId BotRunId { get; }
    public PortfolioId PortfolioId { get; }
    public TradingBotConfigurationVersionId ConfigurationVersionId { get; }
    public PortfolioDecisionSnapshotId PortfolioSnapshotId { get; }
    public InstrumentId InstrumentId { get; }
    public ProposalType ProposalType { get; }
    public RequestedAction RequestedAction { get; }
    public string Rationale { get; }
    public HypothesisVersionId? HypothesisVersionId { get; }
    public IReadOnlyList<ResearchReportId> EvidenceReportIds { get; }
    public ProposalContentVersion ContentVersion { get; }
    public HypothesisEvidenceReference? HypothesisEvidence { get; }
    public IReadOnlyList<ReportEvidenceReference> ReportEvidence { get; }
    public ProposalStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ValidUntil { get; }
    public long Version { get; private set; }
    public IReadOnlyList<GuardrailEvaluation> GuardrailEvaluations => _evaluations.AsReadOnly();
    public IReadOnlyList<ProposalApproval> ApprovalHistory => _approvals.AsReadOnly();

    public void StartValidation(DateTimeOffset at) => Transition(ProposalStatus.Recorded, ProposalStatus.Validating, at);

    public GuardrailEvaluation RecordEvaluation(GuardrailEvaluationId id, string stage, string policyVersion,
        GuardrailOutcome outcome, IEnumerable<GuardrailRuleResult> ruleResults, DateTimeOffset evaluatedAt,
        PortfolioDecisionSnapshotId stateSnapshotId)
    {
        if (Status != ProposalStatus.Validating) throw new InvalidOperationException("Evaluations require validation to be active.");
        EnsureNotExpired(evaluatedAt);
        var evaluation = new GuardrailEvaluation(id, _evaluations.Count + 1, stage, policyVersion, outcome,
            ruleResults, evaluatedAt, stateSnapshotId);
        _evaluations.Add(evaluation);
        return evaluation;
    }

    public GuardrailEvaluation RecordEvaluation(GuardrailEvaluationId id, GuardrailPolicyReference policy,
        GuardrailOutcome outcome, IEnumerable<GuardrailRuleResult> ruleResults, DateTimeOffset evaluatedAt,
        FreshStateReference freshState)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(freshState);
        if (Status != ProposalStatus.Validating) throw new InvalidOperationException("Evaluations require validation to be active.");
        EnsureNotExpired(evaluatedAt);
        var evaluation = new GuardrailEvaluation(id, _evaluations.Count + 1, policy.Level.ToString(), policy.Version,
            outcome, ruleResults, evaluatedAt, freshState.SnapshotId, policy, freshState);
        _evaluations.Add(evaluation);
        return evaluation;
    }

    public void RequireHumanApproval(DateTimeOffset at) => Transition(ProposalStatus.Validating, ProposalStatus.AwaitingHumanApproval, at);

    public ProposalApproval Approve(ProposalApprovalId id, ApprovalActorType actorType, string actorId, string? reason,
        DateTimeOffset decidedAt, long reviewedProposalVersion, PortfolioDecisionSnapshotId reviewedSnapshotId)
    {
        if (Status is not ProposalStatus.Validating and not ProposalStatus.AwaitingHumanApproval)
            throw new InvalidOperationException("Proposal is not eligible for approval.");
        if (Status == ProposalStatus.AwaitingHumanApproval && actorType != ApprovalActorType.User)
            throw new InvalidOperationException("Human approval cannot be supplied by policy.");
        EnsureNotExpired(decidedAt);
        if (reviewedProposalVersion != Version) throw new InvalidOperationException("Approval does not match the reviewed proposal version.");
        if (reviewedSnapshotId != PortfolioSnapshotId) throw new InvalidOperationException("Approval does not match the reviewed snapshot.");
        var approval = AddDecision(id, ApprovalDecision.Approved, actorType, actorId, reason, decidedAt,
            reviewedProposalVersion, reviewedSnapshotId);
        Status = ProposalStatus.Approved; Version++;
        return approval;
    }

    public ProposalApproval Approve(ProposalApprovalId id, DecisionActor actor, string? reason,
        DateTimeOffset decidedAt, ProposalContentVersion reviewedContentVersion, FreshStateReference reviewedState)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(reviewedContentVersion);
        ArgumentNullException.ThrowIfNull(reviewedState);
        if (reviewedContentVersion != ContentVersion)
            throw new InvalidOperationException(ProposalGovernanceCodes.VersionMismatch);
        if (Status is not ProposalStatus.Validating and not ProposalStatus.AwaitingHumanApproval)
            throw new InvalidOperationException("Proposal is not eligible for approval.");
        if (Status == ProposalStatus.AwaitingHumanApproval && actor.Type != ApprovalActorType.User)
            throw new InvalidOperationException("Human approval cannot be supplied by policy.");
        EnsureNotExpired(decidedAt);
        if (reviewedState.SnapshotId != PortfolioSnapshotId)
            throw new InvalidOperationException(ProposalGovernanceCodes.StateMismatch);
        var approval = new ProposalApproval(id, ApprovalDecision.Approved, actor.Type, actor.Id, reason, decidedAt,
            Version, reviewedState.SnapshotId, reviewedContentVersion, reviewedState);
        _approvals.Add(approval);
        Status = ProposalStatus.Approved;
        Version++;
        return approval;
    }

    public ProposalApproval Reject(ProposalApprovalId id, ApprovalActorType actorType, string actorId, string reason,
        DateTimeOffset decidedAt, long reviewedProposalVersion, PortfolioDecisionSnapshotId reviewedSnapshotId)
    {
        if (Status is not ProposalStatus.Validating and not ProposalStatus.AwaitingHumanApproval)
            throw new InvalidOperationException("Proposal is not eligible for rejection.");
        EnsureNotExpired(decidedAt);
        if (reviewedProposalVersion != Version || reviewedSnapshotId != PortfolioSnapshotId)
            throw new InvalidOperationException("Decision does not match the reviewed proposal state.");
        var approval = AddDecision(id, ApprovalDecision.Rejected, actorType, actorId, reason, decidedAt,
            reviewedProposalVersion, reviewedSnapshotId);
        Status = ProposalStatus.Rejected; Version++;
        return approval;
    }

    public bool Expire(DateTimeOffset at)
    {
        ProposalValidation.Utc(at, nameof(at));
        if (Status == ProposalStatus.Expired) return false;
        if (at < ValidUntil) throw new InvalidOperationException("Proposal is still valid.");
        if (IsTerminal(Status) || Status == ProposalStatus.Approved) throw new InvalidOperationException("Proposal cannot expire from its current state.");
        Status = ProposalStatus.Expired; Version++; return true;
    }

    public void Cancel(DateTimeOffset at)
    {
        ProposalValidation.Utc(at, nameof(at));
        if (IsTerminal(Status)) throw new InvalidOperationException("Terminal proposal cannot be cancelled.");
        Status = ProposalStatus.Cancelled; Version++;
    }

    public bool ConvertToOrder(DateTimeOffset at)
    {
        ProposalValidation.Utc(at, nameof(at));
        if (Status == ProposalStatus.ConvertedToOrder) return false;
        if (Status != ProposalStatus.Approved) throw new InvalidOperationException("Only an approved proposal can become an order.");
        EnsureNotExpired(at); Status = ProposalStatus.ConvertedToOrder; Version++; return true;
    }

    private ProposalApproval AddDecision(ProposalApprovalId id, ApprovalDecision decision, ApprovalActorType actorType,
        string actorId, string? reason, DateTimeOffset decidedAt, long reviewedVersion, PortfolioDecisionSnapshotId snapshotId)
    {
        var approval = new ProposalApproval(id, decision, actorType, actorId, reason, decidedAt, reviewedVersion, snapshotId);
        _approvals.Add(approval); return approval;
    }
    private void Transition(ProposalStatus required, ProposalStatus next, DateTimeOffset at)
    {
        if (Status != required) throw new InvalidOperationException($"Cannot transition from {Status} to {next}.");
        EnsureNotExpired(at); Status = next; Version++;
    }
    private void EnsureNotExpired(DateTimeOffset at)
    {
        ProposalValidation.Utc(at, nameof(at));
        if (at >= ValidUntil) throw new InvalidOperationException("Proposal has expired.");
    }
    private static bool IsTerminal(ProposalStatus status) => status is ProposalStatus.Rejected or ProposalStatus.Expired or ProposalStatus.Cancelled or ProposalStatus.ConvertedToOrder;
}

public sealed record TradeProposalState(TradeProposalId Id, TradingBotId TradingBotId, BotRunId BotRunId,
    PortfolioId PortfolioId, TradingBotConfigurationVersionId ConfigurationVersionId,
    PortfolioDecisionSnapshotId PortfolioSnapshotId, InstrumentId InstrumentId, RequestedAction RequestedAction,
    string Rationale, ProposalContentVersion ContentVersion, HypothesisEvidenceReference? HypothesisEvidence,
    IReadOnlyList<ReportEvidenceReference> ReportEvidence, ProposalStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset ValidUntil, long Version, IReadOnlyList<GuardrailEvaluationState> Evaluations,
    IReadOnlyList<ProposalApprovalState> Approvals);
