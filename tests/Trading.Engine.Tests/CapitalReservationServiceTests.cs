using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;

namespace Trading.Engine.Tests;

[TestFixture, Category("CapitalReservation")]
public sealed class CapitalReservationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ExactLimitBuyAmountIsReservedWithCurrencyAndBindings()
    {
        var proposal = ApprovedProposal();
        var writer = new CapturingWriter(new AtomicCapitalReservationWriteResult.Reserved(
            Reservation(proposal, 250)));
        var result = await new CapitalReservationService(new ProposalRepository(proposal), writer, new ReservationRepository())
            .ReserveAsync(Command(proposal, 250), default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(CapitalReservationOutcome.Reserved));
            Assert.That(writer.Request!.TradingBotId, Is.EqualTo(proposal.TradingBotId));
            Assert.That(writer.Request.Reservation.Amount, Is.EqualTo(new Money(250, Currency.USD)));
            Assert.That(writer.Request.ValidatedState.ContentHash, Is.EqualTo(Hash('s')));
        });
    }

    [TestCase(249, "capital_reservation.amount_mismatch")]
    [TestCase(0, "capital_reservation.invalid_amount")]
    public async Task InvalidAmountIsRejectedBeforePersistence(decimal amount, string code)
    {
        var proposal = ApprovedProposal(); var writer = new CapturingWriter(null);
        var result = await new CapitalReservationService(new ProposalRepository(proposal), writer, new ReservationRepository())
            .ReserveAsync(Command(proposal, amount), default);
        Assert.Multiple(() => { Assert.That(result.Code, Is.EqualTo(code)); Assert.That(writer.Request, Is.Null); });
    }

    [Test]
    public async Task RepositoryOutcomesRemainStableAndDoNotImplyExecution()
    {
        var proposal = ApprovedProposal(); var reservation = Reservation(proposal, 250);
        var cases = new (AtomicCapitalReservationWriteResult Write, CapitalReservationOutcome Outcome, string Code)[]
        {
            (new AtomicCapitalReservationWriteResult.AlreadyReserved(reservation), CapitalReservationOutcome.AlreadyReserved, "capital_reservation.already_reserved"),
            (new AtomicCapitalReservationWriteResult.Rejected(ProposalGovernanceCodes.InsufficientCapital), CapitalReservationOutcome.Rejected, ProposalGovernanceCodes.InsufficientCapital),
            (new AtomicCapitalReservationWriteResult.Contention(), CapitalReservationOutcome.ConcurrencyConflict, ProposalGovernanceCodes.ConcurrencyConflict),
        };
        foreach (var item in cases)
        {
            var result = await new CapitalReservationService(new ProposalRepository(proposal), new CapturingWriter(item.Write), new ReservationRepository())
                .ReserveAsync(Command(proposal, 250), default);
            Assert.That((result.Outcome, result.Code), Is.EqualTo((item.Outcome, item.Code)));
        }
    }

    private static CapitalReservationCommand Command(TradeProposal proposal, decimal amount) => new(
        CapitalReservationId.New(), proposal.Id, proposal.PortfolioId, proposal.ContentVersion,
        new FreshStateReference(proposal.PortfolioSnapshotId, Now, Hash('s')),
        new Money(amount, Currency.USD), new Money(1000, Currency.USD), Now.AddMinutes(3), Now.AddMinutes(20));

    private static CapitalReservation Reservation(TradeProposal proposal, decimal amount) =>
        new(CapitalReservationId.New(), proposal, new Money(amount, Currency.USD), Now.AddMinutes(3), Now.AddMinutes(20));

    private static TradeProposal ApprovedProposal()
    {
        var proposal = new TradeProposal(TradeProposalId.New(), TradingBotId.New(), BotRunId.New(), PortfolioId.New(),
            TradingBotConfigurationVersionId.New(), PortfolioDecisionSnapshotId.New(), InstrumentId.New(),
            new DirectTradeAction(TradeSide.Buy, new Quantity(2, "shares"), ProposedOrderType.Limit,
                new Price(125, Currency.USD), ProposedTimeInForce.Day), "rationale", new ProposalContentVersion(1, Hash('p')),
            null, [], Now, Now.AddHours(1));
        proposal.StartValidation(Now.AddMinutes(1));
        proposal.RequireHumanApproval(Now.AddMinutes(1));
        proposal.Approve(ProposalApprovalId.New(), new DecisionActor(ApprovalActorType.User, "operator"), null,
            Now.AddMinutes(2), proposal.ContentVersion,
            new FreshStateReference(proposal.PortfolioSnapshotId, Now, Hash('s')));
        return proposal;
    }

    private static string Hash(char value) => new(value, 64);

    private sealed class ProposalRepository(TradeProposal proposal) : ITradeProposalRepository
    {
        public Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken token) => Task.FromResult<TradeProposal?>(id == proposal.Id ? proposal : null);
        public Task<ProposalRecordResult> RecordAsync(TradeProposal value, string key, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(TradeProposal value, long version, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class CapturingWriter(AtomicCapitalReservationWriteResult? result) : IAtomicCapitalReservationRepository
    {
        public AtomicCapitalReservationRequest? Request { get; private set; }
        public Task<AtomicCapitalReservationWriteResult> TryReserveAsync(AtomicCapitalReservationRequest request, CancellationToken token)
        {
            Request = request;
            return Task.FromResult(result ?? throw new AssertionException("Writer should not have been called."));
        }
    }

    private sealed class ReservationRepository : ICapitalReservationRepository
    {
        public Task<CapitalReservation?> GetAsync(CapitalReservationId id, CancellationToken token) => Task.FromResult<CapitalReservation?>(null);
        public Task<CapitalReservation?> GetActiveAsync(TradeProposalId id, CancellationToken token) => Task.FromResult<CapitalReservation?>(null);
        public Task<IReadOnlyList<CapitalReservation>> GetActiveForPortfolioAsync(PortfolioId id, DateTimeOffset at, CancellationToken token) => Task.FromResult<IReadOnlyList<CapitalReservation>>([]);
        public Task<PersistenceWriteResult> AddAsync(CapitalReservation value, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(CapitalReservation value, long version, CancellationToken token) => throw new NotSupportedException();
        public Task<int> ExpireAsync(PortfolioId id, DateTimeOffset at, CancellationToken token) => Task.FromResult(0);
    }
}
