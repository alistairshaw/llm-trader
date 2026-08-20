using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;

namespace Trading.IntegrationTests;

[TestFixture, Category("ResearchOnlyProposal")]
public sealed class ResearchOnlyProposalWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AttemptedApprovalAndReservationCannotReachOrderOrBrokerSubmission()
    {
        var proposal = new TradeProposal(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(),
            TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(),
            new DirectTradeAction(TradeSide.Buy, new Quantity(1, "share"), ProposedOrderType.Limit,
                new Price(100, Currency.USD), ProposedTimeInForce.Day), "research", new(1, Hash('p')), null, [],
            Now.AddMinutes(-1), Now.AddHours(1), ExecutionMode.ResearchOnly);
        var repository = new ProposalRepository(proposal);
        var reservationWriter = new ReservationWriter();
        var decision = await new HumanProposalDecisionService(repository, new Authorizer(), new Ids(), new Clock())
            .DecideAsync(new(proposal.Id, proposal.ContentVersion, proposal.ConfigurationVersionId,
                new(proposal.PortfolioSnapshotId, Now, Hash('s')), GuardrailEvaluationId.New(), Hash('e'),
                ApprovalDecision.Approved, null, new(ApprovalActorType.User, "operator"),
                new HashSet<string> { "proposal.approve" }), default);
        var reservation = await new CapitalReservationService(repository, reservationWriter, new Reservations())
            .ReserveAsync(new(CapitalReservationId.New(), proposal.Id, proposal.PortfolioId, proposal.ContentVersion,
                new(proposal.PortfolioSnapshotId, Now, Hash('s')), new(100, Currency.USD),
                new(1000, Currency.USD), Now, Now.AddMinutes(10)), default);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Code, Is.EqualTo(ProposalGovernanceCodes.ResearchOnly));
            Assert.That(reservation.Code, Is.EqualTo(ProposalGovernanceCodes.ResearchOnly));
            Assert.That(reservationWriter.Calls, Is.Zero);
            Assert.That(() => proposal.ConvertToOrder(Now), Throws.InvalidOperationException);
            Assert.That(proposal.ApprovalHistory, Is.Empty);
        });
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
    private sealed class Authorizer : IProposalDecisionAuthorizer
    {
        public Task<ProposalDecisionAuthorizationResult> AuthorizeAsync(ProposalDecisionAuthorizationRequest request,
            CancellationToken token) => Task.FromResult(new ProposalDecisionAuthorizationResult(true, "allowed"));
    }
    private sealed class ProposalRepository(TradeProposal proposal) : ITradeProposalRepository
    {
        public Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken token) => Task.FromResult<TradeProposal?>(proposal);
        public Task<ProposalRecordResult> RecordAsync(TradeProposal value, string key, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(TradeProposal value, long expected, CancellationToken token) => throw new AssertionException("ResearchOnly decision cannot mutate proposal.");
    }
    private sealed class ReservationWriter : IAtomicCapitalReservationRepository
    {
        public int Calls { get; private set; }
        public Task<AtomicCapitalReservationWriteResult> TryReserveAsync(AtomicCapitalReservationRequest request, CancellationToken token)
        { Calls++; throw new AssertionException("ResearchOnly cannot reach reservation persistence."); }
    }
    private sealed class Reservations : ICapitalReservationRepository
    {
        public Task<CapitalReservation?> GetAsync(CapitalReservationId id, CancellationToken token) => Task.FromResult<CapitalReservation?>(null);
        public Task<CapitalReservation?> GetActiveAsync(TradeProposalId id, CancellationToken token) => Task.FromResult<CapitalReservation?>(null);
        public Task<IReadOnlyList<CapitalReservation>> GetActiveForPortfolioAsync(PortfolioId id, DateTimeOffset at, CancellationToken token) => Task.FromResult<IReadOnlyList<CapitalReservation>>([]);
        public Task<PersistenceWriteResult> AddAsync(CapitalReservation value, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(CapitalReservation value, long expected, CancellationToken token) => throw new NotSupportedException();
        public Task<int> ExpireAsync(PortfolioId id, DateTimeOffset at, CancellationToken token) => Task.FromResult(0);
    }
}
