using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.IntegrationTests;

[TestFixture, Category("OrderConversion"), Category("ProposalOrderConversion"), Category("OrderConversionBoundary")]
public sealed class ProposalOrderConversionBoundaryTests
{
    [Test]
    public async Task ConversionDelegatesOnlyToAtomicPersistenceAndNeverRequiresABrokerGateway()
    {
        var repository = new CapturingRepository();
        var service = new ProposalOrderConversionService(repository, new Identifiers());
        var at = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

        var result = await service.ConvertAsync(new(TradeProposalId.New(), CapitalReservationId.New(), at), default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(OrderConversionOutcome.Rejected));
            Assert.That(result.Code, Is.EqualTo(AtomicOrderConversionCodes.ApprovalRequired));
            Assert.That(repository.Calls, Is.EqualTo(1));
            Assert.That(typeof(ProposalOrderConversionService).GetConstructors().Single().GetParameters()
                .Select(parameter => parameter.ParameterType), Has.None.EqualTo(typeof(IPaperBrokerGateway)));
        });
    }

    private sealed class CapturingRepository : IAtomicOrderConversionRepository
    {
        public int Calls { get; private set; }
        public Task<AtomicOrderConversionWriteResult> TryConvertAsync(
            AtomicOrderConversionRequest request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult<AtomicOrderConversionWriteResult>(new AtomicOrderConversionWriteResult.Rejected(
                AtomicOrderConversionCodes.ApprovalRequired));
        }
    }

    private sealed class Identifiers : IOrderExecutionIdentifierSource
    {
        public OrderId NewOrderId() => OrderId.New();
        public OrderTransitionId NewTransitionId() => OrderTransitionId.New();
        public FillId NewFillId() => FillId.New();
        public OrderWorkItemId NewWorkItemId() => OrderWorkItemId.New();
        public BrokerMessageId NewBrokerMessageId() => BrokerMessageId.New();
        public CorrelationIdentity NewCorrelationId() => new("conversion-boundary");
    }
}
