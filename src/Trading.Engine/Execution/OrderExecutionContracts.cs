using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Engine.Execution;

public sealed record OrderConversionCommand(TradeProposalId ProposalId, CapitalReservationId ReservationId,
    DateTimeOffset RequestedAt);
public enum OrderConversionOutcome { Created, AlreadyCreated, Rejected, NotFound, Contention }
public sealed record OrderConversionResult(OrderConversionOutcome Outcome, string Code, Order? Order);
public interface IOrderConversionService
{
    Task<OrderConversionResult> ConvertAsync(OrderConversionCommand command, CancellationToken cancellationToken);
}

public static class OrderConversionCodes
{
    public const string Created = "order_conversion.created";
    public const string AlreadyCreated = "order_conversion.already_created";
    public const string NotFound = "order_conversion.not_found";
    public const string ProposalNotApproved = AtomicOrderConversionCodes.ProposalNotApproved;
    public const string ProposalExpired = AtomicOrderConversionCodes.ProposalExpired;
    public const string ResearchOnly = AtomicOrderConversionCodes.ResearchOnly;
    public const string EnvironmentMismatch = AtomicOrderConversionCodes.EnvironmentMismatch;
    public const string ApprovalMismatch = AtomicOrderConversionCodes.ApprovalMismatch;
    public const string EvaluationMismatch = AtomicOrderConversionCodes.EvaluationMismatch;
    public const string SnapshotMismatch = AtomicOrderConversionCodes.SnapshotMismatch;
    public const string ReservationMismatch = AtomicOrderConversionCodes.ReservationMismatch;
    public const string PortfolioMismatch = AtomicOrderConversionCodes.PortfolioMismatch;
    public const string AccountRestricted = AtomicOrderConversionCodes.AccountRestricted;
    public const string AccountUnreconciled = AtomicOrderConversionCodes.AccountUnreconciled;
    public const string InstrumentUnavailable = AtomicOrderConversionCodes.InstrumentUnavailable;
    public const string InstrumentMappingUnavailable = AtomicOrderConversionCodes.InstrumentMappingUnavailable;
    public const string CurrencyMismatch = AtomicOrderConversionCodes.CurrencyMismatch;
    public const string UnsupportedAction = AtomicOrderConversionCodes.UnsupportedAction;
    public const string Contention = "order_conversion.contention";
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
