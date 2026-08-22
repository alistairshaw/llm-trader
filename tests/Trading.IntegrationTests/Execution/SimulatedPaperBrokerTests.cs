using Trading.Brokers.Simulation;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;

namespace Trading.IntegrationTests.Execution;

[TestFixture]
[Category("SimulatedBroker")]
[Category("BrokerContracts")]
public sealed class SimulatedPaperBrokerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly BrokerConnectionId ConnectionId = BrokerConnectionId.Parse("01J00000000000000000000001");
    private static readonly BrokerAccountId AccountId = BrokerAccountId.Parse("01J00000000000000000000002");
    private static readonly Currency Usd = Currency.USD;
    private static readonly string[] ExpectedBasicOperations = ["submit", "lookup", "reconcile"];

    [Test]
    public async Task CapabilitiesAndAcceptedSubmissionSatisfyTheCommonContract()
    {
        var fixture = Create();

        var submitted = await fixture.Broker.SubmitAsync(fixture.Context, Request("accepted"), CancellationToken.None);
        var found = await fixture.Broker.FindByClientOrderIdAsync(fixture.Context,
            new(new("accepted")), CancellationToken.None);
        var reconciled = await fixture.Broker.ReconcileAsync(fixture.Context,
            new(new("accepted")), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Broker.Capabilities, Is.EqualTo((BrokerCapabilities)63));
            Assert.That(submitted.Outcome, Is.EqualTo(BrokerSubmissionOutcome.Accepted));
            Assert.That(found.Outcome, Is.EqualTo(BrokerReconciliationOutcome.Found));
            Assert.That(reconciled.BrokerOrderId, Is.EqualTo(submitted.BrokerOrderId));
            Assert.That(fixture.Latency.Operations, Is.EqualTo(ExpectedBasicOperations));
        });
    }

    [Test]
    public async Task TimeoutAfterAcceptanceReconcilesWithoutCreatingAnotherBrokerOrder()
    {
        var fixture = Create();
        var identity = new ClientOrderIdentity("timeout");
        fixture.Broker.Configure(identity, new(SimulatedSubmissionBehavior.TimeoutAfterAcceptance, []));

        var first = await fixture.Broker.SubmitAsync(fixture.Context, Request(identity.Value), CancellationToken.None);
        var found = await fixture.Broker.FindByClientOrderIdAsync(fixture.Context, new(identity), CancellationToken.None);
        var retry = await fixture.Broker.SubmitAsync(fixture.Context, Request(identity.Value), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.Outcome, Is.EqualTo(BrokerSubmissionOutcome.Unknown));
            Assert.That(found.Outcome, Is.EqualTo(BrokerReconciliationOutcome.Found));
            Assert.That(retry.Outcome, Is.EqualTo(BrokerSubmissionOutcome.Duplicate));
            Assert.That(retry.BrokerOrderId, Is.EqualTo(found.BrokerOrderId));
            Assert.That(fixture.Ids.BrokerOrderCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ExactDuplicateIsIdempotentAndConflictingDuplicateIsTerminal()
    {
        var fixture = Create();
        var request = Request("duplicate");
        var first = await fixture.Broker.SubmitAsync(fixture.Context, request, CancellationToken.None);
        var duplicate = await fixture.Broker.SubmitAsync(fixture.Context, request, CancellationToken.None);
        var conflict = await fixture.Broker.SubmitAsync(fixture.Context,
            request with { Quantity = new Quantity(2m, "shares") }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Outcome, Is.EqualTo(BrokerSubmissionOutcome.Duplicate));
            Assert.That(duplicate.BrokerOrderId, Is.EqualTo(first.BrokerOrderId));
            Assert.That(conflict.Outcome, Is.EqualTo(BrokerSubmissionOutcome.TerminalFailure));
            Assert.That(fixture.Broker.Snapshot(fixture.Context), Has.Count.EqualTo(1));
        });
    }

    [TestCase(SimulatedSubmissionBehavior.Reject, BrokerSubmissionOutcome.Rejected)]
    [TestCase(SimulatedSubmissionBehavior.Unknown, BrokerSubmissionOutcome.Unknown)]
    public async Task RejectionAndUnknownWithoutAcceptanceDoNotCreateBrokerState(
        SimulatedSubmissionBehavior behavior, BrokerSubmissionOutcome outcome)
    {
        var fixture = Create();
        var identity = new ClientOrderIdentity(behavior.ToString());
        fixture.Broker.Configure(identity, new(behavior, []));

        var result = await fixture.Broker.SubmitAsync(fixture.Context, Request(identity.Value), CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(outcome));
        Assert.That(fixture.Broker.Snapshot(fixture.Context), Is.Empty);
    }

    [Test]
    public async Task ScriptEmitsPartialFinalOutOfOrderAndDuplicateEventsDeterministically()
    {
        var fixture = Create();
        var identity = new ClientOrderIdentity("fills");
        fixture.Broker.Configure(identity, SimulatedOrderScript.Accepted(
            new(BrokerOrderEventKind.Execution, "broker.execution", Execution(1m)),
            new(BrokerOrderEventKind.Acknowledged, "broker.acknowledged"),
            new(BrokerOrderEventKind.Execution, "broker.execution", null, DuplicateOf: 0),
            new(BrokerOrderEventKind.Execution, "broker.execution", Execution(2m))));
        await fixture.Broker.SubmitAsync(fixture.Context, Request(identity.Value, 3m), CancellationToken.None);

        var events = await fixture.Broker.ReadEventsAsync(fixture.Context, CancellationToken.None);
        var state = await fixture.Broker.ReconcileAsync(fixture.Context, new(identity), CancellationToken.None);
        var subsequent = await fixture.Broker.ReadEventsAsync(fixture.Context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(events, Has.Count.EqualTo(4));
            Assert.That(events[2], Is.SameAs(events[0]));
            Assert.That(events.Select(item => item.MessageId.ToString()).Distinct().ToArray(), Has.Length.EqualTo(3));
            Assert.That(events.Where(item => item.Execution is not null).Select(item => item.Execution!.ExecutionId).Distinct().ToArray(), Has.Length.EqualTo(2));
            Assert.That(state.Status, Is.EqualTo(OrderStatus.Filled));
            Assert.That(subsequent, Is.Empty);
        });
    }

    [TestCase(BrokerOrderEventKind.Rejected, OrderStatus.Rejected)]
    [TestCase(BrokerOrderEventKind.Expired, OrderStatus.Expired)]
    public async Task TerminalEventScriptsAreReconciled(BrokerOrderEventKind kind, OrderStatus expected)
    {
        var fixture = Create();
        var identity = new ClientOrderIdentity(kind.ToString());
        fixture.Broker.Configure(identity, SimulatedOrderScript.Accepted(
            new SimulatedEventScript(kind, $"broker.{kind.ToString().ToLowerInvariant()}")));
        await fixture.Broker.SubmitAsync(fixture.Context, Request(identity.Value), CancellationToken.None);
        await fixture.Broker.ReadEventsAsync(fixture.Context, CancellationToken.None);

        var result = await fixture.Broker.ReconcileAsync(fixture.Context, new(identity), CancellationToken.None);
        Assert.That(result.Status, Is.EqualTo(expected));
    }

    [Test]
    public async Task CancellationEmitsOneTerminalEventAndRepeatIsAlreadyTerminal()
    {
        var fixture = Create();
        var request = Request("cancel");
        var submitted = await fixture.Broker.SubmitAsync(fixture.Context, request, CancellationToken.None);
        var command = new BrokerCancellationRequest(request.ClientOrderId, submitted.BrokerOrderId!);

        var cancelled = await fixture.Broker.CancelAsync(fixture.Context, command, CancellationToken.None);
        var repeated = await fixture.Broker.CancelAsync(fixture.Context, command, CancellationToken.None);
        var events = await fixture.Broker.ReadEventsAsync(fixture.Context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(cancelled.Outcome, Is.EqualTo(BrokerCancellationOutcome.Accepted));
            Assert.That(repeated.Outcome, Is.EqualTo(BrokerCancellationOutcome.AlreadyTerminal));
            Assert.That(events.Single().Kind, Is.EqualTo(BrokerOrderEventKind.Cancelled));
        });
    }

    [TestCase(BrokerCancellationOutcome.Rejected)]
    [TestCase(BrokerCancellationOutcome.Unknown)]
    [TestCase(BrokerCancellationOutcome.RetryableFailure)]
    [TestCase(BrokerCancellationOutcome.TerminalFailure)]
    public async Task CancellationOutcomesAreScriptedWithoutMutatingBrokerState(BrokerCancellationOutcome outcome)
    {
        var fixture = Create();
        var identity = new ClientOrderIdentity($"cancel-{outcome}");
        fixture.Broker.Configure(identity, new(SimulatedSubmissionBehavior.Accept, [], outcome));
        var request = Request(identity.Value);
        var submitted = await fixture.Broker.SubmitAsync(fixture.Context, request, CancellationToken.None);

        var result = await fixture.Broker.CancelAsync(fixture.Context,
            new(identity, submitted.BrokerOrderId!), CancellationToken.None);
        var reconciled = await fixture.Broker.ReconcileAsync(fixture.Context, new(identity), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(outcome));
            Assert.That(reconciled.Status, Is.EqualTo(OrderStatus.Submitted));
        });
    }

    [Test]
    public void MismatchedPaperIdentityIsRejectedBeforeStateOrLatencyChanges()
    {
        var fixture = Create();
        var wrong = fixture.Context with { Environment = new BrokerOperationEnvironment.Paper("other") };

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Broker.SubmitAsync(wrong, Request("wrong"), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(error!.Message, Is.EqualTo("broker.paper_environment_mismatch"));
            Assert.That(fixture.Broker.Snapshot(fixture.Context), Is.Empty);
            Assert.That(fixture.Latency.Operations, Is.Empty);
        });
    }

    [Test]
    public void CancellationIsPropagatedThroughTheDeterministicLatencySeam()
    {
        var fixture = Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Broker.SubmitAsync(fixture.Context, Request("cancelled"), cancellation.Token));
        Assert.That(fixture.Broker.Snapshot(fixture.Context), Is.Empty);
    }

    private static SimulatedExecutionScript Execution(decimal quantity) =>
        new(new Quantity(quantity, "shares"), new Price(10m, Usd), new Money(0.01m, Usd));

    private static BrokerOrderRequest Request(string identity, decimal quantity = 1m) => new(
        new(identity), InstrumentId.Parse("01J00000000000000000000003"), "instrument-1",
        OrderSide.Buy, new Quantity(quantity, "shares"), Usd, OrderType.Market, null, TimeInForce.Day);

    private static Fixture Create()
    {
        var clock = new Clock();
        var ids = new Ids();
        var latency = new Latency();
        var broker = new SimulatedPaperBroker(ConnectionId, AccountId, "paper-fixture", clock, ids, latency);
        var context = new PaperBrokerOperationContext(AccountId, ConnectionId,
            new("paper-fixture"), new("correlation-1"), Now);
        return new(broker, context, ids, latency);
    }

    private sealed record Fixture(SimulatedPaperBroker Broker, PaperBrokerOperationContext Context, Ids Ids, Latency Latency);
    private sealed class Clock : ISimulatedBrokerClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Latency : ISimulatedBrokerLatency
    {
        public List<string> Operations { get; } = [];
        public Task WaitAsync(string operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add(operation);
            return Task.CompletedTask;
        }
    }

    private sealed class Ids : ISimulatedBrokerIdentifierSource
    {
        private int brokerOrder;
        private int message;
        private int execution;
        public int BrokerOrderCount => brokerOrder;
        public string NewBrokerOrderId() => $"paper-order-{++brokerOrder:D4}";
        public BrokerMessageId NewMessageId() => BrokerMessageId.Parse($"01J0000000000{++message:D13}");
        public string NewExecutionId() => $"paper-execution-{++execution:D4}";
    }
}
