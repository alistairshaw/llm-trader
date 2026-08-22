using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.Engine.Tests;

[TestFixture, Category("OrderSubmission")]
public sealed class PaperOrderSubmissionDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);
    private static readonly string[] StableClientIds = ["paper-order-stable"];

    [TestCase(BrokerSubmissionOutcome.Accepted, DurableBrokerDispatchDisposition.Finalized)]
    [TestCase(BrokerSubmissionOutcome.Rejected, DurableBrokerDispatchDisposition.Finalized)]
    [TestCase(BrokerSubmissionOutcome.Unknown, DurableBrokerDispatchDisposition.Finalized)]
    [TestCase(BrokerSubmissionOutcome.TerminalFailure, DurableBrokerDispatchDisposition.Finalized)]
    [TestCase(BrokerSubmissionOutcome.RetryableFailure, DurableBrokerDispatchDisposition.Retryable)]
    public async Task MapsNormalizedOutcomesAndPersistsOnlyTerminalKnowledge(
        BrokerSubmissionOutcome outcome, DurableBrokerDispatchDisposition expected)
    {
        var store = new Store(Ready());
        var gateway = new Gateway((_, _, _) => Task.FromResult(Result(outcome)));
        var result = await Dispatcher(store, gateway).DispatchAsync(Work(), default);

        Assert.Multiple(() =>
        {
            Assert.That(result.Disposition, Is.EqualTo(expected));
            Assert.That(gateway.Calls, Is.EqualTo(1));
            Assert.That(store.Completions, Is.EqualTo(expected == DurableBrokerDispatchDisposition.Finalized ? 1 : 0));
        });
    }

    [Test]
    public async Task DurableRetryUsesTheOriginalStableCommandAndDuplicateCannotCreateAnotherOrder()
    {
        var store = new Store(Ready());
        var gateway = new Gateway((_, request, _) => Task.FromResult(new BrokerSubmissionResult(
            BrokerSubmissionOutcome.Duplicate, BrokerExecutionCodes.Duplicate, "broker-stable", Now)));
        var dispatcher = Dispatcher(store, gateway);

        var first = await dispatcher.DispatchAsync(Work(), default);
        store.Prepared = new PrepareOrderSubmissionResult.AlreadyCompleted(BrokerExecutionCodes.Duplicate);
        var retry = await dispatcher.DispatchAsync(Work(), default);

        Assert.Multiple(() =>
        {
            Assert.That(first.Disposition, Is.EqualTo(DurableBrokerDispatchDisposition.Finalized));
            Assert.That(retry.Disposition, Is.EqualTo(DurableBrokerDispatchDisposition.Completed));
            Assert.That(gateway.Calls, Is.EqualTo(1));
            Assert.That(gateway.ClientIds, Is.EqualTo(StableClientIds));
        });
    }

    [TestCase(typeof(PrepareOrderSubmissionResult.Rejected), DurableBrokerDispatchDisposition.Terminal)]
    [TestCase(typeof(PrepareOrderSubmissionResult.Contention), DurableBrokerDispatchDisposition.Retryable)]
    public async Task PreflightFailureNeverCallsBroker(Type resultType, DurableBrokerDispatchDisposition expected)
    {
        PrepareOrderSubmissionResult prepared = resultType == typeof(PrepareOrderSubmissionResult.Rejected)
            ? new PrepareOrderSubmissionResult.Rejected(OrderSubmissionCodes.AccountRestricted)
            : new PrepareOrderSubmissionResult.Contention();
        var store = new Store(prepared); var gateway = new Gateway((_, _, _) => throw new AssertionException("Broker must not be called."));

        var result = await Dispatcher(store, gateway).DispatchAsync(Work(), default);

        Assert.That(result.Disposition, Is.EqualTo(expected));
        Assert.That(gateway.Calls, Is.Zero);
    }

    [Test]
    public async Task TimeoutBecomesUnknownWhileCallerCancellationRemainsCancellation()
    {
        var timeoutStore = new Store(Ready());
        var slow = new Gateway(async (_, _, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return Result(BrokerSubmissionOutcome.Accepted); });
        var timed = await Dispatcher(timeoutStore, slow, TimeSpan.FromMilliseconds(10)).DispatchAsync(Work(), default);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();

        Assert.Multiple(() =>
        {
            Assert.That(timed.Disposition, Is.EqualTo(DurableBrokerDispatchDisposition.Finalized));
            Assert.That(timeoutStore.LastCompletion!.Result.Outcome, Is.EqualTo(BrokerSubmissionOutcome.Unknown));
            Assert.That(async () => await Dispatcher(new Store(Ready()), slow).DispatchAsync(Work(), cancelled.Token), Throws.InstanceOf<OperationCanceledException>());
        });
    }

    [Test]
    public async Task TransportExceptionIsUnknownAndCannotCauseAnImmediateResubmit()
    {
        var store = new Store(Ready());
        var result = await Dispatcher(store, new Gateway((_, _, _) => throw new IOException("secret provider detail")))
            .DispatchAsync(Work(), default);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new DurableBrokerDispatchResult(DurableBrokerDispatchDisposition.Finalized, BrokerExecutionCodes.Unknown)));
            Assert.That(store.Completions, Is.EqualTo(1));
            Assert.That(store.LastCompletion!.Result.Outcome, Is.EqualTo(BrokerSubmissionOutcome.Unknown));
        });
    }

    private static PaperOrderSubmissionDispatcher Dispatcher(Store store, Gateway gateway, TimeSpan? timeout = null) =>
        new(store, gateway, new Clock(), new Ids(), new(timeout ?? TimeSpan.FromSeconds(1)));
    private static PrepareOrderSubmissionResult.Ready Ready() => new(new(
        OrderWorkItemId.New(), OrderId.New(), BrokerAccountId.New(), BrokerConnectionId.New(), "paper-fixture",
        new("correlation"), new(new("paper-order-stable"), InstrumentId.New(), "AAPL", OrderSide.Buy,
            new Quantity(2, "shares"), Currency.USD, OrderType.Limit, new Price(100, Currency.USD), TimeInForce.Day),
        new string('a', 64), "Simulated", "worker", 0));
    private static OrderWorkEnvelope Work() => new(OrderWorkItemId.New(), OrderId.New(), OrderWorkKind.Submit,
        "submit:paper-order-stable", "{}", new("correlation"), 1, Now, Now);
    private static BrokerSubmissionResult Result(BrokerSubmissionOutcome outcome) => new(outcome,
        outcome switch { BrokerSubmissionOutcome.Accepted => BrokerExecutionCodes.Accepted, BrokerSubmissionOutcome.Rejected => BrokerExecutionCodes.Rejected, BrokerSubmissionOutcome.Unknown => BrokerExecutionCodes.Unknown, BrokerSubmissionOutcome.RetryableFailure => BrokerExecutionCodes.Retryable, BrokerSubmissionOutcome.TerminalFailure => BrokerExecutionCodes.Terminal, _ => BrokerExecutionCodes.Duplicate },
        outcome is BrokerSubmissionOutcome.Accepted or BrokerSubmissionOutcome.Duplicate ? "broker-stable" : null, Now);

    private sealed class Store(PrepareOrderSubmissionResult prepared) : IOrderSubmissionRepository
    {
        public PrepareOrderSubmissionResult Prepared { get; set; } = prepared; public int Completions { get; private set; }
        public CompleteOrderSubmissionCommand? LastCompletion { get; private set; }
        public Task<PrepareOrderSubmissionResult> PrepareAsync(OrderWorkEnvelope work, DateTimeOffset at, BrokerCapabilities capabilities, CancellationToken token) => Task.FromResult(Prepared);
        public Task<PersistenceWriteResult> CompleteAsync(CompleteOrderSubmissionCommand command, CancellationToken token) { Completions++; LastCompletion = command; return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
    }
    private sealed class Gateway(Func<PaperBrokerOperationContext, BrokerOrderRequest, CancellationToken, Task<BrokerSubmissionResult>> submit) : IPaperBrokerGateway
    {
        public BrokerCapabilities Capabilities => BrokerCapabilities.SubmitLimitOrders; public int Calls { get; private set; }
        public List<string> ClientIds { get; } = [];
        public Task<BrokerSubmissionResult> SubmitAsync(PaperBrokerOperationContext context, BrokerOrderRequest request, CancellationToken token) { Calls++; ClientIds.Add(request.ClientOrderId.Value); return submit(context, request, token); }
        public Task<BrokerReconciliationResult> FindByClientOrderIdAsync(PaperBrokerOperationContext c, BrokerOrderLookup l, CancellationToken t) => throw new NotSupportedException();
        public Task<BrokerReconciliationResult> ReconcileAsync(PaperBrokerOperationContext c, BrokerOrderLookup l, CancellationToken t) => throw new NotSupportedException();
        public Task<BrokerCancellationResult> CancelAsync(PaperBrokerOperationContext c, BrokerCancellationRequest r, CancellationToken t) => throw new NotSupportedException();
    }
    private sealed class Clock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IOrderExecutionIdentifierSource
    {
        public OrderId NewOrderId() => OrderId.New(); public OrderTransitionId NewTransitionId() => OrderTransitionId.New(); public FillId NewFillId() => FillId.New();
        public OrderWorkItemId NewWorkItemId() => OrderWorkItemId.New(); public BrokerMessageId NewBrokerMessageId() => BrokerMessageId.New(); public CorrelationIdentity NewCorrelationId() => new("new-correlation");
    }
}
