using System.Globalization;
using System.Text.Json;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Engine.Execution;

public sealed class FillAccountingDispatcher(IFillAccountingRepository repository, IOrderExecutionClock clock,
    string leaseOwner) : IBrokerInboxDispatcher
{
    private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal)
    { "schemaVersion", "environment", "brokerAccountId", "clientOrderId", "brokerOrderId", "kind", "code", "occurredAt", "execution" };
    private static readonly HashSet<string> ExecutionProperties = new(StringComparer.Ordinal)
    { "executionId", "quantity", "quantityUnit", "price", "currency", "fee", "feeCurrency", "executedAt" };

    public async Task<DurableBrokerDispatchResult> DispatchAsync(BrokerInboxEnvelope message, CancellationToken cancellationToken)
    {
        if (!TryParse(message, out var parsed)) return new(DurableBrokerDispatchDisposition.Terminal, BrokerOrderEventCodes.InvalidSchema);
        var result = await repository.ApplyAsync(new(message, leaseOwner, parsed.Account, parsed.Environment,
            parsed.ClientOrderId, parsed.BrokerOrderId, parsed.Execution, clock.UtcNow), cancellationToken).ConfigureAwait(false);
        return result.Disposition switch
        {
            FillAccountingWriteDisposition.Applied or FillAccountingWriteDisposition.Duplicate or FillAccountingWriteDisposition.Rejected =>
                new(DurableBrokerDispatchDisposition.Finalized, result.Code),
            FillAccountingWriteDisposition.Deferred or FillAccountingWriteDisposition.Contention =>
                new(DurableBrokerDispatchDisposition.Retryable, result.Code),
            _ => new(DurableBrokerDispatchDisposition.Terminal, result.Code)
        };
    }

    private static bool TryParse(BrokerInboxEnvelope message, out Parsed value)
    {
        value = default!;
        try
        {
            using var document = JsonDocument.Parse(message.CanonicalPayload);
            var root = document.RootElement;
            if (!HasExactProperties(root, RootProperties) || root.GetProperty("schemaVersion").GetInt32() != 1 ||
                root.GetProperty("environment").GetString() != "Paper" || root.GetProperty("kind").GetString() != "Execution") return false;
            var detail = root.GetProperty("execution");
            if (!HasExactProperties(detail, ExecutionProperties)) return false;
            var currency = new Currency(detail.GetProperty("currency").GetString()!);
            var feeCurrency = new Currency(detail.GetProperty("feeCurrency").GetString()!);
            var executedAt = ParseUtc(detail.GetProperty("executedAt").GetString()!);
            var occurredAt = ParseUtc(root.GetProperty("occurredAt").GetString()!);
            if (executedAt > occurredAt || occurredAt > message.ReceivedAt) return false;
            var execution = new BrokerExecution(detail.GetProperty("executionId").GetString()!,
                new Quantity(detail.GetProperty("quantity").GetDecimal(), detail.GetProperty("quantityUnit").GetString()!),
                new Price(detail.GetProperty("price").GetDecimal(), currency),
                new Money(detail.GetProperty("fee").GetDecimal(), feeCurrency), executedAt);
            value = new(BrokerAccountId.Parse(root.GetProperty("brokerAccountId").GetString()!), "Paper",
                new ClientOrderIdentity(root.GetProperty("clientOrderId").GetString()!),
                root.GetProperty("brokerOrderId").GetString()!, execution);
            return !string.IsNullOrWhiteSpace(value.BrokerOrderId);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentException or KeyNotFoundException)
        { return false; }
    }

    private static bool HasExactProperties(JsonElement element, HashSet<string> expected)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var properties = element.EnumerateObject().Select(x => x.Name).ToArray();
        return properties.Length == expected.Count && properties.Distinct(StringComparer.Ordinal).Count() == properties.Length && properties.All(expected.Contains);
    }

    private static DateTimeOffset ParseUtc(string value)
    {
        var parsed = DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None);
        if (parsed.Offset != TimeSpan.Zero) throw new FormatException("Timestamp must be UTC.");
        return parsed;
    }

    private sealed record Parsed(BrokerAccountId Account, string Environment, ClientOrderIdentity ClientOrderId,
        string BrokerOrderId, BrokerExecution Execution);
}
