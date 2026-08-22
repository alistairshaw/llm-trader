using System.Text.Json;
using Trading.Core.Brokers;
using Trading.Core.Persistence;

namespace Trading.Engine.Execution;

public static class DurableBrokerProcessingCodes
{
    public const string Completed = "broker_work.completed";
    public const string Cancelled = "broker_work.cancelled";
    public const string MalformedPayload = "broker_work.malformed_payload";
    public const string PayloadNotCanonical = "broker_work.payload_not_canonical";
    public const string RetryExhausted = "broker_work.retry_exhausted";
    public const string TransientFailure = "broker_work.transient_failure";
    public const string TerminalFailure = "broker_work.terminal_failure";
    public const string LeaseLost = "broker_work.lease_lost";
}

public enum DurableBrokerDispatchDisposition { Completed, Finalized, Retryable, Terminal }
public sealed record DurableBrokerDispatchResult(DurableBrokerDispatchDisposition Disposition, string Code);
public sealed record DurableBrokerDrainResult(int Claimed, int Completed, int Retried, int Failed, int LeaseLost)
{
    public int Processed => Completed + Retried + Failed + LeaseLost;
}

public sealed record DurableBrokerProcessorOptions(int BatchSize, int MaximumAttempts, TimeSpan LeaseDuration,
    TimeSpan InitialBackoff, TimeSpan MaximumBackoff)
{
    public static DurableBrokerProcessorOptions Default { get; } = new(16, 5, TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

    public DurableBrokerProcessorOptions Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumAttempts, 1);
        if (LeaseDuration <= TimeSpan.Zero || InitialBackoff <= TimeSpan.Zero || MaximumBackoff < InitialBackoff)
            throw new ArgumentOutOfRangeException(nameof(LeaseDuration));
        return this;
    }
}

public interface IOrderWorkDispatcher
{
    Task<DurableBrokerDispatchResult> DispatchAsync(OrderWorkEnvelope work, CancellationToken cancellationToken);
}

public interface IBrokerInboxDispatcher
{
    Task<DurableBrokerDispatchResult> DispatchAsync(BrokerInboxEnvelope message, CancellationToken cancellationToken);
}

public sealed class DurableBrokerTransientException(string code) : Exception(code)
{
    public string Code { get; } = DurableBrokerPayload.Code(code, DurableBrokerProcessingCodes.TransientFailure);
}

public sealed class OrderOutboxProcessor(IOrderWorkRepository repository, IOrderWorkDispatcher dispatcher,
    IOrderExecutionClock clock, string owner, DurableBrokerProcessorOptions options)
{
    private readonly DurableBrokerProcessorOptions options = options.Validate();
    private readonly string owner = DurableBrokerPayload.Identity(owner);

    public async Task<DurableBrokerDrainResult> DrainOnceAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var work = await repository.ClaimAsync(options.BatchSize, now,
            new DurableWorkLease(owner, now + options.LeaseDuration), cancellationToken).ConfigureAwait(false);
        var completed = 0; var retried = 0; var failed = 0; var leaseLost = 0;
        foreach (var item in work)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (await RetryAsync(item, DurableBrokerProcessingCodes.Cancelled, CancellationToken.None).ConfigureAwait(false)) retried++; else leaseLost++;
                continue;
            }

            var validation = DurableBrokerPayload.Validate(item.CanonicalPayload);
            if (validation is not null)
            {
                if (await FailAsync(item, validation, cancellationToken).ConfigureAwait(false)) failed++; else leaseLost++;
                continue;
            }

            if (!await RenewAsync(item, cancellationToken).ConfigureAwait(false)) { leaseLost++; continue; }
            DurableBrokerDispatchResult result;
            try { result = await dispatcher.DispatchAsync(item, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { if (await RetryAsync(item, DurableBrokerProcessingCodes.Cancelled, CancellationToken.None).ConfigureAwait(false)) retried++; else leaseLost++; continue; }
            catch (DurableBrokerTransientException exception) { result = new(DurableBrokerDispatchDisposition.Retryable, exception.Code); }
            catch (Exception) { result = new(DurableBrokerDispatchDisposition.Terminal, DurableBrokerProcessingCodes.TerminalFailure); }

            var code = DurableBrokerPayload.Code(result.Code, result.Disposition == DurableBrokerDispatchDisposition.Completed ? DurableBrokerProcessingCodes.Completed : DurableBrokerProcessingCodes.TerminalFailure);
            if (result.Disposition == DurableBrokerDispatchDisposition.Finalized) completed++;
            else if (result.Disposition == DurableBrokerDispatchDisposition.Completed)
            { if (await IsSuccess(repository.CompleteAsync(item.Id, owner, code, clock.UtcNow, cancellationToken)).ConfigureAwait(false)) completed++; else leaseLost++; }
            else if (result.Disposition == DurableBrokerDispatchDisposition.Retryable && item.Attempt < options.MaximumAttempts)
            { if (await RetryAsync(item, code, cancellationToken).ConfigureAwait(false)) retried++; else leaseLost++; }
            else
            { if (await FailAsync(item, result.Disposition == DurableBrokerDispatchDisposition.Retryable ? DurableBrokerProcessingCodes.RetryExhausted : code, cancellationToken).ConfigureAwait(false)) failed++; else leaseLost++; }
        }
        return new(work.Count, completed, retried, failed, leaseLost);
    }

    private async Task<bool> RenewAsync(OrderWorkEnvelope item, CancellationToken token) => await IsSuccess(repository.RenewAsync(item.Id, owner, clock.UtcNow + options.LeaseDuration, token)).ConfigureAwait(false);
    private async Task<bool> RetryAsync(OrderWorkEnvelope item, string code, CancellationToken token) => await IsSuccess(repository.RetryAsync(item.Id, owner, code, clock.UtcNow + Backoff(item.Attempt), token)).ConfigureAwait(false);
    private async Task<bool> FailAsync(OrderWorkEnvelope item, string code, CancellationToken token) => await IsSuccess(repository.FailAsync(item.Id, owner, code, clock.UtcNow, token)).ConfigureAwait(false);
    private TimeSpan Backoff(int attempt) => TimeSpan.FromTicks(Math.Min(options.MaximumBackoff.Ticks, options.InitialBackoff.Ticks * (1L << Math.Min(30, Math.Max(0, attempt - 1)))));
    private static async Task<bool> IsSuccess(Task<PersistenceWriteResult> operation) => await operation.ConfigureAwait(false) is PersistenceWriteResult.Succeeded;
}

public sealed class BrokerInboxProcessor(IBrokerInboxRepository repository, IBrokerInboxDispatcher dispatcher,
    IOrderExecutionClock clock, string owner, DurableBrokerProcessorOptions options)
{
    private readonly DurableBrokerProcessorOptions options = options.Validate();
    private readonly string owner = DurableBrokerPayload.Identity(owner);

    public async Task<DurableBrokerDrainResult> DrainOnceAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var messages = await repository.ClaimAsync(options.BatchSize, now,
            new DurableWorkLease(owner, now + options.LeaseDuration), cancellationToken).ConfigureAwait(false);
        var completed = 0; var retried = 0; var failed = 0; var leaseLost = 0;
        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested)
            { if (await RetryAsync(message, DurableBrokerProcessingCodes.Cancelled, CancellationToken.None).ConfigureAwait(false)) retried++; else leaseLost++; continue; }
            var validation = DurableBrokerPayload.Validate(message.CanonicalPayload);
            if (validation is not null)
            { if (await FailAsync(message, validation, cancellationToken).ConfigureAwait(false)) failed++; else leaseLost++; continue; }
            if (!await IsSuccess(repository.RenewAsync(message.Id, owner, clock.UtcNow + options.LeaseDuration, cancellationToken)).ConfigureAwait(false)) { leaseLost++; continue; }
            DurableBrokerDispatchResult result;
            try { result = await dispatcher.DispatchAsync(message, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { if (await RetryAsync(message, DurableBrokerProcessingCodes.Cancelled, CancellationToken.None).ConfigureAwait(false)) retried++; else leaseLost++; continue; }
            catch (DurableBrokerTransientException exception) { result = new(DurableBrokerDispatchDisposition.Retryable, exception.Code); }
            catch (Exception) { result = new(DurableBrokerDispatchDisposition.Terminal, DurableBrokerProcessingCodes.TerminalFailure); }
            var code = DurableBrokerPayload.Code(result.Code, DurableBrokerProcessingCodes.TerminalFailure);
            if (result.Disposition == DurableBrokerDispatchDisposition.Finalized) completed++;
            else if (result.Disposition == DurableBrokerDispatchDisposition.Completed)
            { if (await IsSuccess(repository.CompleteAsync(message.Id, owner, code, clock.UtcNow, cancellationToken)).ConfigureAwait(false)) completed++; else leaseLost++; }
            else if (result.Disposition == DurableBrokerDispatchDisposition.Retryable && message.Attempt < options.MaximumAttempts)
            { if (await RetryAsync(message, code, cancellationToken).ConfigureAwait(false)) retried++; else leaseLost++; }
            else
            { if (await FailAsync(message, result.Disposition == DurableBrokerDispatchDisposition.Retryable ? DurableBrokerProcessingCodes.RetryExhausted : code, cancellationToken).ConfigureAwait(false)) failed++; else leaseLost++; }
        }
        return new(messages.Count, completed, retried, failed, leaseLost);
    }

    private async Task<bool> RetryAsync(BrokerInboxEnvelope item, string code, CancellationToken token) => await IsSuccess(repository.RetryAsync(item.Id, owner, code, clock.UtcNow + Backoff(item.Attempt), token)).ConfigureAwait(false);
    private TimeSpan Backoff(int attempt) => TimeSpan.FromTicks(Math.Min(options.MaximumBackoff.Ticks, options.InitialBackoff.Ticks * (1L << Math.Min(30, Math.Max(0, attempt - 1)))));
    private async Task<bool> FailAsync(BrokerInboxEnvelope item, string code, CancellationToken token) => await IsSuccess(repository.FailAsync(item.Id, owner, code, clock.UtcNow, token)).ConfigureAwait(false);
    private static async Task<bool> IsSuccess(Task<PersistenceWriteResult> operation) => await operation.ConfigureAwait(false) is PersistenceWriteResult.Succeeded;
}

internal static class DurableBrokerPayload
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static string? Validate(string payload)
    {
        if (payload.Length > 16_384) return DurableBrokerProcessingCodes.MalformedPayload;
        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 32 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return DurableBrokerProcessingCodes.MalformedPayload;
            if (HasDuplicateProperties(document.RootElement)) return DurableBrokerProcessingCodes.MalformedPayload;
            var canonical = JsonSerializer.Serialize(document.RootElement, SerializerOptions);
            return string.Equals(payload, canonical, StringComparison.Ordinal) ? null : DurableBrokerProcessingCodes.PayloadNotCanonical;
        }
        catch (JsonException) { return DurableBrokerProcessingCodes.MalformedPayload; }
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) if (HasDuplicateProperties(item)) return true;
        return false;
    }

    public static string Identity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 200) throw new ArgumentException("Lease owner exceeds its bound.", nameof(value));
        return value;
    }

    public static string Code(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100) return fallback;
        return value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-') ? value : fallback;
    }
}
