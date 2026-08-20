using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;

namespace Trading.Engine.Tests;

[TestFixture, Category("ProposalOrchestration")]
public sealed class ProposalGovernanceOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 16, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ValidProposalAwaitsDecisionThenRevalidatesFreshStateAndReserves()
    {
        var fixture = new Fixture(Proposal());
        var initial = await fixture.Service.ValidateAsync(fixture.Proposal.Id, default);
        var reviewed = initial.Evaluation!;

        fixture.Fresh.Advance();
        var result = await fixture.Service.DecideAndReserveAsync(Command(fixture.Proposal, reviewed),
            TimeSpan.FromMinutes(15), default);

        Assert.Multiple(() =>
        {
            Assert.That(initial.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.AwaitingHumanApproval));
            Assert.That(result.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.Reserved));
            Assert.That(result.Proposal!.Status, Is.EqualTo(ProposalStatus.Approved));
            Assert.That(result.Proposal.GuardrailEvaluations, Has.Count.EqualTo(2));
            Assert.That(result.Reservation!.Amount, Is.EqualTo(Usd(20)));
            Assert.That(result.Evaluation!.FreshState, Is.EqualTo(fixture.Fresh.Current.Reference));
            Assert.That(fixture.Reservations.Writes, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RetryReusesDecisionEvaluationAndReservation()
    {
        var fixture = new Fixture(Proposal());
        var initial = await fixture.Service.ValidateAsync(fixture.Proposal.Id, default);
        fixture.Fresh.Advance();
        var command = Command(fixture.Proposal, initial.Evaluation!);
        var first = await fixture.Service.DecideAndReserveAsync(command, TimeSpan.FromMinutes(15), default);
        var second = await fixture.Service.DecideAndReserveAsync(command, TimeSpan.FromMinutes(15), default);

        Assert.Multiple(() =>
        {
            Assert.That(first.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.Reserved));
            Assert.That(second.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.AlreadyCompleted));
            Assert.That(second.Reservation!.Id, Is.EqualTo(first.Reservation!.Id));
            Assert.That(fixture.Proposal.ApprovalHistory, Has.Count.EqualTo(1));
            Assert.That(fixture.Proposal.GuardrailEvaluations, Has.Count.EqualTo(2));
            Assert.That(fixture.Reservations.Writes, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ChangedFreshStateRejectsApprovedProposalWithoutReservation()
    {
        var fixture = new Fixture(Proposal());
        var initial = await fixture.Service.ValidateAsync(fixture.Proposal.Id, default);
        fixture.Fresh.Advance();
        fixture.Context.Reject = true;

        var result = await fixture.Service.DecideAndReserveAsync(Command(fixture.Proposal, initial.Evaluation!),
            TimeSpan.FromMinutes(15), default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.Rejected));
            Assert.That(result.Proposal!.Status, Is.EqualTo(ProposalStatus.Rejected));
            Assert.That(result.Proposal.ApprovalHistory, Has.Count.EqualTo(1));
            Assert.That(fixture.Reservations.Writes, Is.Zero);
        });
    }

    [Test]
    public async Task RejectionIsTerminalAndResearchOnlyNeverReachesReservation()
    {
        var human = new Fixture(Proposal());
        var initial = await human.Service.ValidateAsync(human.Proposal.Id, default);
        var rejected = await human.Service.DecideAndReserveAsync(
            Command(human.Proposal, initial.Evaluation!) with { Decision = ApprovalDecision.Rejected, Reason = "No" },
            TimeSpan.FromMinutes(15), default);
        var research = new Fixture(Proposal(ExecutionMode.ResearchOnly));
        var researchResult = await research.Service.ValidateAsync(research.Proposal.Id, default);

        Assert.Multiple(() =>
        {
            Assert.That(rejected.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.Rejected));
            Assert.That(rejected.Proposal!.Status, Is.EqualTo(ProposalStatus.Rejected));
            Assert.That(researchResult.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.ResearchOnly));
            Assert.That(research.Reservations.Writes, Is.Zero);
        });
    }

    [Test]
    public async Task ExpirationIsRecoverableAndIdempotent()
    {
        var fixture = new Fixture(Proposal(validUntil: Now.AddMinutes(-1)));
        var first = await fixture.Service.ExpireAsync(fixture.Proposal.Id, default);
        var second = await fixture.Service.ExpireAsync(fixture.Proposal.Id, default);

        Assert.Multiple(() =>
        {
            Assert.That(first.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.Expired));
            Assert.That(second.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.AlreadyCompleted));
            Assert.That(fixture.Reservations.ExpireCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task StateFailureIsBoundedAndUnrelatedProposalCanContinue()
    {
        var failed = new Fixture(Proposal()) { Fresh = { Fail = true } };
        var healthy = new Fixture(Proposal());
        var failure = await failed.Service.ValidateAsync(failed.Proposal.Id, default);
        var success = await healthy.Service.ValidateAsync(healthy.Proposal.Id, default);

        Assert.Multiple(() =>
        {
            Assert.That((failure.Outcome, failure.Code), Is.EqualTo((ProposalOrchestrationOutcome.Failed,
                "proposal_orchestration.state_unavailable")));
            Assert.That(success.Outcome, Is.EqualTo(ProposalOrchestrationOutcome.AwaitingHumanApproval));
        });
    }

    private static HumanProposalDecisionCommand Command(TradeProposal proposal, GuardrailEvaluation evaluation) =>
        new(proposal.Id, proposal.ContentVersion, proposal.ConfigurationVersionId, evaluation.FreshState!, evaluation.Id,
            evaluation.ContentHash!, ApprovalDecision.Approved, "Reviewed", new(ApprovalActorType.User, "operator"),
            new HashSet<string> { "proposal.approve" });

    private static TradeProposal Proposal(ExecutionMode mode = ExecutionMode.HumanApproval,
        DateTimeOffset? validUntil = null) => new(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(),
        PortfolioId.New(), TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(),
        InstrumentId.New(), new DirectTradeAction(TradeSide.Buy, new Quantity(2, "share"),
            ProposedOrderType.Limit, new Price(10, Currency.USD), ProposedTimeInForce.Day), "rationale",
        new(1, new string('a', 64)), null, [], Now.AddMinutes(-5), validUntil ?? Now.AddHours(1), mode);
    private static Money Usd(decimal amount) => new(amount, Currency.USD);

    private sealed class Fixture
    {
        public Fixture(TradeProposal proposal)
        {
            Proposal = proposal;
            Repository = new Repository(proposal);
            Fresh = new FreshProvider(proposal);
            Context = new ContextProvider();
            Reservations = new ReservationPort();
            var ids = new Ids(); var clock = new Clock();
            Service = new ProposalGovernanceOrchestrator(Repository, Fresh, Context,
                new GuardrailEvaluationService(Repository, new DeterministicGuardrailPolicyEvaluator(), ids, clock),
                new HumanProposalDecisionService(Repository, new Authorizer(), ids, clock),
                new CapitalReservationService(Repository, Reservations, Reservations), ids, clock);
        }
        public TradeProposal Proposal { get; }
        public Repository Repository { get; }
        public FreshProvider Fresh { get; init; }
        public ContextProvider Context { get; }
        public ReservationPort Reservations { get; }
        public ProposalGovernanceOrchestrator Service { get; }
    }

    private sealed class Clock : IProposalGovernanceClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IProposalGovernanceIdentifierSource
    {
        public TradeProposalId NewProposalId() => TradeProposalId.New();
        public GuardrailEvaluationId NewEvaluationId() => GuardrailEvaluationId.New();
        public ProposalApprovalId NewApprovalId() => ProposalApprovalId.New();
        public CapitalReservationId NewReservationId() => CapitalReservationId.New();
    }
    private sealed class Authorizer : IProposalDecisionAuthorizer
    {
        public Task<ProposalDecisionAuthorizationResult> AuthorizeAsync(ProposalDecisionAuthorizationRequest request,
            CancellationToken token) => Task.FromResult(new ProposalDecisionAuthorizationResult(
                request.Roles.Contains("proposal.approve"), "proposal_decision.unauthorized"));
    }
    private sealed class FreshProvider(TradeProposal proposal) : IFreshProposalStateProvider
    {
        public bool Fail { get; set; }
        public FreshProposalState Current { get; private set; } = State(proposal.PortfolioSnapshotId, 'b');
        public void Advance() => Current = State(PortfolioDecisionSnapshotId.New(), 'c');
        public Task<FreshProposalState> AcquireAsync(TradeProposal value, CancellationToken token) => Fail
            ? throw new InvalidOperationException("synthetic secret diagnostic")
            : Task.FromResult(Current);
        private static FreshProposalState State(PortfolioDecisionSnapshotId id, char hash) =>
            new(new(id, Now, new string(hash, 64)), Usd(1000), Usd(0), Now);
    }
    private sealed class ContextProvider : IProposalGovernanceContextProvider
    {
        public bool Reject { get; set; }
        public Task<ProposalGovernanceEvaluationContext> GetAsync(TradeProposal proposal,
            FreshProposalState fresh, CancellationToken token)
        {
            GuardrailPolicy P(GuardrailPolicyLevel level) => new(new(level, $"{level}-policy", "v1"), true,
                null, Usd(1000), new Percentage(20), Usd(1), TimeSpan.FromMinutes(5), Usd(10000), true);
            var definitions = new HierarchicalGuardrailPolicySet(P(GuardrailPolicyLevel.Platform),
                P(GuardrailPolicyLevel.Account), P(GuardrailPolicyLevel.Portfolio), P(GuardrailPolicyLevel.TradingBot));
            var refs = definitions.InEvaluationOrder.Select(x => x.Reference).ToArray();
            var state = new GuardrailState(Now, !Reject, true, Usd(20), Usd(1000), new Percentage(2),
                Usd(100), Now, Usd(10000), true);
            return Task.FromResult(new ProposalGovernanceEvaluationContext(
                new(refs[0], refs[1], refs[2], refs[3]), definitions, state));
        }
    }
    private sealed class Repository(TradeProposal proposal) : ITradeProposalRepository
    {
        public Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken token) =>
            Task.FromResult(id == proposal.Id ? proposal : null);
        public Task<ProposalRecordResult> RecordAsync(TradeProposal value, string key, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(TradeProposal value, long expected, CancellationToken token) =>
            Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded());
    }
    private sealed class ReservationPort : IAtomicCapitalReservationRepository, ICapitalReservationRepository
    {
        private CapitalReservation? reservation;
        public int Writes { get; private set; }
        public int ExpireCalls { get; private set; }
        public Task<AtomicCapitalReservationWriteResult> TryReserveAsync(AtomicCapitalReservationRequest request,
            CancellationToken token)
        {
            Writes++;
            if (reservation is not null)
                return Task.FromResult<AtomicCapitalReservationWriteResult>(
                    new AtomicCapitalReservationWriteResult.AlreadyReserved(reservation));
            reservation = request.Reservation;
            return Task.FromResult<AtomicCapitalReservationWriteResult>(
                new AtomicCapitalReservationWriteResult.Reserved(reservation));
        }
        public Task<CapitalReservation?> GetAsync(CapitalReservationId id, CancellationToken token) =>
            Task.FromResult(reservation?.Id == id ? reservation : null);
        public Task<CapitalReservation?> GetActiveAsync(TradeProposalId id, CancellationToken token) =>
            Task.FromResult(reservation?.TradeProposalId == id ? reservation : null);
        public Task<IReadOnlyList<CapitalReservation>> GetActiveForPortfolioAsync(PortfolioId id, DateTimeOffset at,
            CancellationToken token) => Task.FromResult<IReadOnlyList<CapitalReservation>>(
                reservation?.PortfolioId == id ? [reservation] : []);
        public Task<PersistenceWriteResult> AddAsync(CapitalReservation value, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(CapitalReservation value, long expected, CancellationToken token) =>
            Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded());
        public Task<int> ExpireAsync(PortfolioId id, DateTimeOffset at, CancellationToken token)
        { ExpireCalls++; return Task.FromResult(0); }
    }
}
