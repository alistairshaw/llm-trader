using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Orders;

public enum OrderSide { Buy, Sell }
public enum OrderType { Market, Limit }
public enum TimeInForce { Day, GoodTillCancelled, ImmediateOrCancel, FillOrKill }
public enum OrderStatus { Created, Submitting, Submitted, Acknowledged, PartiallyFilled, Filled, CancelPending, Cancelled, Rejected, Expired, Unknown }
public enum OrderTransitionSource { Platform, Broker, Reconciliation }

public sealed class OrderTransition
{
    public OrderTransition(OrderTransitionId id, int sequence, OrderStatus previousStatus, OrderStatus newStatus,
        string reason, OrderTransitionSource source, DateTimeOffset occurredAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Sequence = sequence;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Reason = OrderValidation.Required(reason, nameof(reason), 1000);
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        Source = source;
        OccurredAt = OrderValidation.Utc(occurredAt, nameof(occurredAt));
    }

    public OrderTransitionId Id { get; }
    public int Sequence { get; }
    public OrderStatus PreviousStatus { get; }
    public OrderStatus NewStatus { get; }
    public string Reason { get; }
    public OrderTransitionSource Source { get; }
    public DateTimeOffset OccurredAt { get; }
}

public sealed class Fill
{
    public Fill(FillId id, string brokerExecutionId, Quantity quantity, Price price, Money fee,
        DateTimeOffset executedAt, DateTimeOffset receivedAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        BrokerExecutionId = OrderValidation.Required(brokerExecutionId, nameof(brokerExecutionId), 200);
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
        Price = price ?? throw new ArgumentNullException(nameof(price));
        Fee = fee ?? throw new ArgumentNullException(nameof(fee));
        if (price.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(price), "Fill price must be positive.");
        if (fee.Amount < 0) throw new ArgumentOutOfRangeException(nameof(fee), "Fill fee cannot be negative.");
        if (fee.Currency != price.Currency) throw new InvalidOperationException("Fill fee and price currencies must match.");
        ExecutedAt = OrderValidation.Utc(executedAt, nameof(executedAt));
        ReceivedAt = OrderValidation.Utc(receivedAt, nameof(receivedAt));
        if (receivedAt < executedAt) throw new ArgumentException("A fill cannot be received before it executes.", nameof(receivedAt));
    }

    public FillId Id { get; }
    public string BrokerExecutionId { get; }
    public Quantity Quantity { get; }
    public Price Price { get; }
    public Money Fee { get; }
    public DateTimeOffset ExecutedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
}

internal static class OrderValidation
{
    public static string Required(string? value, string name, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        var trimmed = value.Trim();
        if (trimmed.Length == 0) throw new ArgumentException("Value is required.", name);
        if (trimmed.Length > maximumLength) throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", name);
        return trimmed;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be expressed in UTC.", name);
        return value;
    }
}
