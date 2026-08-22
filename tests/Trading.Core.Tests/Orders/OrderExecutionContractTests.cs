using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Core.Tests.Orders;

[Category("OrderExecution")]
public sealed class OrderExecutionContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExecutionIds = ["execution-1", "execution-2"];

    [Test]
    public void PartialFillsMaintainExactCumulativeQuantityGrossAndFees()
    {
        var order = NewAcknowledgedOrder();
        Apply(order, "execution-1", 2.125m, 10.20m, 0.11m, 4);
        Apply(order, "execution-2", 7.875m, 10.25m, 0.22m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Filled));
            Assert.That(order.FilledQuantity, Is.EqualTo(10.000m));
            Assert.That(order.CumulativeGrossAmount, Is.EqualTo(102.39375m));
            Assert.That(order.CumulativeFeeAmount, Is.EqualTo(0.33m));
            Assert.That(order.Fills.Select(x => x.BrokerExecutionId), Is.EqualTo(ExecutionIds));
        });
    }

    [Test]
    public void BrokerContractsAreBoundedCanonicalAndUtc()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new ClientOrderIdentity("order with spaces"), Throws.ArgumentException);
            Assert.That(() => new BrokerSubmissionResult(BrokerSubmissionOutcome.Accepted, "Not Canonical", "broker", Now), Throws.ArgumentException);
            Assert.That(() => new BrokerSubmissionResult(BrokerSubmissionOutcome.Accepted, BrokerExecutionCodes.Accepted,
                new string('x', 201), Now), Throws.ArgumentException);
            Assert.That(() => new BrokerSubmissionResult(BrokerSubmissionOutcome.Accepted, BrokerExecutionCodes.Accepted,
                "broker", Now.ToOffset(TimeSpan.FromHours(1))), Throws.ArgumentException);
            Assert.That(() => new OrderWorkEnvelope(OrderWorkItemId.New(), OrderId.New(), OrderWorkKind.Submit,
                "key", new string('x', 16_385), new CorrelationIdentity("correlation"), 0, Now, Now), Throws.ArgumentException);
        });
    }

    [Test]
    public void PaperAndLiveEnvironmentIdentitiesAreStructurallyDistinct()
    {
        var paper = new BrokerOperationEnvironment.Paper("simulated-paper");
        var live = new BrokerOperationEnvironment.Live("production-live");
        var context = new PaperBrokerOperationContext(BrokerAccountId.New(), BrokerConnectionId.New(), paper,
            new CorrelationIdentity("correlation-1"), Now);

        Assert.Multiple(() =>
        {
            Assert.That(paper, Is.Not.InstanceOf<BrokerOperationEnvironment.Live>());
            Assert.That(live, Is.Not.InstanceOf<BrokerOperationEnvironment.Paper>());
            Assert.That(context.Environment, Is.SameAs(paper));
            Assert.That(typeof(PaperBrokerOperationContext).GetConstructors().Single().GetParameters()[2].ParameterType,
                Is.EqualTo(typeof(BrokerOperationEnvironment.Paper)));
        });
    }

    [Test]
    public void AtomicConversionRequestPinsEveryMaterialIdentityAndUsesCanonicalCodes()
    {
        var proposal = TradeProposalId.New();
        var reservation = CapitalReservationId.New();
        var request = new AtomicOrderConversionRequest(proposal, reservation, OrderId.New(),
            OrderWorkItemId.New(), new CorrelationIdentity("conversion-1"),
            new ClientOrderIdentity("paper-stable-1"), Now);
        Assert.Multiple(() =>
        {
            Assert.That(request.ProposalId, Is.EqualTo(proposal));
            Assert.That(request.ReservationId, Is.EqualTo(reservation));
            Assert.That(request.At, Is.EqualTo(Now));
            Assert.That(AtomicOrderConversionCodes.ApprovalRequired,
                Is.EqualTo("order_execution.approval_required"));
            Assert.That(AtomicOrderConversionCodes.ProposalExpired,
                Is.EqualTo("order_execution.proposal_expired"));
            Assert.That(AtomicOrderConversionCodes.FreshValidationRequired,
                Is.EqualTo("order_execution.fresh_validation_required"));
            Assert.That(AtomicOrderConversionCodes.EnvironmentMismatch,
                Is.EqualTo("order_conversion.environment_mismatch"));
        });
    }

    [TestCase(BrokerSubmissionOutcome.Accepted, BrokerExecutionCodes.Accepted)]
    [TestCase(BrokerSubmissionOutcome.Rejected, BrokerExecutionCodes.Rejected)]
    [TestCase(BrokerSubmissionOutcome.Unknown, BrokerExecutionCodes.Unknown)]
    [TestCase(BrokerSubmissionOutcome.RetryableFailure, BrokerExecutionCodes.Retryable)]
    [TestCase(BrokerSubmissionOutcome.TerminalFailure, BrokerExecutionCodes.Terminal)]
    [TestCase(BrokerSubmissionOutcome.Duplicate, BrokerExecutionCodes.Duplicate)]
    public void SubmissionOutcomesHaveStableCodes(BrokerSubmissionOutcome outcome, string code)
    {
        var result = new BrokerSubmissionResult(outcome, code, outcome == BrokerSubmissionOutcome.Accepted ? "broker-1" : null, Now);
        Assert.That(result.Code, Is.EqualTo(code));
    }

    private static Order NewAcknowledgedOrder()
    {
        var order = new Order(OrderId.New(), "client-1", PortfolioId.New(), BrokerAccountId.New(),
            TradeProposalId.New(), InstrumentId.New(), OrderSide.Buy, new Quantity(10, "shares"), Currency.USD,
            OrderType.Limit, new Price(11, Currency.USD), TimeInForce.Day, Now);
        order.BeginSubmission(OrderTransitionId.New(), Now.AddMinutes(1));
        order.MarkSubmitted(OrderTransitionId.New(), Now.AddMinutes(2));
        order.Acknowledge(OrderTransitionId.New(), "broker-1", Now.AddMinutes(3));
        return order;
    }

    private static void Apply(Order order, string executionId, decimal quantity, decimal price, decimal fee, int minute) =>
        order.ApplyFill(FillId.New(), OrderTransitionId.New(), executionId, new Quantity(quantity, "shares"),
            new Price(price, Currency.USD), new Money(fee, Currency.USD), Now.AddMinutes(minute), Now.AddMinutes(minute));
}
