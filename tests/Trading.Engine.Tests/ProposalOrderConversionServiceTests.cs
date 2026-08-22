using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.Engine.Tests;

[TestFixture, Category("OrderConversion"), Category("ProposalOrderConversion")]
public sealed class ProposalOrderConversionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task DerivesStableClientIdentityAndMapsAtomicOutcomes()
    {
        var proposalId = TradeProposalId.New();
        var order = NewOrder(proposalId, ProposalOrderConversionService.DeriveClientOrderId(proposalId.ToString()));
        var repository = new StubRepository(new AtomicOrderConversionWriteResult.Created(order));
        var service = new ProposalOrderConversionService(repository, new Identifiers(order.Id));

        var result = await service.ConvertAsync(new(proposalId, CapitalReservationId.New(), Now), default);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new OrderConversionResult(
                OrderConversionOutcome.Created, OrderConversionCodes.Created, order)));
            Assert.That(repository.Request!.ClientOrderId,
                Is.EqualTo(ProposalOrderConversionService.DeriveClientOrderId(proposalId.ToString())));
            Assert.That(repository.Request.At, Is.EqualTo(Now));
        });
    }

    [Test]
    public void ClientIdentityIsDeterministicBoundedAndProposalSpecific()
    {
        var first = ProposalOrderConversionService.DeriveClientOrderId("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var retry = ProposalOrderConversionService.DeriveClientOrderId("01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var other = ProposalOrderConversionService.DeriveClientOrderId("01BX5ZZKBKACTAV9WEVGEMMVRZ");
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(retry));
            Assert.That(first, Is.Not.EqualTo(other));
            Assert.That(first.Value, Has.Length.LessThanOrEqualTo(100));
            Assert.That(first.Value, Does.StartWith("paper-"));
        });
    }

    [TestCaseSource(nameof(RejectedOutcomes))]
    public async Task PreservesStableRepositoryOutcomes(AtomicOrderConversionWriteResult write,
        OrderConversionOutcome outcome, string code)
    {
        var service = new ProposalOrderConversionService(new StubRepository(write), new Identifiers(OrderId.New()));
        var result = await service.ConvertAsync(new(TradeProposalId.New(), CapitalReservationId.New(), Now), default);
        Assert.That(result, Is.EqualTo(new OrderConversionResult(outcome, code, null)));
    }

    private static IEnumerable<TestCaseData> RejectedOutcomes()
    {
        yield return new(new AtomicOrderConversionWriteResult.Rejected(OrderConversionCodes.ApprovalRequired),
            OrderConversionOutcome.Rejected, OrderConversionCodes.ApprovalRequired);
        yield return new(new AtomicOrderConversionWriteResult.Rejected(OrderConversionCodes.ProposalExpired),
            OrderConversionOutcome.Rejected, OrderConversionCodes.ProposalExpired);
        yield return new(new AtomicOrderConversionWriteResult.Rejected(OrderConversionCodes.FreshValidationRequired),
            OrderConversionOutcome.Rejected, OrderConversionCodes.FreshValidationRequired);
        yield return new(new AtomicOrderConversionWriteResult.NotFound(), OrderConversionOutcome.NotFound,
            OrderConversionCodes.NotFound);
        yield return new(new AtomicOrderConversionWriteResult.Contention(), OrderConversionOutcome.Contention,
            OrderConversionCodes.Contention);
    }

    private static Order NewOrder(TradeProposalId proposalId, ClientOrderIdentity clientOrderId) => new(
        OrderId.New(), clientOrderId.Value, PortfolioId.New(), BrokerAccountId.New(), proposalId,
        InstrumentId.New(), OrderSide.Buy, new(1, "shares"), new("USD"), OrderType.Market,
        null, TimeInForce.Day, Now);

    private sealed class StubRepository(AtomicOrderConversionWriteResult result) : IAtomicOrderConversionRepository
    {
        public AtomicOrderConversionRequest? Request { get; private set; }
        public Task<AtomicOrderConversionWriteResult> TryConvertAsync(
            AtomicOrderConversionRequest request, CancellationToken token)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class Identifiers(OrderId orderId) : IOrderExecutionIdentifierSource
    {
        public OrderId NewOrderId() => orderId;
        public OrderTransitionId NewTransitionId() => OrderTransitionId.New();
        public FillId NewFillId() => FillId.New();
        public OrderWorkItemId NewWorkItemId() => OrderWorkItemId.New();
        public BrokerMessageId NewBrokerMessageId() => BrokerMessageId.New();
        public CorrelationIdentity NewCorrelationId() => new("conversion-correlation");
    }
}
