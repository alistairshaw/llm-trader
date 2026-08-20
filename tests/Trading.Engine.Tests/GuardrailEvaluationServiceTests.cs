using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;

namespace Trading.Engine.Tests;

[TestFixture, Category("GuardrailEvaluation")]
public sealed class GuardrailEvaluationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 22, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PassingEvaluationPersistsCompleteArtifactAndRepeatIsIdempotent()
    {
        var proposal = Proposal(); var repository = new Repository(proposal);
        var service = new GuardrailEvaluationService(repository, new DeterministicGuardrailPolicyEvaluator(),
            new Identifiers(), new Clock());
        var policies = Policies(); var state = State(); var fresh = Fresh(proposal);

        var first = await service.EvaluateAndPersistAsync(proposal.Id, References(policies), policies, state, fresh, default);
        var second = await service.EvaluateAndPersistAsync(proposal.Id, References(policies), policies, state, fresh, default);

        Assert.Multiple(() =>
        {
            Assert.That(first.Outcome, Is.EqualTo(GuardrailEvaluationPersistenceOutcome.Persisted));
            Assert.That(first.Proposal!.Status, Is.EqualTo(ProposalStatus.AwaitingHumanApproval));
            Assert.That(first.Evaluation!.Sequence, Is.EqualTo(1));
            Assert.That(first.Evaluation.EvaluatedPolicies, Has.Count.EqualTo(4));
            Assert.That(first.Evaluation.RuleResults, Has.Count.EqualTo(44));
            Assert.That(first.Evaluation.ContentHash,
                Is.EqualTo("34e2dd5a6a57e2343b46aff397817c604b96d75efa8dd4a5657c7e7f6881c41b"));
            Assert.That(second.Outcome, Is.EqualTo(GuardrailEvaluationPersistenceOutcome.AlreadyEvaluated));
            Assert.That(second.Evaluation!.Id, Is.EqualTo(first.Evaluation.Id));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task FailedEvaluationRejectsAndConcurrencyIsReportedWithoutClaimingPersistence()
    {
        var proposal = Proposal(); var repository = new Repository(proposal) { Conflict = true };
        var service = new GuardrailEvaluationService(repository, new DeterministicGuardrailPolicyEvaluator(),
            new Identifiers(), new Clock());
        var policies = Policies(); var state = State() with { IdentityAuthorized = false };

        var result = await service.EvaluateAndPersistAsync(proposal.Id, References(policies), policies, state,
            Fresh(proposal), default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(GuardrailEvaluationPersistenceOutcome.ConcurrencyConflict));
            Assert.That(result.Code, Is.EqualTo(ProposalGovernanceCodes.ConcurrencyConflict));
            Assert.That(result.Evaluation!.Outcome, Is.EqualTo(GuardrailOutcome.Failed));
            Assert.That(result.Proposal!.Status, Is.EqualTo(ProposalStatus.Rejected));
        });
    }

    private static TradeProposal Proposal() => new(TradeProposalId.Parse("01PPEEEEEEEEEEEEEEEEEEEEEE"),
        TradingBotId.Parse("01EEEEEEEEEEEEEEEEEEEEEEEE"), BotRunId.Parse("01BREEEEEEEEEEEEEEEEEEEEEE"),
        PortfolioId.Parse("01PFEEEEEEEEEEEEEEEEEEEEEE"),
        TradingBotConfigurationVersionId.Parse("01CFEEEEEEEEEEEEEEEEEEEEEE"),
        PortfolioDecisionSnapshotId.Parse("01PSEEEEEEEEEEEEEEEEEEEEEE"),
        InstrumentId.Parse("01MNEEEEEEEEEEEEEEEEEEEEEE"),
        new DirectTradeAction(TradeSide.Buy, new Quantity(1, "share"), ProposedOrderType.Market, null,
            ProposedTimeInForce.Day), "rationale", new ProposalContentVersion(1, new string('a', 64)), null, [],
        Now.AddMinutes(-1), Now.AddHours(1));
    private static FreshStateReference Fresh(TradeProposal proposal) =>
        new(proposal.PortfolioSnapshotId, Now, new string('b', 64));
    private static Money Usd(decimal amount) => new(amount, Currency.USD);
    private static GuardrailState State() => new(Now, true, true, Usd(100), Usd(1000),
        new Percentage(5), Usd(1000), Now, Usd(20000), true);
    private static HierarchicalGuardrailPolicySet Policies()
    {
        GuardrailPolicy Policy(GuardrailPolicyLevel level) => new(new(level, $"{level}-policy", "v1"), true,
            null, Usd(1000), new Percentage(20), Usd(10), TimeSpan.FromMinutes(5), Usd(10000), true);
        return new(Policy(GuardrailPolicyLevel.Platform), Policy(GuardrailPolicyLevel.Account),
            Policy(GuardrailPolicyLevel.Portfolio), Policy(GuardrailPolicyLevel.TradingBot));
    }
    private static GuardrailPolicySet References(HierarchicalGuardrailPolicySet value)
    {
        var p = value.InEvaluationOrder.Select(x => x.Reference).ToArray(); return new(p[0], p[1], p[2], p[3]);
    }
    private sealed class Clock : IProposalGovernanceClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Identifiers : IProposalGovernanceIdentifierSource
    {
        public TradeProposalId NewProposalId() => TradeProposalId.New();
        public GuardrailEvaluationId NewEvaluationId() => GuardrailEvaluationId.Parse("01GVEEEEEEEEEEEEEEEEEEEEEE");
        public ProposalApprovalId NewApprovalId() => ProposalApprovalId.New();
        public CapitalReservationId NewReservationId() => CapitalReservationId.New();
    }
    private sealed class Repository(TradeProposal proposal) : ITradeProposalRepository
    {
        public bool Conflict { get; init; }
        public int SaveCount { get; private set; }
        public Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken token) => Task.FromResult<TradeProposal?>(proposal);
        public Task<ProposalRecordResult> RecordAsync(TradeProposal value, string key, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(TradeProposal value, long expected, CancellationToken token)
        {
            SaveCount++;
            return Task.FromResult<PersistenceWriteResult>(Conflict
                ? new PersistenceWriteResult.ConcurrencyConflict(expected, expected + 1)
                : new PersistenceWriteResult.Succeeded());
        }
    }
}
