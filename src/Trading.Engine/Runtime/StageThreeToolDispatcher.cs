using System.Text;
using System.Text.Json;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;

namespace Trading.Engine.Runtime;

public static class ToolDispatchCodes
{
    public const string Succeeded = "tool_succeeded";
    public const string UnknownTool = "unknown_tool";
    public const string UnsupportedSchemaVersion = "unsupported_schema_version";
    public const string MalformedArguments = "malformed_arguments";
    public const string ArgumentsTooLarge = "arguments_too_large";
    public const string NonCanonicalArguments = "non_canonical_arguments";
    public const string ToolDisallowed = "tool_disallowed";
    public const string PerToolBudgetExceeded = "per_tool_budget_exceeded";
    public const string TotalToolBudgetExceeded = "total_tool_budget_exceeded";
    public const string RunMismatch = "run_mismatch";
    public const string RunNotActive = "run_not_active";
    public const string Cancelled = "cancelled";
    public const string FinishAlreadyCalled = "finish_already_called";
    public const string InvocationAlreadyExists = "invocation_already_exists";
    public const string PersistenceConflict = "persistence_conflict";
    public const string ToolFailed = "tool_failed";
}

public sealed class StageThreeToolDispatcher(
    IBotRunRepository runs,
    ITradingBotRepository bots,
    IBotRunInputService inputs,
    IUtcClock clock) : IToolDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };
    public const int SchemaVersion = 1;
    public const int MaximumArgumentsBytes = 16 * 1024;

    public static IReadOnlyList<ToolDefinition> Definitions { get; } =
    [
        new(StageThreeTools.GetPortfolioSnapshot, SchemaVersion,
            "{\"additionalProperties\":false,\"properties\":{\"snapshotId\":{\"type\":\"string\"}},\"required\":[\"snapshotId\"],\"type\":\"object\"}"),
        new(StageThreeTools.Finish, SchemaVersion,
            "{\"additionalProperties\":false,\"properties\":{\"nextRunAt\":{\"format\":\"date-time\",\"type\":[\"string\",\"null\"]},\"status\":{\"enum\":[\"Completed\",\"Incomplete\",\"Failed\"]},\"summary\":{\"minLength\":1,\"type\":\"string\"},\"wakeReason\":{\"type\":[\"string\",\"null\"]}},\"required\":[\"status\",\"summary\"],\"type\":\"object\"}"),
    ];

    public async Task<ToolDispatchResult> DispatchAsync(ToolDispatchContext context, ModelToolCall toolCall, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(toolCall);
        var auditToken = cancellationToken.IsCancellationRequested ? CancellationToken.None : cancellationToken;
        var run = await runs.GetAsync(context.RunId, auditToken).ConfigureAwait(false);
        if (run is null || run.TradingBotId != context.TradingBotId || run.PortfolioSnapshotId != context.SnapshotId)
            return Reject(toolCall, ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.RunMismatch);
        if (run.ToolInvocations.Any(x => x.Id == toolCall.InvocationId))
            return Reject(toolCall, ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.InvocationAlreadyExists);
        if (run.IsTerminal) return Reject(toolCall, ToolAuthorizationOutcome.Disallowed,
            run.FinishResult is null ? ToolDispatchCodes.RunNotActive : ToolDispatchCodes.FinishAlreadyCalled);
        if (run.Status != BotRunStatus.WaitingForTool)
            return Reject(toolCall, ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.RunNotActive);

        var bot = await bots.GetAsync(run.TradingBotId, auditToken).ConfigureAwait(false);
        var configuration = bot?.ConfigurationVersions.SingleOrDefault(x => x.Id == run.ConfigurationVersionId);
        if (configuration is null) return Reject(toolCall, ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.RunMismatch);

        var validation = Validate(toolCall, run, configuration.ToolPolicy, configuration.RunBudget, cancellationToken);
        var startedAt = clock.UtcNow;
        var invocation = run.StartToolInvocation(toolCall.InvocationId, NormalizeName(toolCall.Name), SafeArguments(toolCall.CanonicalArguments), startedAt);
        var startVersion = run.Version;
        if (await runs.SaveAsync(run, startVersion, auditToken).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded)
            return Reject(toolCall, ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.PersistenceConflict);

        if (validation is not null)
        {
            var usage = IncrementUsage(run.Usage, startedAt, clock.UtcNow);
            invocation.Fail(validation.Value.Code, usage, clock.UtcNow);
            if (!await SaveTerminalAsync(run, startVersion + 1, auditToken).ConfigureAwait(false))
                return Reject(toolCall, ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.PersistenceConflict);
            return Reject(toolCall, validation.Value.Outcome, validation.Value.Code);
        }

        try
        {
            var canonicalResult = toolCall.Name switch
            {
                StageThreeTools.GetPortfolioSnapshot => await ExecuteSnapshotAsync(run, toolCall, cancellationToken).ConfigureAwait(false),
                StageThreeTools.Finish => ExecuteFinish(run, toolCall),
                _ => throw new InvalidOperationException(ToolDispatchCodes.UnknownTool),
            };
            var usage = IncrementUsage(run.Usage, startedAt, clock.UtcNow);
            invocation.Complete(canonicalResult, usage, clock.UtcNow);
            if (toolCall.Name == StageThreeTools.Finish)
            {
                var arguments = ParseFinish(toolCall.CanonicalArguments);
                run.Complete(new FinishResult(arguments.Status, arguments.Summary, arguments.RequestedNextRunAt, arguments.WakeReason), usage, clock.UtcNow);
            }
            if (!await SaveTerminalAsync(run, startVersion + 1, cancellationToken).ConfigureAwait(false))
                return Reject(toolCall, ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.PersistenceConflict);
            return Result(toolCall, ToolExecutionOutcome.Succeeded, ToolAuthorizationOutcome.Authorized,
                ToolDispatchCodes.Succeeded, canonicalResult);
        }
        catch (OperationCanceledException)
        {
            invocation.Fail(ToolDispatchCodes.Cancelled, IncrementUsage(run.Usage, startedAt, clock.UtcNow), clock.UtcNow);
            await SaveTerminalAsync(run, startVersion + 1, CancellationToken.None).ConfigureAwait(false);
            return Reject(toolCall, ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.Cancelled);
        }
        catch (Exception)
        {
            invocation.Fail(ToolDispatchCodes.ToolFailed, IncrementUsage(run.Usage, startedAt, clock.UtcNow), clock.UtcNow);
            await SaveTerminalAsync(run, startVersion + 1, cancellationToken).ConfigureAwait(false);
            return Reject(toolCall, ToolAuthorizationOutcome.Authorized, ToolDispatchCodes.ToolFailed, ToolExecutionOutcome.Failed);
        }
    }

    private static (ToolAuthorizationOutcome Outcome, string Code)? Validate(ModelToolCall call, BotRun run,
        ToolPolicy policy, RunBudget budget, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return (ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.Cancelled);
        if (call.Name is not StageThreeTools.GetPortfolioSnapshot and not StageThreeTools.Finish)
            return (ToolAuthorizationOutcome.UnknownTool, ToolDispatchCodes.UnknownTool);
        if (call.SchemaVersion != SchemaVersion)
            return (ToolAuthorizationOutcome.UnsupportedSchemaVersion, ToolDispatchCodes.UnsupportedSchemaVersion);
        if (!policy.IsAllowed(call.Name)) return (ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.ToolDisallowed);
        if (run.ToolInvocations.Count >= budget.ToolCallLimit)
            return (ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.TotalToolBudgetExceeded);
        if (run.ToolInvocations.Count(x => x.ToolName == call.Name) >= policy.GetCallLimit(call.Name))
            return (ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.PerToolBudgetExceeded);
        if (run.FinishResult is not null || run.ToolInvocations.Any(x => x.ToolName == StageThreeTools.Finish))
            return (ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.FinishAlreadyCalled);
        if (Encoding.UTF8.GetByteCount(call.CanonicalArguments ?? string.Empty) > MaximumArgumentsBytes)
            return (ToolAuthorizationOutcome.InvalidArguments, ToolDispatchCodes.ArgumentsTooLarge);
        try
        {
            var arguments = call.CanonicalArguments ?? string.Empty;
            using var document = JsonDocument.Parse(arguments, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, MaxDepth = 8 });
            if (call.Name == StageThreeTools.GetPortfolioSnapshot)
            {
                var snapshot = ParseSnapshot(arguments);
                if (snapshot.SnapshotId != run.PortfolioSnapshotId)
                    return (ToolAuthorizationOutcome.Disallowed, ToolDispatchCodes.RunMismatch);
            }
            else ParseFinish(arguments);
            if (Canonicalize(document.RootElement) != arguments)
                return (ToolAuthorizationOutcome.InvalidArguments, ToolDispatchCodes.NonCanonicalArguments);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException or InvalidOperationException)
        { return (ToolAuthorizationOutcome.InvalidArguments, ToolDispatchCodes.MalformedArguments); }
        return null;
    }

    private async Task<string> ExecuteSnapshotAsync(BotRun run, ModelToolCall call, CancellationToken cancellationToken)
    {
        var arguments = ParseSnapshot(call.CanonicalArguments);
        if (arguments.SnapshotId != run.PortfolioSnapshotId) throw new InvalidOperationException("Pinned snapshot mismatch.");
        var snapshot = await inputs.GetPortfolioSnapshotAsync(run.Id, cancellationToken).ConfigureAwait(false);
        return Json(new Dictionary<string, object?> { ["content"] = JsonDocument.Parse(snapshot.CanonicalContent).RootElement.Clone(), ["contentHash"] = snapshot.ContentHash, ["schemaVersion"] = snapshot.SchemaVersion, ["snapshotId"] = snapshot.Snapshot.Id.ToString() });
    }

    private static string ExecuteFinish(BotRun run, ModelToolCall call)
    {
        var value = ParseFinish(call.CanonicalArguments);
        _ = new FinishResult(value.Status, value.Summary, value.RequestedNextRunAt, value.WakeReason);
        return Json(new Dictionary<string, object?> { ["accepted"] = true, ["status"] = value.Status.ToString() });
    }

    private async Task<bool> SaveTerminalAsync(BotRun run, long version, CancellationToken cancellationToken) =>
        await runs.SaveAsync(run, version, cancellationToken).ConfigureAwait(false) is PersistenceWriteResult.Succeeded;

    private static GetPortfolioSnapshotArguments ParseSnapshot(string json)
    {
        using var document = JsonDocument.Parse(json); var root = RequireObject(document.RootElement, ["snapshotId"]);
        return new GetPortfolioSnapshotArguments(PortfolioDecisionSnapshotId.Parse(root.GetProperty("snapshotId").GetString() ?? string.Empty));
    }

    private static FinishArguments ParseFinish(string json)
    {
        using var document = JsonDocument.Parse(json); var root = RequireObject(document.RootElement, ["status", "summary", "nextRunAt", "wakeReason"]);
        if (!root.TryGetProperty("status", out var statusNode) || statusNode.ValueKind != JsonValueKind.String ||
            !Enum.TryParse<FinishStatus>(statusNode.GetString(), false, out var status)) throw new JsonException();
        if (!root.TryGetProperty("summary", out var summaryNode) || summaryNode.ValueKind != JsonValueKind.String) throw new JsonException();
        var summary = summaryNode.GetString() ?? string.Empty; if (string.IsNullOrWhiteSpace(summary) || summary.Length > 4096) throw new JsonException();
        var next = OptionalUtc(root, "nextRunAt"); var wake = OptionalString(root, "wakeReason", 1024);
        if ((next is null) != (wake is null)) throw new JsonException();
        return new FinishArguments(status, summary, next, wake);
    }

    private static JsonElement RequireObject(JsonElement value, IReadOnlyCollection<string> allowed)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException();
        if (value.EnumerateObject().Any(x => !allowed.Contains(x.Name, StringComparer.Ordinal))) throw new JsonException();
        return value;
    }
    private static DateTimeOffset? OptionalUtc(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var node) || node.ValueKind == JsonValueKind.Null) return null;
        if (node.ValueKind != JsonValueKind.String || !DateTimeOffset.TryParseExact(node.GetString(), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var value)) throw new JsonException();
        return value.ToUniversalTime();
    }
    private static string? OptionalString(JsonElement root, string name, int max)
    {
        if (!root.TryGetProperty(name, out var node) || node.ValueKind == JsonValueKind.Null) return null;
        if (node.ValueKind != JsonValueKind.String) throw new JsonException(); var value = node.GetString();
        if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new JsonException(); return value;
    }
    private static string Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); break;
            default: element.WriteTo(writer); break;
        }
    }
    private static string Json(object value) => JsonSerializer.Serialize(value, SerializerOptions);
    private static string SafeArguments(string? value) => Encoding.UTF8.GetByteCount(value ?? string.Empty) <= MaximumArgumentsBytes ? value ?? "null" : "{\"redacted\":\"arguments_too_large\"}";
    private static string NormalizeName(string? value) => string.IsNullOrWhiteSpace(value) ? "invalid" : value.Length <= 128 ? value : value[..128];
    private static Usage IncrementUsage(Usage current, DateTimeOffset started, DateTimeOffset completed) => new(current.Elapsed + (completed - started), current.Tokens, current.Cost, current.ToolCalls + 1, current.ResearchRequests, current.Proposals);
    private static ToolDispatchResult Reject(ModelToolCall call, ToolAuthorizationOutcome authorization, string code, ToolExecutionOutcome outcome = ToolExecutionOutcome.Rejected) => Result(call, outcome, authorization, code, Json(new Dictionary<string, object?> { ["code"] = code }));
    private static ToolDispatchResult Result(ModelToolCall call, ToolExecutionOutcome outcome, ToolAuthorizationOutcome authorization, string code, string json) => new(new ModelToolResult(call.InvocationId, NormalizeName(call.Name), call.SchemaVersion, outcome, json), new ToolAuthorizationResult(authorization, code));
}
