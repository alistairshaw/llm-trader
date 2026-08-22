using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.Engine.Tests;

[TestFixture, Category("SubmissionReconciliation")]
public sealed class PaperOrderReconciliationDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);

    [TestCase(BrokerReconciliationOutcome.Found, OrderStatus.Acknowledged, OrderReconciliationCodes.Found, DurableBrokerDispatchDisposition.Finalized)]
    [TestCase(BrokerReconciliationOutcome.Uncertain, null, OrderReconciliationCodes.Uncertain, DurableBrokerDispatchDisposition.Retryable)]
    [TestCase(BrokerReconciliationOutcome.RetryableFailure, null, OrderReconciliationCodes.Unavailable, DurableBrokerDispatchDisposition.Retryable)]
    public async Task NormalizesBrokerKnowledge(BrokerReconciliationOutcome outcome, OrderStatus? status,
        string code, DurableBrokerDispatchDisposition disposition)
    {
        var store = new Store(Ready(1));
        var broker = new Gateway(new(outcome, BrokerExecutionCodes.ReconciliationUncertain,
            outcome == BrokerReconciliationOutcome.Found ? "broker-one" : null, status, Now));
        var result = await Dispatcher(store, broker).DispatchAsync(Work(1), default);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new DurableBrokerDispatchResult(disposition, code)));
            Assert.That(broker.Lookups, Is.EqualTo(1)); Assert.That(broker.Submissions, Is.Zero);
        });
    }

    [Test]
    public async Task AbsenceRequiresGraceAndRepeatedLookupBeforeSchedulingSameIdentity()
    {
        var absent = new BrokerReconciliationResult(BrokerReconciliationOutcome.Absent,
            BrokerExecutionCodes.ReconciledAbsent, null, null, Now);
        var earlyStore = new Store(Ready(1, Now.AddSeconds(-1)));
        var early = await Dispatcher(earlyStore, new Gateway(absent)).DispatchAsync(Work(1), default);
        var confirmedStore = new Store(Ready(2, Now.AddMinutes(-1)));
        var confirmed = await Dispatcher(confirmedStore, new Gateway(absent)).DispatchAsync(Work(2), default);
        Assert.Multiple(() =>
        {
            Assert.That(early.Code, Is.EqualTo(OrderReconciliationCodes.AbsentPending));
            Assert.That(earlyStore.Completions, Is.Zero); Assert.That(confirmed.Code, Is.EqualTo(OrderReconciliationCodes.AbsenceConfirmed));
            Assert.That(confirmedStore.Last!.Reconciliation.ClientOrderId.Value, Is.EqualTo("stable-client"));
        });
    }

    [Test]
    public async Task FoundWithoutIdentityIsTerminalMismatchAndCallerCancellationPropagates()
    {
        var store = new Store(Ready(1));
        var mismatch = await Dispatcher(store, new Gateway(new(BrokerReconciliationOutcome.Found,
            BrokerExecutionCodes.ReconciledFound, null, OrderStatus.Submitted, Now))).DispatchAsync(Work(1), default);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Multiple(() =>
        {
            Assert.That(mismatch.Code, Is.EqualTo(OrderReconciliationCodes.IdentityMismatch));
            Assert.That(async () => await Dispatcher(new Store(Ready(1)), new Gateway(null)).DispatchAsync(Work(1), cancelled.Token),
                Throws.InstanceOf<OperationCanceledException>());
        });
    }

    private static PaperOrderReconciliationDispatcher Dispatcher(Store store, Gateway gateway) => new(store,
        gateway, new Clock(), new Ids(), new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), 2, 5));
    private static PrepareOrderReconciliationResult.Ready Ready(int attempt, DateTimeOffset? unknown = null) => new(new(
        OrderWorkItemId.New(), OrderId.New(), BrokerAccountId.New(), BrokerConnectionId.New(), "paper-fixture",
        new("stable-client"), new("correlation"), "worker", 2, attempt, unknown ?? Now.AddMinutes(-1)));
    private static OrderWorkEnvelope Work(int attempt) => new(OrderWorkItemId.New(), OrderId.New(),
        OrderWorkKind.Reconcile, "reconcile:stable-client", "{}", new("correlation"), attempt, Now, Now);
    private sealed class Store(PrepareOrderReconciliationResult prepared) : IOrderReconciliationRepository
    {
        public int Completions { get; private set; }
        public CompleteOrderReconciliationCommand? Last { get; private set; }
        public Task<PrepareOrderReconciliationResult> PrepareAsync(OrderWorkEnvelope w, BrokerCapabilities c, CancellationToken t) => Task.FromResult(prepared);
        public Task<PersistenceWriteResult> CompleteAsync(CompleteOrderReconciliationCommand c, CancellationToken t)
        { Completions++; Last = c; return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
    }
    private sealed class Gateway(BrokerReconciliationResult? result) : IPaperBrokerGateway
    {
        public BrokerCapabilities Capabilities => BrokerCapabilities.LookupByClientOrderId; public int Lookups { get; private set; }
        public int Submissions { get; private set; }
        public Task<BrokerSubmissionResult> SubmitAsync(PaperBrokerOperationContext c, BrokerOrderRequest r, CancellationToken t) { Submissions++; throw new AssertionException("Reconciliation cannot submit directly."); }
        public Task<BrokerReconciliationResult> FindByClientOrderIdAsync(PaperBrokerOperationContext c, BrokerOrderLookup l, CancellationToken t)
        { Lookups++; t.ThrowIfCancellationRequested(); return Task.FromResult(result!); }
        public Task<BrokerReconciliationResult> ReconcileAsync(PaperBrokerOperationContext c, BrokerOrderLookup l, CancellationToken t) => throw new NotSupportedException();
        public Task<BrokerCancellationResult> CancelAsync(PaperBrokerOperationContext c, BrokerCancellationRequest r, CancellationToken t) => throw new NotSupportedException();
    }
    private sealed class Clock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IOrderExecutionIdentifierSource
    {
        public OrderId NewOrderId() => OrderId.New(); public OrderTransitionId NewTransitionId() => OrderTransitionId.New(); public FillId NewFillId() => FillId.New();
        public OrderWorkItemId NewWorkItemId() => OrderWorkItemId.New(); public BrokerMessageId NewBrokerMessageId() => BrokerMessageId.New(); public CorrelationIdentity NewCorrelationId() => new("new");
    }
}
