using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Orders;

namespace Trading.Engine.Execution;

public sealed record OrderConversionCommand(TradeProposalId ProposalId, CapitalReservationId ReservationId,
    ClientOrderIdentity ClientOrderId, DateTimeOffset RequestedAt);
public enum OrderConversionOutcome { Created, AlreadyCreated, Rejected, NotFound, Contention }
public sealed record OrderConversionResult(OrderConversionOutcome Outcome, string Code, Order? Order);
public interface IOrderConversionService
{
    Task<OrderConversionResult> ConvertAsync(OrderConversionCommand command, CancellationToken cancellationToken);
}

public interface IOrderWorkStore
{
    Task<IReadOnlyList<OrderWorkEnvelope>> ClaimAsync(int limit, DateTimeOffset now, CancellationToken cancellationToken);
    Task CompleteAsync(OrderWorkItemId id, string resultCode, DateTimeOffset completedAt, CancellationToken cancellationToken);
    Task RetryAsync(OrderWorkItemId id, string resultCode, DateTimeOffset availableAt, CancellationToken cancellationToken);
}

public interface IBrokerInbox
{
    Task<bool> ReceiveAsync(BrokerInboxEnvelope envelope, CancellationToken cancellationToken);
    Task<IReadOnlyList<BrokerInboxEnvelope>> ClaimAsync(int limit, CancellationToken cancellationToken);
    Task CompleteAsync(BrokerMessageId id, string resultCode, DateTimeOffset completedAt, CancellationToken cancellationToken);
}

public interface IOrderReconciliationService
{
    Task<BrokerReconciliationResult> ReconcileAsync(OrderId orderId, CancellationToken cancellationToken);
}

public interface IFillAccountingService
{
    Task<string> ApplyAsync(OrderId orderId, BrokerOrderEvent execution, CancellationToken cancellationToken);
}

public interface IOrderExecutionClock { DateTimeOffset UtcNow { get; } }

public interface IOrderExecutionIdentifierSource
{
    OrderId NewOrderId();
    OrderTransitionId NewTransitionId();
    FillId NewFillId();
    OrderWorkItemId NewWorkItemId();
    BrokerMessageId NewBrokerMessageId();
    CorrelationIdentity NewCorrelationId();
}

public interface IOrderExecutionTransaction
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
