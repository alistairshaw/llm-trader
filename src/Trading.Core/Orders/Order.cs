using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Orders;

public sealed class Order
{
    private readonly List<OrderTransition> transitions = [];
    private readonly List<Fill> fills = [];

    public Order(OrderId id, string clientOrderId, PortfolioId portfolioId, BrokerAccountId brokerAccountId,
        TradeProposalId tradeProposalId, InstrumentId instrumentId, OrderSide side, Quantity quantity,
        Currency currency, OrderType orderType, Price? limitPrice, TimeInForce timeInForce, DateTimeOffset createdAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ClientOrderId = OrderValidation.Required(clientOrderId, nameof(clientOrderId), 200);
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        BrokerAccountId = brokerAccountId ?? throw new ArgumentNullException(nameof(brokerAccountId));
        TradeProposalId = tradeProposalId ?? throw new ArgumentNullException(nameof(tradeProposalId));
        InstrumentId = instrumentId ?? throw new ArgumentNullException(nameof(instrumentId));
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        Side = side;
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        if (!Enum.IsDefined(orderType)) throw new ArgumentOutOfRangeException(nameof(orderType));
        if (!Enum.IsDefined(timeInForce)) throw new ArgumentOutOfRangeException(nameof(timeInForce));
        if (orderType == OrderType.Market && limitPrice is not null)
            throw new ArgumentException("A market order cannot have a limit price.", nameof(limitPrice));
        if (orderType == OrderType.Limit && (limitPrice is null || limitPrice.Amount <= 0))
            throw new ArgumentException("A limit order requires a positive limit price.", nameof(limitPrice));
        if (limitPrice is not null && limitPrice.Currency != currency)
            throw new InvalidOperationException("Order and limit-price currencies must match.");
        OrderType = orderType;
        LimitPrice = limitPrice;
        TimeInForce = timeInForce;
        CreatedAt = OrderValidation.Utc(createdAt, nameof(createdAt));
        Status = OrderStatus.Created;
    }

    public OrderId Id { get; }
    public string ClientOrderId { get; }
    public PortfolioId PortfolioId { get; }
    public BrokerAccountId BrokerAccountId { get; }
    public TradeProposalId TradeProposalId { get; }
    public InstrumentId InstrumentId { get; }
    public OrderSide Side { get; }
    public Quantity Quantity { get; }
    public Currency Currency { get; }
    public OrderType OrderType { get; }
    public Price? LimitPrice { get; }
    public TimeInForce TimeInForce { get; }
    public OrderStatus Status { get; private set; }
    public string? BrokerOrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyList<OrderTransition> Transitions => transitions.AsReadOnly();
    public IReadOnlyList<Fill> Fills => fills.AsReadOnly();
    public decimal FilledQuantity => fills.Sum(fill => fill.Quantity.Amount);
    public decimal CumulativeGrossAmount => fills.Sum(fill => checked(fill.Quantity.Amount * fill.Price.Amount));
    public decimal CumulativeFeeAmount => fills.Sum(fill => fill.Fee.Amount);
    public bool RequiresReconciliation => Status == OrderStatus.Unknown;

    public static Order Rehydrate(OrderId id, string clientOrderId, PortfolioId portfolioId, BrokerAccountId brokerAccountId,
        TradeProposalId tradeProposalId, InstrumentId instrumentId, OrderSide side, Quantity quantity, Currency currency,
        OrderType orderType, Price? limitPrice, TimeInForce timeInForce, DateTimeOffset createdAt, string? brokerOrderId,
        DateTimeOffset? submittedAt, DateTimeOffset? completedAt, long version,
        IReadOnlyList<OrderTransition> persistedTransitions, IReadOnlyList<Fill> persistedFills)
    {
        ArgumentNullException.ThrowIfNull(persistedTransitions); ArgumentNullException.ThrowIfNull(persistedFills);
        if (version < 0 || persistedTransitions.Count != version) throw new ArgumentException("Persisted version and transition count differ.", nameof(version));
        var order = new Order(id, clientOrderId, portfolioId, brokerAccountId, tradeProposalId, instrumentId, side, quantity, currency, orderType, limitPrice, timeInForce, createdAt);
        for (var index = 0; index < persistedTransitions.Count; index++)
        {
            var transition = persistedTransitions[index];
            if (transition.Sequence != index + 1 || transition.PreviousStatus != order.Status) throw new ArgumentException("Persisted transitions are not contiguous.", nameof(persistedTransitions));
            order.transitions.Add(transition); order.Status = transition.NewStatus;
        }
        order.fills.AddRange(persistedFills); order.BrokerOrderId = brokerOrderId; order.SubmittedAt = submittedAt;
        order.CompletedAt = completedAt; order.Version = version; return order;
    }

    public void BeginSubmission(OrderTransitionId id, DateTimeOffset at) =>
        Transition(id, OrderStatus.Submitting, "Submission started.", OrderTransitionSource.Platform, at);

    public void BeginResubmission(OrderTransitionId id, DateTimeOffset at) =>
        Transition(id, OrderStatus.Submitting, "Authoritative absence confirmed; submission restarted.",
            OrderTransitionSource.Reconciliation, at);

    public void MarkSubmitted(OrderTransitionId id, DateTimeOffset at) =>
        Transition(id, OrderStatus.Submitted, "Broker submission completed.", OrderTransitionSource.Platform, at);

    public void MarkUnknown(OrderTransitionId id, string reason, DateTimeOffset at) =>
        Transition(id, OrderStatus.Unknown, reason, OrderTransitionSource.Platform, at);

    public void Acknowledge(OrderTransitionId id, string brokerOrderId, DateTimeOffset at,
        OrderTransitionSource source = OrderTransitionSource.Broker)
    {
        var normalized = OrderValidation.Required(brokerOrderId, nameof(brokerOrderId), 200);
        EnsureBrokerOrderIdentity(normalized);
        Transition(id, OrderStatus.Acknowledged, "Broker acknowledged order.", source, at);
        BrokerOrderId = normalized;
    }

    public void RequestCancellation(OrderTransitionId id, DateTimeOffset at) =>
        Transition(id, OrderStatus.CancelPending, "Cancellation requested.", OrderTransitionSource.Platform, at);

    public void Cancel(OrderTransitionId id, string reason, DateTimeOffset at,
        OrderTransitionSource source = OrderTransitionSource.Broker) =>
        Transition(id, OrderStatus.Cancelled, reason, source, at);

    public void Reject(OrderTransitionId id, string reason, DateTimeOffset at,
        OrderTransitionSource source = OrderTransitionSource.Broker) =>
        Transition(id, OrderStatus.Rejected, reason, source, at);

    public void Expire(OrderTransitionId id, string reason, DateTimeOffset at,
        OrderTransitionSource source = OrderTransitionSource.Broker) =>
        Transition(id, OrderStatus.Expired, reason, source, at);

    public bool ApplyFill(FillId fillId, OrderTransitionId transitionId, string brokerExecutionId,
        Quantity quantity, Price price, Money fee, DateTimeOffset executedAt, DateTimeOffset receivedAt)
    {
        var normalizedExecutionId = OrderValidation.Required(brokerExecutionId, nameof(brokerExecutionId), 200);
        if (fills.Any(fill => string.Equals(fill.BrokerExecutionId, normalizedExecutionId, StringComparison.Ordinal))) return false;
        if (Status is not (OrderStatus.Acknowledged or OrderStatus.PartiallyFilled or OrderStatus.CancelPending))
            throw new InvalidOperationException($"Fills cannot be applied while an order is {Status}.");
        ArgumentNullException.ThrowIfNull(quantity);
        if (!string.Equals(quantity.Unit, Quantity.Unit, StringComparison.Ordinal))
            throw new InvalidOperationException("Fill and order quantity units must match.");
        if (checked(FilledQuantity + quantity.Amount) > Quantity.Amount)
            throw new InvalidOperationException("Filled quantity cannot exceed ordered quantity.");
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(fee);
        if (price.Currency != Currency || fee.Currency != Currency)
            throw new InvalidOperationException("Order, fill price, and fee currencies must match.");

        var fill = new Fill(fillId, normalizedExecutionId, quantity, price, fee, executedAt, receivedAt);
        var target = FilledQuantity + quantity.Amount == Quantity.Amount ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
        EnsureTransitionAllowed(Status, target, OrderTransitionSource.Broker);
        ValidateTransitionTime(receivedAt);
        fills.Add(fill);
        AddTransition(transitionId, target, "Broker execution applied.", OrderTransitionSource.Broker, receivedAt);
        return true;
    }

    public void Reconcile(OrderTransitionId id, OrderStatus reconciledStatus, string reason, DateTimeOffset at,
        string? brokerOrderId = null)
    {
        if (Status != OrderStatus.Unknown) throw new InvalidOperationException("Only an unknown order may be reconciled.");
        if (reconciledStatus == OrderStatus.Acknowledged)
        {
            var normalized = OrderValidation.Required(brokerOrderId, nameof(brokerOrderId), 200);
            EnsureBrokerOrderIdentity(normalized);
            Transition(id, reconciledStatus, reason, OrderTransitionSource.Reconciliation, at);
            BrokerOrderId = normalized;
            return;
        }
        Transition(id, reconciledStatus, reason, OrderTransitionSource.Reconciliation, at);
    }

    private void Transition(OrderTransitionId id, OrderStatus target, string reason, OrderTransitionSource source,
        DateTimeOffset at)
    {
        if (!Enum.IsDefined(target)) throw new ArgumentOutOfRangeException(nameof(target));
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        EnsureTransitionAllowed(Status, target, source);
        ValidateTransitionTime(at);
        AddTransition(id, target, reason, source, at);
    }

    private void AddTransition(OrderTransitionId id, OrderStatus target, string reason,
        OrderTransitionSource source, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (transitions.Any(transition => transition.Id == id))
            throw new InvalidOperationException("Order transition identity must be unique within an order.");
        var previous = Status;
        transitions.Add(new OrderTransition(id, transitions.Count + 1, previous, target, reason, source, at));
        Status = target;
        if (target == OrderStatus.Submitted) SubmittedAt = at;
        if (target is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)
            CompletedAt = at;
        Version++;
    }

    private static void EnsureTransitionAllowed(OrderStatus from, OrderStatus to, OrderTransitionSource source)
    {
        var allowed = (from, to) switch
        {
            (OrderStatus.Created, OrderStatus.Submitting) => true,
            (OrderStatus.Submitting, OrderStatus.Submitted or OrderStatus.Unknown) => true,
            (OrderStatus.Submitted, OrderStatus.Acknowledged or OrderStatus.Rejected or OrderStatus.Expired) => true,
            (OrderStatus.Acknowledged, OrderStatus.PartiallyFilled or OrderStatus.Filled or OrderStatus.CancelPending) => true,
            (OrderStatus.PartiallyFilled, OrderStatus.PartiallyFilled or OrderStatus.Filled or OrderStatus.CancelPending) => true,
            (OrderStatus.CancelPending, OrderStatus.PartiallyFilled or OrderStatus.Filled or OrderStatus.Cancelled) => true,
            (OrderStatus.Unknown, OrderStatus.Submitted or OrderStatus.Acknowledged or OrderStatus.PartiallyFilled or OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)
                when source == OrderTransitionSource.Reconciliation => true,
            (OrderStatus.Unknown, OrderStatus.Submitting) when source == OrderTransitionSource.Reconciliation => true,
            _ => false,
        };
        if (!allowed) throw new InvalidOperationException($"Order cannot transition from {from} to {to}.");
    }

    private void ValidateTransitionTime(DateTimeOffset at)
    {
        OrderValidation.Utc(at, nameof(at));
        var previousTime = transitions.Count == 0 ? CreatedAt : transitions[^1].OccurredAt;
        if (at < previousTime) throw new ArgumentException("Order transitions must be chronologically ordered.", nameof(at));
    }

    private void EnsureBrokerOrderIdentity(string brokerOrderId)
    {
        if (BrokerOrderId is not null && !string.Equals(BrokerOrderId, brokerOrderId, StringComparison.Ordinal))
            throw new InvalidOperationException("Broker order identity cannot change.");
    }
}
