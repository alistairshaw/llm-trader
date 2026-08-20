using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Core.Proposals;

namespace Trading.Engine.Proposals;

public sealed record ProposalRecordingCommand(
    TradeProposalId ProposalId,
    TradingBotId TradingBotId,
    BotRunId BotRunId,
    TradingBotConfigurationVersionId ConfigurationVersionId,
    PortfolioId PortfolioId,
    PortfolioDecisionSnapshotId DecisionSnapshotId,
    InstrumentId InstrumentId,
    RequestedAction Action,
    string Rationale,
    ProposalContentVersion ContentVersion,
    HypothesisEvidenceReference? HypothesisEvidence,
    IReadOnlyList<ReportEvidenceReference> ReportEvidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset ValidUntil);

public sealed record ProposalRecordingContext(
    TradingBot Bot,
    BotRun Run,
    Portfolio Portfolio,
    PortfolioDecisionSnapshot DecisionSnapshot,
    IReadOnlySet<ResearchReportId> VisibleReportVersions);

public enum ProposalRecordingOutcome { Recorded, Duplicate, Rejected }
public sealed record ProposalRecordingResult(ProposalRecordingOutcome Outcome, string Code, TradeProposal? Proposal);

public interface IProposalRecorder
{
    Task<ProposalRecordingResult> RecordAsync(ProposalRecordingCommand command, CancellationToken cancellationToken);
}

public interface IProposalRecordingContextProvider
{
    Task<ProposalRecordingContext?> GetAsync(ProposalRecordingCommand command, CancellationToken cancellationToken);
}

public sealed record GuardrailPolicySet(
    GuardrailPolicyReference Platform,
    GuardrailPolicyReference Account,
    GuardrailPolicyReference Portfolio,
    GuardrailPolicyReference TradingBot)
{
    public IReadOnlyList<GuardrailPolicyReference> InEvaluationOrder =>
        [Platform, Account, Portfolio, TradingBot];
}

public sealed record GuardrailEvaluationRequest(
    TradeProposal Proposal,
    GuardrailPolicySet Policies,
    FreshStateReference FreshState,
    HierarchicalGuardrailPolicySet PolicyDefinitions,
    GuardrailState State);

public sealed record GuardrailEvaluationDecision(
    GuardrailOutcome Outcome,
    string Code,
    IReadOnlyList<GuardrailRuleResult> RuleResults,
    IReadOnlyList<GuardrailPolicyReference> EvaluatedPolicies,
    FreshStateReference FreshState);

public interface IGuardrailPolicyEvaluator
{
    Task<GuardrailEvaluationDecision> EvaluateAsync(GuardrailEvaluationRequest request, CancellationToken cancellationToken);
}

public sealed record ProposalDecisionAuthorizationRequest(
    DecisionActor Actor,
    TradeProposalId ProposalId,
    ProposalContentVersion ReviewedContentVersion,
    FreshStateReference ReviewedState,
    ApprovalDecision Decision);

public sealed record ProposalDecisionAuthorizationResult(bool Authorized, string Code);

public interface IProposalDecisionAuthorizer
{
    Task<ProposalDecisionAuthorizationResult> AuthorizeAsync(
        ProposalDecisionAuthorizationRequest request,
        CancellationToken cancellationToken);
}

public interface IFreshProposalStateProvider
{
    Task<FreshProposalState> AcquireAsync(TradeProposal proposal, CancellationToken cancellationToken);
}

public sealed record FreshProposalState(
    FreshStateReference Reference,
    Money AvailableCapital,
    Money ReservedCapital,
    DateTimeOffset MarketDataAsOf);

public sealed record CapitalAvailabilityRequest(
    PortfolioId PortfolioId,
    TradeProposalId ProposalId,
    Money RequestedAmount,
    FreshStateReference FreshState);

public sealed record CapitalAvailabilityResult(bool Available, Money UnreservedCapital, string Code);

public interface ICapitalAvailabilityProvider
{
    Task<CapitalAvailabilityResult> CheckAsync(CapitalAvailabilityRequest request, CancellationToken cancellationToken);
}

public sealed record CapitalReservationCommand(
    CapitalReservationId ReservationId,
    TradeProposalId ProposalId,
    PortfolioId PortfolioId,
    ProposalContentVersion ApprovedContentVersion,
    FreshStateReference ValidatedState,
    Money Amount,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public enum CapitalReservationOutcome { Reserved, AlreadyReserved, Rejected, ConcurrencyConflict }
public sealed record CapitalReservationResult(CapitalReservationOutcome Outcome, string Code, CapitalReservation? Reservation);

public interface ICapitalReservationService
{
    Task<CapitalReservationResult> ReserveAsync(CapitalReservationCommand command, CancellationToken cancellationToken);
}

public interface IProposalGovernanceClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IProposalGovernanceIdentifierSource
{
    TradeProposalId NewProposalId();
    GuardrailEvaluationId NewEvaluationId();
    ProposalApprovalId NewApprovalId();
    CapitalReservationId NewReservationId();
}

public interface IProposalGovernanceTransaction
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
