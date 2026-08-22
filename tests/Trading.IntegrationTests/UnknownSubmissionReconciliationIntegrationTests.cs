using Trading.Brokers.Simulation;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.IntegrationTests;

[TestFixture, Category("UnknownSubmission")]
public sealed class UnknownSubmissionReconciliationIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 17, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task TimeoutAfterAcceptanceIsFoundByStableClientIdentityWithoutASecondSubmission()
    {
        var account = BrokerAccountId.New(); var connection = BrokerConnectionId.New();
        var broker = new SimulatedPaperBroker(connection, account, "paper-fixture", new BrokerClock(), new BrokerIds(), new Latency());
        broker.Configure(new("stable-timeout"), new(SimulatedSubmissionBehavior.TimeoutAfterAcceptance, []));
        var context = new PaperBrokerOperationContext(account, connection, new("paper-fixture"), new("correlation"), Now);
        var request = new BrokerOrderRequest(new("stable-timeout"), InstrumentId.New(), "AAPL", OrderSide.Buy,
            new Quantity(1, "shares"), Currency.USD, OrderType.Market, null, TimeInForce.Day);
        var unknown = await broker.SubmitAsync(context, request, default);
        var store = new Store(new PrepareOrderReconciliationResult.Ready(new(OrderWorkItemId.New(), OrderId.New(), account, connection,
            "paper-fixture", request.ClientOrderId, context.CorrelationId, "worker", 2, 1, Now.AddMinutes(-1))));
        var dispatcher = new PaperOrderReconciliationDispatcher(store, broker, new ExecutionClock(), new ExecutionIds(),
            PaperOrderReconciliationOptions.Default);

        var reconciled = await dispatcher.DispatchAsync(new(OrderWorkItemId.New(), OrderId.New(),
            OrderWorkKind.Reconcile, "reconcile:stable-timeout", "{}", context.CorrelationId, 1, Now, Now), default);

        Assert.Multiple(() =>
        {
            Assert.That(unknown.Outcome, Is.EqualTo(BrokerSubmissionOutcome.Unknown));
            Assert.That(reconciled.Code, Is.EqualTo(OrderReconciliationCodes.Found));
            Assert.That(store.Last!.Result.BrokerOrderId, Is.Not.Null); Assert.That(broker.Snapshot(context), Has.Count.EqualTo(1));
        });
    }

    private sealed class Store(PrepareOrderReconciliationResult prepared) : IOrderReconciliationRepository
    {
        public CompleteOrderReconciliationCommand? Last { get; private set; }
        public Task<PrepareOrderReconciliationResult> PrepareAsync(OrderWorkEnvelope work, BrokerCapabilities gatewayCapabilities, CancellationToken token) => Task.FromResult(prepared);
        public Task<PersistenceWriteResult> CompleteAsync(CompleteOrderReconciliationCommand command, CancellationToken token)
        { Last = command; return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
    }
    private sealed class BrokerClock : ISimulatedBrokerClock { public DateTimeOffset UtcNow => Now; }
    private sealed class ExecutionClock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Latency : ISimulatedBrokerLatency { public Task WaitAsync(string operation, CancellationToken token) => Task.CompletedTask; }
    private sealed class BrokerIds : ISimulatedBrokerIdentifierSource
    { private int value; public string NewBrokerOrderId() => $"broker-{++value}"; public BrokerMessageId NewMessageId() => BrokerMessageId.New(); public string NewExecutionId() => $"execution-{value}"; }
    private sealed class ExecutionIds : IOrderExecutionIdentifierSource
    {
        public OrderId NewOrderId() => OrderId.New(); public OrderTransitionId NewTransitionId() => OrderTransitionId.New(); public FillId NewFillId() => FillId.New();
        public OrderWorkItemId NewWorkItemId() => OrderWorkItemId.New(); public BrokerMessageId NewBrokerMessageId() => BrokerMessageId.New(); public CorrelationIdentity NewCorrelationId() => new("new");
    }
}
