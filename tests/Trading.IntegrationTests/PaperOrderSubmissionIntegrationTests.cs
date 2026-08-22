using Trading.Brokers.Simulation;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.IntegrationTests;

[TestFixture, Category("PaperSubmission")]
public sealed class PaperOrderSubmissionIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 15, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task DurableDuplicateDispatchAgainstSimulatedBrokerCreatesOneBrokerOrder()
    {
        var account = BrokerAccountId.New(); var connection = BrokerConnectionId.New();
        var broker = new SimulatedPaperBroker(connection, account, "paper-fixture", new Clock(), new BrokerIds(), new Latency());
        var prepared = new PreparedOrderSubmission(OrderWorkItemId.New(), OrderId.New(), account, connection,
            "paper-fixture", new("paper-correlation"), new(new("paper-order-alpha-v1"), InstrumentId.New(), "AAPL",
                OrderSide.Buy, new Quantity(1, "shares"), Currency.USD, OrderType.Market, null, TimeInForce.Day),
            new string('a', 64), "Simulated", "worker", 0);
        var store = new Store(new PrepareOrderSubmissionResult.Ready(prepared));
        var dispatcher = new PaperOrderSubmissionDispatcher(store, broker, new ExecutionClock(), new ExecutionIds(), PaperOrderSubmissionOptions.Default);
        var work = new OrderWorkEnvelope(prepared.WorkItemId, prepared.OrderId, OrderWorkKind.Submit,
            "submit:paper-order-alpha-v1", "{}", prepared.CorrelationId, 1, Now, Now);

        var first = await dispatcher.DispatchAsync(work, default);
        store.Prepared = new PrepareOrderSubmissionResult.AlreadyCompleted(BrokerExecutionCodes.Accepted);
        var duplicate = await dispatcher.DispatchAsync(work, default);
        var context = new PaperBrokerOperationContext(account, connection, new("paper-fixture"), prepared.CorrelationId, Now);

        Assert.Multiple(() =>
        {
            Assert.That(first.Disposition, Is.EqualTo(DurableBrokerDispatchDisposition.Finalized));
            Assert.That(duplicate.Disposition, Is.EqualTo(DurableBrokerDispatchDisposition.Completed));
            Assert.That(store.Completions, Is.EqualTo(1));
            Assert.That(broker.Snapshot(context), Has.Count.EqualTo(1));
            Assert.That(store.Last!.Submission.Request.ClientOrderId.Value, Is.EqualTo("paper-order-alpha-v1"));
        });
    }

    private sealed class Store(PrepareOrderSubmissionResult prepared) : IOrderSubmissionRepository
    {
        public PrepareOrderSubmissionResult Prepared { get; set; } = prepared; public int Completions { get; private set; }
        public CompleteOrderSubmissionCommand? Last { get; private set; }
        public Task<PrepareOrderSubmissionResult> PrepareAsync(OrderWorkEnvelope w, DateTimeOffset a, BrokerCapabilities c, CancellationToken t) => Task.FromResult(Prepared);
        public Task<PersistenceWriteResult> CompleteAsync(CompleteOrderSubmissionCommand c, CancellationToken t) { Completions++; Last = c; return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
    }
    private sealed class Clock : ISimulatedBrokerClock { public DateTimeOffset UtcNow => Now; }
    private sealed class ExecutionClock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Latency : ISimulatedBrokerLatency { public Task WaitAsync(string operation, CancellationToken token) => Task.CompletedTask; }
    private sealed class BrokerIds : ISimulatedBrokerIdentifierSource
    {
        private int value; public string NewBrokerOrderId() => $"broker-{++value}"; public BrokerMessageId NewMessageId() => BrokerMessageId.New(); public string NewExecutionId() => $"execution-{value}";
    }
    private sealed class ExecutionIds : IOrderExecutionIdentifierSource
    {
        public OrderId NewOrderId() => OrderId.New(); public OrderTransitionId NewTransitionId() => OrderTransitionId.New(); public FillId NewFillId() => FillId.New();
        public OrderWorkItemId NewWorkItemId() => OrderWorkItemId.New(); public BrokerMessageId NewBrokerMessageId() => BrokerMessageId.New(); public CorrelationIdentity NewCorrelationId() => new("correlation");
    }
}
