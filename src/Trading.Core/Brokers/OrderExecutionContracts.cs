using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;

namespace Trading.Core.Brokers;

public static class BrokerExecutionCodes
{
    public const string Accepted = "broker.accepted";
    public const string Rejected = "broker.rejected";
    public const string Unknown = "broker.submission_unknown";
    public const string Retryable = "broker.retryable";
    public const string Terminal = "broker.terminal";
    public const string Duplicate = "broker.duplicate";
    public const string ReconciledFound = "broker.reconciled_found";
    public const string ReconciledAbsent = "broker.reconciled_absent";
    public const string ReconciliationUncertain = "broker.reconciliation_uncertain";
}

public sealed record ClientOrderIdentity
{
    public ClientOrderIdentity(string value)
    {
        Value = BrokerContractValidation.Required(value, nameof(value), 100);
        if (!Value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            throw new ArgumentException("Client order identity contains unsupported characters.", nameof(value));
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record CorrelationIdentity
{
    public CorrelationIdentity(string value) => Value = BrokerContractValidation.Required(value, nameof(value), 100);
    public string Value { get; }
    public override string ToString() => Value;
}

public abstract record BrokerOperationEnvironment
{
    private BrokerOperationEnvironment() { }
    public sealed record Paper(string Name) : BrokerOperationEnvironment
    {
        public string Name { get; } = BrokerContractValidation.Required(Name, nameof(Name), 100);
    }
    public sealed record Live(string Name) : BrokerOperationEnvironment
    {
        public string Name { get; } = BrokerContractValidation.Required(Name, nameof(Name), 100);
    }
}

[Flags]
public enum BrokerCapabilities
{
    None = 0,
    SubmitMarketOrders = 1,
    SubmitLimitOrders = 2,
    LookupByClientOrderId = 4,
    ReconcileOrderStatus = 8,
    CancelOrders = 16,
    StreamExecutions = 32,
}

public sealed record BrokerOperationContext(
    BrokerAccountId BrokerAccountId,
    BrokerConnectionId BrokerConnectionId,
    BrokerOperationEnvironment Environment,
    CorrelationIdentity CorrelationId,
    DateTimeOffset RequestedAt)
{
    public DateTimeOffset RequestedAt { get; } = BrokerContractValidation.Utc(RequestedAt, nameof(RequestedAt));
}

public sealed record PaperBrokerOperationContext(
    BrokerAccountId BrokerAccountId,
    BrokerConnectionId BrokerConnectionId,
    BrokerOperationEnvironment.Paper Environment,
    CorrelationIdentity CorrelationId,
    DateTimeOffset RequestedAt)
{
    public DateTimeOffset RequestedAt { get; } = BrokerContractValidation.Utc(RequestedAt, nameof(RequestedAt));
}

public sealed record BrokerOrderRequest(
    ClientOrderIdentity ClientOrderId,
    InstrumentId InstrumentId,
    string ExternalInstrumentId,
    OrderSide Side,
    Quantity Quantity,
    Currency Currency,
    OrderType OrderType,
    Price? LimitPrice,
    TimeInForce TimeInForce)
{
    public string ExternalInstrumentId { get; } = BrokerContractValidation.Required(ExternalInstrumentId, nameof(ExternalInstrumentId), 200);
}

public enum BrokerSubmissionOutcome { Accepted, Rejected, Unknown, RetryableFailure, TerminalFailure, Duplicate }
public sealed record BrokerSubmissionResult(
    BrokerSubmissionOutcome Outcome,
    string Code,
    string? BrokerOrderId,
    DateTimeOffset CompletedAt)
{
    public string Code { get; } = BrokerContractValidation.Code(Code, nameof(Code));
    public string? BrokerOrderId { get; } = BrokerContractValidation.Optional(BrokerOrderId, nameof(BrokerOrderId), 200);
    public DateTimeOffset CompletedAt { get; } = BrokerContractValidation.Utc(CompletedAt, nameof(CompletedAt));
}

public sealed record BrokerOrderLookup(ClientOrderIdentity ClientOrderId);
public enum BrokerReconciliationOutcome { Found, Absent, Uncertain, RetryableFailure, TerminalFailure }
public sealed record BrokerReconciliationResult(
    BrokerReconciliationOutcome Outcome,
    string Code,
    string? BrokerOrderId,
    OrderStatus? Status,
    DateTimeOffset ObservedAt)
{
    public string Code { get; } = BrokerContractValidation.Code(Code, nameof(Code));
    public string? BrokerOrderId { get; } = BrokerContractValidation.Optional(BrokerOrderId, nameof(BrokerOrderId), 200);
    public DateTimeOffset ObservedAt { get; } = BrokerContractValidation.Utc(ObservedAt, nameof(ObservedAt));
}

public sealed record BrokerCancellationRequest(ClientOrderIdentity ClientOrderId, string BrokerOrderId)
{
    public string BrokerOrderId { get; } = BrokerContractValidation.Required(BrokerOrderId, nameof(BrokerOrderId), 200);
}

public enum BrokerCancellationOutcome { Accepted, AlreadyTerminal, Rejected, Unknown, RetryableFailure, TerminalFailure }
public sealed record BrokerCancellationResult(BrokerCancellationOutcome Outcome, string Code, DateTimeOffset CompletedAt)
{
    public string Code { get; } = BrokerContractValidation.Code(Code, nameof(Code));
    public DateTimeOffset CompletedAt { get; } = BrokerContractValidation.Utc(CompletedAt, nameof(CompletedAt));
}

public enum BrokerOrderEventKind { Acknowledged, Rejected, Cancelled, Expired, Execution }
public sealed record BrokerExecution(
    string ExecutionId,
    Quantity Quantity,
    Price Price,
    Money Fee,
    DateTimeOffset ExecutedAt)
{
    public string ExecutionId { get; } = BrokerContractValidation.Required(ExecutionId, nameof(ExecutionId), 200);
    public DateTimeOffset ExecutedAt { get; } = BrokerContractValidation.Utc(ExecutedAt, nameof(ExecutedAt));
}

public sealed record BrokerOrderEvent(
    BrokerMessageId MessageId,
    ClientOrderIdentity ClientOrderId,
    string? BrokerOrderId,
    BrokerOrderEventKind Kind,
    string Code,
    BrokerExecution? Execution,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt)
{
    public string? BrokerOrderId { get; } = BrokerContractValidation.Optional(BrokerOrderId, nameof(BrokerOrderId), 200);
    public string Code { get; } = BrokerContractValidation.Code(Code, nameof(Code));
    public DateTimeOffset OccurredAt { get; } = BrokerContractValidation.Utc(OccurredAt, nameof(OccurredAt));
    public DateTimeOffset ReceivedAt { get; } = BrokerContractValidation.Utc(ReceivedAt, nameof(ReceivedAt));
}

public interface IPaperBrokerGateway
{
    BrokerCapabilities Capabilities { get; }
    Task<BrokerSubmissionResult> SubmitAsync(PaperBrokerOperationContext context, BrokerOrderRequest request,
        CancellationToken cancellationToken);
    Task<BrokerReconciliationResult> FindByClientOrderIdAsync(PaperBrokerOperationContext context,
        BrokerOrderLookup lookup, CancellationToken cancellationToken);
    Task<BrokerReconciliationResult> ReconcileAsync(PaperBrokerOperationContext context,
        BrokerOrderLookup lookup, CancellationToken cancellationToken);
    Task<BrokerCancellationResult> CancelAsync(PaperBrokerOperationContext context,
        BrokerCancellationRequest request, CancellationToken cancellationToken);
}

public enum OrderWorkKind { Submit, Reconcile, Cancel, ApplyBrokerEvent }
public sealed record OrderWorkEnvelope(
    OrderWorkItemId Id,
    OrderId OrderId,
    OrderWorkKind Kind,
    string IdempotencyKey,
    string CanonicalPayload,
    CorrelationIdentity CorrelationId,
    int Attempt,
    DateTimeOffset AvailableAt,
    DateTimeOffset CreatedAt)
{
    public string IdempotencyKey { get; } = BrokerContractValidation.Required(IdempotencyKey, nameof(IdempotencyKey), 200);
    public string CanonicalPayload { get; } = BrokerContractValidation.Required(CanonicalPayload, nameof(CanonicalPayload), 16_384);
    public DateTimeOffset AvailableAt { get; } = BrokerContractValidation.Utc(AvailableAt, nameof(AvailableAt));
    public DateTimeOffset CreatedAt { get; } = BrokerContractValidation.Utc(CreatedAt, nameof(CreatedAt));
}

public sealed record BrokerInboxEnvelope(
    BrokerMessageId Id,
    string IdempotencyKey,
    string CanonicalPayload,
    CorrelationIdentity CorrelationId,
    DateTimeOffset ReceivedAt)
{
    public string IdempotencyKey { get; } = BrokerContractValidation.Required(IdempotencyKey, nameof(IdempotencyKey), 200);
    public string CanonicalPayload { get; } = BrokerContractValidation.Required(CanonicalPayload, nameof(CanonicalPayload), 16_384);
    public DateTimeOffset ReceivedAt { get; } = BrokerContractValidation.Utc(ReceivedAt, nameof(ReceivedAt));
}

internal static class BrokerContractValidation
{
    public static string Required(string? value, string name, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maximumLength) throw new ArgumentException("Value is missing or exceeds its bound.", name);
        return trimmed;
    }

    public static string? Optional(string? value, string name, int maximumLength) =>
        value is null ? null : Required(value, name, maximumLength);

    public static string Code(string value, string name)
    {
        var code = Required(value, name, 100);
        if (!code.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
            throw new ArgumentException("Result code is not canonical.", name);
        return code;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", name);
        return value;
    }
}
