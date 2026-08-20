using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;

namespace Trading.Engine.Tests;

[TestFixture, Category("ResearchOnlyProposal")]
public sealed class ResearchOnlyProposalGovernanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PassingGuardrailsPersistStructuredResearchOnlyDisposition()
    {
        var proposal = Proposal(); var repository = new Repository(proposal);
        var service = new GuardrailEvaluationService(repository, new DeterministicGuardrailPolicyEvaluator(),
            new Ids(), new Clock());
        var definitions = Policies();
        var references = definitions.InEvaluationOrder.Select(x => x.Reference).ToArray();
        var result = await service.EvaluateAndPersistAsync(proposal.Id,
            new(references[0], references[1], references[2], references[3]), definitions,
            new(Now, true, true, new Money(10, Currency.USD), new Money(1000, Currency.USD),
                new Percentage(1), new Money(100, Currency.USD), Now, new Money(10000, Currency.USD), true),
            new(proposal.PortfolioSnapshotId, Now, Hash('s')), default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(GuardrailEvaluationPersistenceOutcome.Persisted));
            Assert.That(result.Code, Is.EqualTo(ProposalGovernanceCodes.ResearchOnly));
            Assert.That(result.Proposal!.Status, Is.EqualTo(ProposalStatus.Rejected));
            Assert.That(result.Evaluation!.Outcome, Is.EqualTo(GuardrailOutcome.Passed));
            Assert.That(result.Evaluation.DiagnosticCode, Is.EqualTo(ProposalGovernanceCodes.ResearchOnly));
            Assert.That(result.Evaluation.RuleResults, Is.Not.Empty);
        });
    }

    [Test]
    public async Task ApprovalAndReservationBypassesReturnStableResearchOnlyCodeWithoutWrites()
    {
        var proposal = Proposal(); var repository = new Repository(proposal);
        var decision = await new HumanProposalDecisionService(repository, new AllowAll(), new Ids(), new Clock())
            .DecideAsync(new(proposal.Id, proposal.ContentVersion, proposal.ConfigurationVersionId,
                new(proposal.PortfolioSnapshotId, Now, Hash('s')), GuardrailEvaluationId.New(), Hash('e'),
                ApprovalDecision.Approved, null, new(ApprovalActorType.User, "operator"),
                new HashSet<string> { "proposal.approve" }), default);
        var writer = new ReservationWriter();
        var reservation = await new CapitalReservationService(repository, writer, new ReservationStore())
            .ReserveAsync(new(CapitalReservationId.New(), proposal.Id, proposal.PortfolioId,
                proposal.ContentVersion, new(proposal.PortfolioSnapshotId, Now, Hash('s')),
                new Money(100, Currency.USD), new Money(1000, Currency.USD), Now, Now.AddMinutes(10)), default);

        Assert.Multiple(() =>
        {
            Assert.That((decision.Outcome, decision.Code),
                Is.EqualTo((HumanProposalDecisionOutcome.Unauthorized, ProposalGovernanceCodes.ResearchOnly)));
            Assert.That((reservation.Outcome, reservation.Code),
                Is.EqualTo((CapitalReservationOutcome.Rejected, ProposalGovernanceCodes.ResearchOnly)));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(writer.Called, Is.False);
            Assert.That(proposal.Status, Is.EqualTo(ProposalStatus.Recorded));
        });
    }

    private static TradeProposal Proposal() => new(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(),
        PortfolioId.New(), TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(),
        InstrumentId.New(), new DirectTradeAction(TradeSide.Buy, new Quantity(1, "share"),
            ProposedOrderType.Limit, new Price(10, Currency.USD), ProposedTimeInForce.Day), "research",
        new(1, Hash('p')), null, [], Now.AddMinutes(-1), Now.AddHours(1), ExecutionMode.ResearchOnly);

    private static HierarchicalGuardrailPolicySet Policies()
    {
        GuardrailPolicy P(GuardrailPolicyLevel level) => new(new(level, level.ToString(), "v1"), true, null,
            new Money(1000, Currency.USD), new Percentage(20), new Money(1, Currency.USD),
            TimeSpan.FromMinutes(5), new Money(10000, Currency.USD), true);
        return new(P(GuardrailPolicyLevel.Platform), P(GuardrailPolicyLevel.Account),
            P(GuardrailPolicyLevel.Portfolio), P(GuardrailPolicyLevel.TradingBot));
    }

    private static string Hash(char value) => new(value, 64);
    private sealed class Clock : IProposalGovernanceClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IProposalGovernanceIdentifierSource
    {
        public TradeProposalId NewProposalId() => TradeProposalId.New();
        public GuardrailEvaluationId NewEvaluationId() => GuardrailEvaluationId.New();
        public ProposalApprovalId NewApprovalId() => ProposalApprovalId.New();
        public CapitalReservationId NewReservationId() => CapitalReservationId.New();
    }
    private sealed class AllowAll : IProposalDecisionAuthorizer
    {
        public Task<ProposalDecisionAuthorizationResult> AuthorizeAsync(ProposalDecisionAuthorizationRequest request,
            CancellationToken token) => Task.FromResult(new ProposalDecisionAuthorizationResult(true, "allowed"));
    }
    private sealed class Repository(TradeProposal proposal) : ITradeProposalRepository
    {
        public int SaveCount { get; private set; }
        public Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken token) =>
            Task.FromResult<TradeProposal?>(proposal);
        public Task<ProposalRecordResult> RecordAsync(TradeProposal value, string key, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(TradeProposal value, long expected, CancellationToken token)
        { SaveCount++; return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
    }
    private sealed class ReservationWriter : IAtomicCapitalReservationRepository
    {
        public bool Called { get; private set; }
        public Task<AtomicCapitalReservationWriteResult> TryReserveAsync(AtomicCapitalReservationRequest request,
            CancellationToken token)
        { Called = true; throw new AssertionException("ResearchOnly cannot reserve."); }
    }
    private sealed class ReservationStore : ICapitalReservationRepository
    {
        public Task<CapitalReservation?> GetAsync(CapitalReservationId id, CancellationToken token) => Task.FromResult<CapitalReservation?>(null);
        public Task<CapitalReservation?> GetActiveAsync(TradeProposalId id, CancellationToken token) => Task.FromResult<CapitalReservation?>(null);
        public Task<IReadOnlyList<CapitalReservation>> GetActiveForPortfolioAsync(PortfolioId id, DateTimeOffset at, CancellationToken token) => Task.FromResult<IReadOnlyList<CapitalReservation>>([]);
        public Task<PersistenceWriteResult> AddAsync(CapitalReservation value, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(CapitalReservation value, long expected, CancellationToken token) => throw new NotSupportedException();
        public Task<int> ExpireAsync(PortfolioId id, DateTimeOffset at, CancellationToken token) => Task.FromResult(0);
    }
}
