using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;

namespace Trading.Core.Tests.Orders;

[Category("OrderAggregate")]
public sealed class OrderAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly HashSet<(OrderStatus From, OrderStatus To)> AllowedTransitions =
    [
        (OrderStatus.Created, OrderStatus.Submitting),
        (OrderStatus.Submitting, OrderStatus.Submitted),
        (OrderStatus.Submitting, OrderStatus.Unknown),
        (OrderStatus.Submitted, OrderStatus.Acknowledged),
        (OrderStatus.Submitted, OrderStatus.Rejected),
        (OrderStatus.Submitted, OrderStatus.Expired),
        (OrderStatus.Acknowledged, OrderStatus.PartiallyFilled),
        (OrderStatus.Acknowledged, OrderStatus.Filled),
        (OrderStatus.Acknowledged, OrderStatus.CancelPending),
        (OrderStatus.PartiallyFilled, OrderStatus.PartiallyFilled),
        (OrderStatus.PartiallyFilled, OrderStatus.Filled),
        (OrderStatus.PartiallyFilled, OrderStatus.CancelPending),
        (OrderStatus.CancelPending, OrderStatus.PartiallyFilled),
        (OrderStatus.CancelPending, OrderStatus.Filled),
        (OrderStatus.CancelPending, OrderStatus.Cancelled),
        (OrderStatus.Unknown, OrderStatus.Submitted),
        (OrderStatus.Unknown, OrderStatus.Acknowledged),
        (OrderStatus.Unknown, OrderStatus.Cancelled),
        (OrderStatus.Unknown, OrderStatus.Rejected),
        (OrderStatus.Unknown, OrderStatus.Expired),
    ];

    public static IEnumerable<TestCaseData> EveryStatePair()
    {
        foreach (var from in Enum.GetValues<OrderStatus>())
            foreach (var to in Enum.GetValues<OrderStatus>())
                yield return new TestCaseData(from, to, AllowedTransitions.Contains((from, to)))
                    .SetName($"Transition_{from}_to_{to}_is_{(AllowedTransitions.Contains((from, to)) ? "allowed" : "forbidden")}");
    }

    [TestCaseSource(nameof(EveryStatePair))]
    public void EveryAllowedAndForbiddenStateTransitionIsTableDriven(OrderStatus from, OrderStatus to, bool allowed)
    {
        var order = OrderIn(from);
        void Act() => TransitionTo(order, to);
        if (allowed) Assert.That(Act, Throws.Nothing); else Assert.That(Act, Throws.InvalidOperationException);
    }

    [Test]
    public void OrderTypeAndPriceCombinationsAreEnforced()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => NewOrder(OrderType.Market, new Price(10, Currency.USD)), Throws.ArgumentException);
            Assert.That(() => new Order(OrderId.New(), "client", PortfolioId.New(), BrokerAccountId.New(),
                TradeProposalId.New(), InstrumentId.New(), OrderSide.Buy, new Quantity(10, "shares"),
                Currency.USD, OrderType.Limit, null, TimeInForce.Day, Now), Throws.ArgumentException);
            Assert.That(() => NewOrder(OrderType.Limit, new Price(0, Currency.USD)), Throws.ArgumentException);
            Assert.That(() => NewOrder(OrderType.Market, null), Throws.Nothing);
            Assert.That(() => NewOrder(OrderType.Limit, new Price(10, Currency.USD)), Throws.Nothing);
        });
    }

    [Test]
    public void FillQuantityCannotExceedOrderAndUnitsAndCurrenciesMustMatch()
    {
        var order = OrderIn(OrderStatus.Acknowledged);
        Assert.Multiple(() =>
        {
            Assert.That(() => ApplyFill(order, 11, "shares", Currency.USD, "too-many"), Throws.InvalidOperationException);
            Assert.That(() => ApplyFill(order, 1, "units", Currency.USD, "wrong-unit"), Throws.InvalidOperationException);
            Assert.That(() => ApplyFill(order, 1, "shares", Currency.EUR, "wrong-currency"), Throws.InvalidOperationException);
        });
        ApplyFill(order, 6, "shares", Currency.USD, "execution-1");
        Assert.That(() => ApplyFill(order, 5, "shares", Currency.USD, "execution-2"), Throws.InvalidOperationException);
    }

    [Test]
    public void DuplicateBrokerExecutionIsIgnoredWithoutChangingStateOrVersion()
    {
        var order = OrderIn(OrderStatus.Acknowledged);
        Assert.That(ApplyFill(order, 4, "shares", Currency.USD, "execution-1"), Is.True);
        var version = order.Version;
        Assert.Multiple(() =>
        {
            Assert.That(ApplyFill(order, 4, "shares", Currency.USD, " execution-1 "), Is.False);
            Assert.That(order.Fills, Has.Count.EqualTo(1));
            Assert.That(order.FilledQuantity, Is.EqualTo(4));
            Assert.That(order.Version, Is.EqualTo(version));
        });
    }

    [Test]
    public void UnknownOutcomeRequiresReconciliationAndCannotBeRetried()
    {
        var order = OrderIn(OrderStatus.Unknown);
        Assert.Multiple(() =>
        {
            Assert.That(order.RequiresReconciliation, Is.True);
            Assert.That(() => order.BeginSubmission(OrderTransitionId.New(), Now.AddHours(1)), Throws.InvalidOperationException);
            Assert.That(() => order.MarkSubmitted(OrderTransitionId.New(), Now.AddHours(1)), Throws.InvalidOperationException);
        });
        order.Reconcile(OrderTransitionId.New(), OrderStatus.Acknowledged, "found at broker", Now.AddHours(1), "broker-1");
        Assert.Multiple(() =>
        {
            Assert.That(order.RequiresReconciliation, Is.False);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Acknowledged));
            Assert.That(order.BrokerOrderId, Is.EqualTo("broker-1"));
        });
    }

    [Test]
    public void TransitionAndFillHistoryAreImmutableChronologicalFacts()
    {
        var order = OrderIn(OrderStatus.Acknowledged);
        ApplyFill(order, 4, "shares", Currency.USD, "execution-1");
        Assert.Multiple(() =>
        {
            Assert.That(order.Transitions.Select(x => x.Sequence), Is.EqualTo(Enumerable.Range(1, order.Transitions.Count)));
            Assert.That(order.Transitions.Select(x => x.OccurredAt), Is.Ordered);
            Assert.That(typeof(OrderTransition).GetProperties().All(property => property.SetMethod is null), Is.True);
            Assert.That(typeof(Fill).GetProperties().All(property => property.SetMethod is null), Is.True);
            Assert.That(() => ((IList<OrderTransition>)order.Transitions).Add(order.Transitions[0]), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<Fill>)order.Fills).Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => order.RequestCancellation(OrderTransitionId.New(), Now.AddMinutes(-1)), Throws.ArgumentException);
        });
    }

    [Test]
    public void ClientAndBrokerOrderIdentitiesAreStableAndRequired()
    {
        Assert.That(() => NewOrder(clientOrderId: " "), Throws.ArgumentException);
        var order = OrderIn(OrderStatus.Acknowledged);
        Assert.Multiple(() =>
        {
            Assert.That(order.ClientOrderId, Is.EqualTo("client-1"));
            Assert.That(order.BrokerOrderId, Is.EqualTo("broker-1"));
            Assert.That(typeof(Order).GetProperty(nameof(Order.ClientOrderId))!.SetMethod, Is.Null);
            Assert.That(() => order.Reconcile(OrderTransitionId.New(), OrderStatus.Submitted, "invalid", Now.AddHours(1)), Throws.InvalidOperationException);
        });
    }

    private static Order NewOrder(OrderType orderType = OrderType.Limit, Price? limitPrice = null,
        string clientOrderId = "client-1") =>
        new(OrderId.New(), clientOrderId, PortfolioId.New(), BrokerAccountId.New(), TradeProposalId.New(), InstrumentId.New(),
            OrderSide.Buy, new Quantity(10, "shares"), Currency.USD, orderType,
            limitPrice ?? (orderType == OrderType.Limit ? new Price(25, Currency.USD) : null), TimeInForce.Day, Now);

    private static Order OrderIn(OrderStatus status)
    {
        var order = NewOrder();
        if (status == OrderStatus.Created) return order;
        order.BeginSubmission(OrderTransitionId.New(), Now.AddMinutes(1));
        if (status == OrderStatus.Submitting) return order;
        if (status == OrderStatus.Unknown) { order.MarkUnknown(OrderTransitionId.New(), "timeout", Now.AddMinutes(2)); return order; }
        order.MarkSubmitted(OrderTransitionId.New(), Now.AddMinutes(2));
        if (status == OrderStatus.Submitted) return order;
        if (status == OrderStatus.Rejected) { order.Reject(OrderTransitionId.New(), "rejected", Now.AddMinutes(3)); return order; }
        if (status == OrderStatus.Expired) { order.Expire(OrderTransitionId.New(), "expired", Now.AddMinutes(3)); return order; }
        order.Acknowledge(OrderTransitionId.New(), "broker-1", Now.AddMinutes(3));
        if (status == OrderStatus.Acknowledged) return order;
        if (status == OrderStatus.PartiallyFilled) { ApplyFill(order, 4, "shares", Currency.USD, "setup-partial"); return order; }
        if (status == OrderStatus.Filled) { ApplyFill(order, 10, "shares", Currency.USD, "setup-fill"); return order; }
        order.RequestCancellation(OrderTransitionId.New(), Now.AddMinutes(4));
        if (status == OrderStatus.CancelPending) return order;
        if (status == OrderStatus.Cancelled) { order.Cancel(OrderTransitionId.New(), "cancelled", Now.AddMinutes(5)); return order; }
        throw new ArgumentOutOfRangeException(nameof(status));
    }

    private static void TransitionTo(Order order, OrderStatus target)
    {
        var at = Now.AddHours(1);
        if (order.Status == OrderStatus.Unknown)
        {
            order.Reconcile(OrderTransitionId.New(), target, "reconciled", at,
                target == OrderStatus.Acknowledged ? "broker-1" : null);
            return;
        }
        switch (target)
        {
            case OrderStatus.Submitting: order.BeginSubmission(OrderTransitionId.New(), at); break;
            case OrderStatus.Submitted: order.MarkSubmitted(OrderTransitionId.New(), at); break;
            case OrderStatus.Unknown: order.MarkUnknown(OrderTransitionId.New(), "timeout", at); break;
            case OrderStatus.Acknowledged: order.Acknowledge(OrderTransitionId.New(), "broker-1", at); break;
            case OrderStatus.PartiallyFilled: ApplyFill(order, order.Status == OrderStatus.PartiallyFilled ? 1 : 4, "shares", Currency.USD, Guid.NewGuid().ToString()); break;
            case OrderStatus.Filled: ApplyFill(order, order.Status == OrderStatus.Filled ? 1 : 10 - order.FilledQuantity, "shares", Currency.USD, Guid.NewGuid().ToString()); break;
            case OrderStatus.CancelPending: order.RequestCancellation(OrderTransitionId.New(), at); break;
            case OrderStatus.Cancelled: order.Cancel(OrderTransitionId.New(), "cancelled", at); break;
            case OrderStatus.Rejected: order.Reject(OrderTransitionId.New(), "rejected", at); break;
            case OrderStatus.Expired: order.Expire(OrderTransitionId.New(), "expired", at); break;
            default: throw new InvalidOperationException("There is no transition back to Created.");
        }
    }

    private static bool ApplyFill(Order order, decimal amount, string unit, Currency currency, string executionId) =>
        order.ApplyFill(FillId.New(), OrderTransitionId.New(), executionId, new Quantity(amount, unit),
            new Price(25, currency), new Money(1, currency), Now.AddMinutes(4), Now.AddMinutes(4));
}
