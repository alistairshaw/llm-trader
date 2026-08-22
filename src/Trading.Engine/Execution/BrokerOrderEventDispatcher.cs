using System.Globalization;
using System.Text.Json;
using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Engine.Execution;

public static class BrokerOrderEventCodes
{
    public const string Applied = "broker_event.applied";
    public const string Duplicate = "broker_event.duplicate";
    public const string InvalidSchema = "broker_event.invalid_schema";
    public const string EnvironmentMismatch = "broker_event.environment_mismatch";
    public const string IdentityMismatch = "broker_event.identity_mismatch";
    public const string UnknownOrder = "broker_event.unknown_order";
    public const string Stale = "broker_event.stale";
    public const string ImpossibleTransition = "broker_event.impossible_transition";
    public const string FillConflict = "broker_event.fill_conflict";
    public const string ReconciliationRequired = "broker_event.reconciliation_required";
    public const string Contention = "broker_event.contention";
}

public sealed class BrokerOrderEventDispatcher(
    IBrokerOrderEventRepository repository,
    IOrderExecutionClock clock,
    string leaseOwner) : IBrokerInboxDispatcher
{
    private static readonly HashSet<string> Properties = new(StringComparer.Ordinal)
    {
        "schemaVersion", "environment", "brokerAccountId", "clientOrderId", "brokerOrderId", "kind", "code", "occurredAt"
    };

    public async Task<DurableBrokerDispatchResult> DispatchAsync(
        BrokerInboxEnvelope message, CancellationToken cancellationToken)
    {
        if (!TryParse(message, out var parsed))
            return new(DurableBrokerDispatchDisposition.Terminal, BrokerOrderEventCodes.InvalidSchema);

        var result = await repository.ApplyAsync(new ApplyBrokerOrderEventCommand(
            message, leaseOwner, parsed.AccountId, parsed.Environment, parsed.ClientOrderId,
            parsed.BrokerOrderId, parsed.Kind, parsed.Code, parsed.OccurredAt, clock.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return result.Disposition switch
        {
            BrokerOrderEventWriteDisposition.Applied or BrokerOrderEventWriteDisposition.Duplicate or
                BrokerOrderEventWriteDisposition.Rejected or BrokerOrderEventWriteDisposition.Reconcile =>
                new(DurableBrokerDispatchDisposition.Finalized, result.Code),
            BrokerOrderEventWriteDisposition.Deferred or BrokerOrderEventWriteDisposition.Contention =>
                new(DurableBrokerDispatchDisposition.Retryable, result.Code),
            _ => new(DurableBrokerDispatchDisposition.Terminal, result.Code)
        };
    }

    private static bool TryParse(BrokerInboxEnvelope message, out ParsedEvent value)
    {
        value = default!;
        try
        {
            using var document = JsonDocument.Parse(message.CanonicalPayload);
            var root = document.RootElement;
            var names = root.EnumerateObject().Select(x => x.Name).ToArray();
            if (names.Any(x => !Properties.Contains(x)) || names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
                root.GetProperty("schemaVersion").GetInt32() != 1) return false;
            var environment = root.GetProperty("environment").GetString();
            if (!string.Equals(environment, "Paper", StringComparison.Ordinal)) return false;
            var account = BrokerAccountId.Parse(root.GetProperty("brokerAccountId").GetString()!);
            var client = new ClientOrderIdentity(root.GetProperty("clientOrderId").GetString()!);
            var broker = root.GetProperty("brokerOrderId").ValueKind == JsonValueKind.Null ? null : root.GetProperty("brokerOrderId").GetString();
            var kind = Enum.Parse<BrokerOrderEventKind>(root.GetProperty("kind").GetString()!, false);
            if (kind == BrokerOrderEventKind.Execution) return false;
            if (kind is BrokerOrderEventKind.Acknowledged or BrokerOrderEventKind.Cancelled or
                BrokerOrderEventKind.Expired && broker is null) return false;
            var code = root.GetProperty("code").GetString()!;
            _ = new BrokerOrderEvent(message.Id, client, broker, kind, code, null, message.ReceivedAt, message.ReceivedAt);
            var occurred = DateTimeOffset.ParseExact(root.GetProperty("occurredAt").GetString()!, "O", CultureInfo.InvariantCulture, DateTimeStyles.None);
            if (occurred.Offset != TimeSpan.Zero || occurred > message.ReceivedAt) return false;
            value = new(account, environment!, client, broker, kind, code, occurred);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            FormatException or ArgumentException or KeyNotFoundException)
        {
            return false;
        }
    }

    private sealed record ParsedEvent(BrokerAccountId AccountId, string Environment,
        ClientOrderIdentity ClientOrderId, string? BrokerOrderId, BrokerOrderEventKind Kind,
        string Code, DateTimeOffset OccurredAt);
}
