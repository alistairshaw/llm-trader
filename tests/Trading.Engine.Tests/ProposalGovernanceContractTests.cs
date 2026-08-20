using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;

namespace Trading.Engine.Tests;

[Category("ProposalContracts")]
public sealed class ProposalGovernanceContractTests
{
    [Test]
    public void HierarchicalPolicySetHasOneUnambiguousEvaluationOrder()
    {
        var policies = new GuardrailPolicySet(
            Policy(GuardrailPolicyLevel.Platform), Policy(GuardrailPolicyLevel.Account),
            Policy(GuardrailPolicyLevel.Portfolio), Policy(GuardrailPolicyLevel.TradingBot));

        Assert.That(policies.InEvaluationOrder.Select(policy => policy.Level), Is.EqualTo(new[]
        {
            GuardrailPolicyLevel.Platform,
            GuardrailPolicyLevel.Account,
            GuardrailPolicyLevel.Portfolio,
            GuardrailPolicyLevel.TradingBot,
        }));
    }

    [Test]
    public void ReservationCommandPinsApprovalContentAndFreshState()
    {
        var version = new ProposalContentVersion(3, "proposal-hash");
        var state = new FreshStateReference(PortfolioDecisionSnapshotId.New(),
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero), "state-hash");
        var command = new CapitalReservationCommand(CapitalReservationId.New(), TradeProposalId.New(),
            PortfolioId.New(), version, state, new Money(100, Currency.USD), state.ObservedAt,
            state.ObservedAt.AddMinutes(5));

        Assert.Multiple(() =>
        {
            Assert.That(command.ApprovedContentVersion, Is.SameAs(version));
            Assert.That(command.ValidatedState, Is.SameAs(state));
            Assert.That(command.Amount.Amount, Is.EqualTo(100m));
        });
    }

    [Test]
    public void GovernancePortsExposeCancellationOnEverySideEffect()
    {
        var ports = new[]
        {
            typeof(IProposalRecorder), typeof(IProposalRecordingContextProvider), typeof(IGuardrailPolicyEvaluator),
            typeof(IProposalDecisionAuthorizer), typeof(IFreshProposalStateProvider), typeof(ICapitalAvailabilityProvider),
            typeof(ICapitalReservationService), typeof(IProposalGovernanceTransaction),
        };
        Assert.That(ports.SelectMany(type => type.GetMethods())
            .All(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(CancellationToken))), Is.True);
    }

    [Test]
    public void StableResultCodesCoverGovernanceTerminalAndRejectionOutcomes()
    {
        Assert.That(new[]
        {
            ProposalGovernanceCodes.Succeeded, ProposalGovernanceCodes.InvalidTransition,
            ProposalGovernanceCodes.Expired, ProposalGovernanceCodes.VersionMismatch,
            ProposalGovernanceCodes.StateMismatch, ProposalGovernanceCodes.UnauthorizedActor,
            ProposalGovernanceCodes.PortfolioNotAssigned, ProposalGovernanceCodes.EvidenceNotVisible,
            ProposalGovernanceCodes.PolicyRejected, ProposalGovernanceCodes.InsufficientCapital,
            ProposalGovernanceCodes.ConcurrencyConflict, ProposalGovernanceCodes.ResearchOnly,
            ProposalGovernanceCodes.Cancelled,
        }, Is.All.Matches<string>(value => value.StartsWith("proposal_governance.", StringComparison.Ordinal)));
    }

    private static GuardrailPolicyReference Policy(GuardrailPolicyLevel level) => new(level, $"{level}-policy", "v1");
}
