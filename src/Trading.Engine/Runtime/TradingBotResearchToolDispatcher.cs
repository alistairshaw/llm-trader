using System.Text;
using System.Text.Json;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Research;

namespace Trading.Engine.Runtime;

public static class StageFourTradingTools
{
    public const string RequestResearch = nameof(RequestResearch);
    public const string ListReports = nameof(ListReports);
    public const string GetReport = nameof(GetReport);
}

public sealed class TradingBotResearchToolDispatcher(
    StageThreeToolDispatcher stageThree,
    IBotRunRepository runs,
    ITradingBotRepository bots,
    ResearchRequestService requests,
    IResearchReportCatalogQueries catalog,
    IUtcClock clock) : IToolDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };
    public const int SchemaVersion = 1;
    public const int MaximumArgumentsBytes = 16 * 1024;
    public static IReadOnlyList<ToolDefinition> Definitions { get; } =
    [
        .. StageThreeToolDispatcher.Definitions,
        new(StageFourTradingTools.RequestResearch, SchemaVersion, "{\"additionalProperties\":false,\"properties\":{\"asOf\":{\"format\":\"date-time\",\"type\":\"string\"},\"desiredSections\":{\"items\":{\"type\":\"string\"},\"maxItems\":20,\"minItems\":1,\"type\":\"array\"},\"maximumAgeDays\":{\"maximum\":365,\"minimum\":1,\"type\":\"integer\"},\"question\":{\"maxLength\":4000,\"minLength\":1,\"type\":\"string\"},\"requiredSourceTypes\":{\"items\":{\"type\":\"string\"},\"maxItems\":20,\"minItems\":1,\"type\":\"array\"},\"subject\":{\"maxLength\":300,\"minLength\":1,\"type\":\"string\"},\"visibility\":{\"enum\":[\"Shared\",\"BotPrivate\"]}},\"required\":[\"asOf\",\"desiredSections\",\"maximumAgeDays\",\"question\",\"requiredSourceTypes\",\"subject\",\"visibility\"],\"type\":\"object\"}"),
        new(StageFourTradingTools.ListReports, SchemaVersion, "{\"additionalProperties\":false,\"properties\":{\"freshOnly\":{\"type\":\"boolean\"},\"offset\":{\"minimum\":0,\"type\":\"integer\"},\"size\":{\"maximum\":100,\"minimum\":1,\"type\":\"integer\"},\"subject\":{\"type\":[\"string\",\"null\"]}},\"required\":[\"freshOnly\",\"offset\",\"size\",\"subject\"],\"type\":\"object\"}"),
        new(StageFourTradingTools.GetReport, SchemaVersion, "{\"additionalProperties\":false,\"properties\":{\"reportId\":{\"type\":\"string\"},\"seriesId\":{\"type\":\"string\"},\"version\":{\"minimum\":1,\"type\":\"integer\"}},\"required\":[\"reportId\",\"seriesId\",\"version\"],\"type\":\"object\"}"),
    ];
    IReadOnlyList<ToolDefinition> IToolDispatcher.Definitions => Definitions;

    public async Task<ToolDispatchResult> DispatchAsync(ToolDispatchContext context, ModelToolCall toolCall, CancellationToken cancellationToken)
    {
        var call = toolCall;
        var token = cancellationToken;
        if (!IsResearch(call.Name)) return await stageThree.DispatchAsync(context, call, token).ConfigureAwait(false);
        var auditToken = token.IsCancellationRequested ? CancellationToken.None : token;
        var run = await runs.GetAsync(context.RunId, auditToken).ConfigureAwait(false);
        if (run is null || run.TradingBotId != context.TradingBotId || run.PortfolioSnapshotId != context.SnapshotId)
            return Reject(call, ToolDispatchCodes.RunMismatch);
        if (run.ToolInvocations.Any(x => x.Id == call.InvocationId)) return Reject(call, ToolDispatchCodes.InvocationAlreadyExists);
        if (run.Status != BotRunStatus.WaitingForTool || run.IsTerminal) return Reject(call, ToolDispatchCodes.RunNotActive);
        var bot = await bots.GetAsync(run.TradingBotId, auditToken).ConfigureAwait(false);
        var configuration = bot?.ConfigurationVersions.SingleOrDefault(x => x.Id == run.ConfigurationVersionId);
        if (configuration is null) return Reject(call, ToolDispatchCodes.RunMismatch);
        var error = Validate(call, run, configuration);
        var started = clock.UtcNow;
        var invocation = run.StartToolInvocation(call.InvocationId, call.Name, Safe(call.CanonicalArguments), started);
        var expected = run.Version;
        if (await runs.SaveAsync(run, expected, auditToken).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded)
            return Reject(call, ToolDispatchCodes.PersistenceConflict);
        if (error is not null) return await FailAsync(run, invocation, expected + 1, call, error, auditToken).ConfigureAwait(false);
        try
        {
            var result = call.Name switch
            {
                StageFourTradingTools.RequestResearch => await RequestAsync(run, configuration, call, token).ConfigureAwait(false),
                StageFourTradingTools.ListReports => await ListAsync(run.TradingBotId, call, token).ConfigureAwait(false),
                StageFourTradingTools.GetReport => await GetAsync(run.TradingBotId, call, token).ConfigureAwait(false),
                _ => throw new InvalidOperationException(),
            };
            var usage = UsageAfter(run, call.Name == StageFourTradingTools.RequestResearch, started);
            invocation.Complete(result, usage, clock.UtcNow);
            run.RecordModelProgress(run.ModelTranscriptSchemaVersion, run.ModelTranscriptJson, usage);
            if (await runs.SaveAsync(run, expected + 1, token).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded)
                return Reject(call, ToolDispatchCodes.PersistenceConflict);
            return Result(call, ToolExecutionOutcome.Succeeded, ToolAuthorizationOutcome.Authorized, ToolDispatchCodes.Succeeded, result);
        }
        catch (OperationCanceledException) { return await FailAsync(run, invocation, expected + 1, call, ToolDispatchCodes.Cancelled, CancellationToken.None).ConfigureAwait(false); }
        catch (UnauthorizedAccessException) { return await FailAsync(run, invocation, expected + 1, call, "research_access_denied", auditToken).ConfigureAwait(false); }
        catch (Exception) { return await FailAsync(run, invocation, expected + 1, call, ToolDispatchCodes.ToolFailed, auditToken).ConfigureAwait(false); }
    }

    private async Task<string> RequestAsync(BotRun run, TradingBotConfigurationVersion configuration, ModelToolCall call, CancellationToken token)
    {
        var x = ParseRequest(call.CanonicalArguments);
        var principal = Principal(run.TradingBotId);
        var remainingTools = Math.Max(1, configuration.RunBudget.ToolCallLimit - run.Usage.ToolCalls);
        var result = await requests.SubmitAsync(new(principal, run.TradingBotId, x.Subject, x.Question, x.Sections,
            x.Sources, x.AsOf, x.Visibility, null, null, TimeSpan.FromDays(x.MaximumAgeDays), "1", "1",
            new ResearchBudget(configuration.RunBudget.WallClock, Math.Max(1, configuration.RunBudget.TokenLimit - run.Usage.Tokens),
                new Money(Math.Max(0, configuration.RunBudget.CostLimit.Amount - run.Usage.Cost.Amount), configuration.RunBudget.CostLimit.Currency),
                remainingTools, Math.Min(20, remainingTools), 100_000, 3), ["approved-fixtures"]), token).ConfigureAwait(false);
        if (result.Decision == ResearchRequestDecision.Rejected) throw new UnauthorizedAccessException(result.Code);
        return Json(new SortedDictionary<string, object?>
        {
            ["code"] = result.Code,
            ["decision"] = result.Decision.ToString(),
            ["normalizedKey"] = result.NormalizedKey,
            ["reportId"] = result.ReportId?.ToString(),
            ["requestId"] = result.RequestId?.ToString(),
            ["subscriptionId"] = result.SubscriptionId?.ToString()
        });
    }

    private async Task<string> ListAsync(TradingBotId botId, ModelToolCall call, CancellationToken token)
    {
        var x = ParseList(call.CanonicalArguments);
        var values = await catalog.SearchAsync(new(Principal(botId), clock.UtcNow, x.Subject, FreshOnly: x.FreshOnly, Offset: x.Offset, Size: x.Size), token).ConfigureAwait(false);
        return Json(new SortedDictionary<string, object?>
        {
            ["reports"] = values.Select(v => new SortedDictionary<string, object?>
            {
                ["dataCutoff"] = Timestamp(v.DataCutoff),
                ["expiresAt"] = Timestamp(v.ExpiresAt),
                ["generatedAt"] = Timestamp(v.GeneratedAt),
                ["isFresh"] = v.IsFresh,
                ["reportId"] = v.Id.ToString(),
                ["seriesId"] = v.SeriesId,
                ["status"] = v.Status.ToString(),
                ["subject"] = v.Subject,
                ["version"] = v.Version
            }).ToArray()
        });
    }

    private async Task<string> GetAsync(TradingBotId botId, ModelToolCall call, CancellationToken token)
    {
        var x = ParseGet(call.CanonicalArguments);
        var report = await catalog.GetAuthorizedVersionAsync(Principal(botId), x.SeriesId, x.Version, token).ConfigureAwait(false);
        if (report is null || report.Id != x.ReportId) throw new UnauthorizedAccessException();
        return Json(new SortedDictionary<string, object?>
        {
            ["content"] = JsonDocument.Parse(report.Content).RootElement.Clone(),
            ["contentHash"] = report.ContentHash,
            ["dataCutoff"] = Timestamp(report.DataCutoff),
            ["expiresAt"] = Timestamp(report.ExpiresAt),
            ["generatedAt"] = Timestamp(report.GeneratedAt),
            ["isFresh"] = report.IsFreshAt(clock.UtcNow),
            ["provenance"] = report.Provenance.Sources.Select(s => new SortedDictionary<string, object?>
            {
                ["contentHash"] = s.ContentHash,
                ["provider"] = s.Provider,
                ["publishedAt"] = s.PublishedAt is null ? null : Timestamp(s.PublishedAt.Value),
                ["retrievedAt"] = Timestamp(s.RetrievedAt),
                ["sourceIdentifier"] = s.SourceIdentifier
            }).ToArray(),
            ["reportId"] = report.Id.ToString(),
            ["seriesId"] = report.ReportSeriesId,
            ["status"] = report.Status.ToString(),
            ["version"] = report.VersionNumber
        });
    }

    private static string? Validate(ModelToolCall call, BotRun run, TradingBotConfigurationVersion config)
    {
        if (call.SchemaVersion != SchemaVersion) return ToolDispatchCodes.UnsupportedSchemaVersion;
        if (!config.ToolPolicy.IsAllowed(call.Name)) return ToolDispatchCodes.ToolDisallowed;
        if (run.ToolInvocations.Count >= config.RunBudget.ToolCallLimit) return ToolDispatchCodes.TotalToolBudgetExceeded;
        if (run.ToolInvocations.Count(x => x.ToolName == call.Name) >= config.ToolPolicy.GetCallLimit(call.Name)) return ToolDispatchCodes.PerToolBudgetExceeded;
        if (call.Name == StageFourTradingTools.RequestResearch && run.Usage.ResearchRequests >= config.RunBudget.ResearchRequestLimit) return "research_request_budget_exceeded";
        if (Encoding.UTF8.GetByteCount(call.CanonicalArguments ?? "") > MaximumArgumentsBytes) return ToolDispatchCodes.ArgumentsTooLarge;
        try { var arguments = call.CanonicalArguments ?? string.Empty; using var d = JsonDocument.Parse(arguments); object parsed = call.Name switch { StageFourTradingTools.RequestResearch => ParseRequest(arguments), StageFourTradingTools.ListReports => ParseList(arguments), _ => ParseGet(arguments) }; _ = parsed; if (Canonical(d.RootElement) != arguments) return ToolDispatchCodes.NonCanonicalArguments; }
        catch { return ToolDispatchCodes.MalformedArguments; }
        return null;
    }

    private async Task<ToolDispatchResult> FailAsync(BotRun run, ToolInvocation invocation, long version, ModelToolCall call, string code, CancellationToken token)
    {
        var usage = UsageAfter(run, false, clock.UtcNow);
        invocation.Fail(code, usage, clock.UtcNow);
        run.RecordModelProgress(run.ModelTranscriptSchemaVersion, run.ModelTranscriptJson, usage);
        await runs.SaveAsync(run, version, token).ConfigureAwait(false);
        return Reject(call, code);
    }
    private Usage UsageAfter(BotRun run, bool research, DateTimeOffset started) => new(run.Usage.Elapsed + (clock.UtcNow - started), run.Usage.Tokens, run.Usage.Cost, run.Usage.ToolCalls + 1, run.Usage.ResearchRequests + (research ? 1 : 0), run.Usage.Proposals);
    private static ResearchPrincipal Principal(TradingBotId id) => new(id.ToString(), ResearchPrincipalKind.TradingBot);
    private static bool IsResearch(string name) => name is StageFourTradingTools.RequestResearch or StageFourTradingTools.ListReports or StageFourTradingTools.GetReport;
    private static RequestArgs ParseRequest(string json) { using var d = JsonDocument.Parse(json); var r = Object(d.RootElement, ["asOf", "desiredSections", "maximumAgeDays", "question", "requiredSourceTypes", "subject", "visibility"]); var vis = Enum.Parse<ResearchVisibility>(String(r, "visibility", 20), false); if (vis == ResearchVisibility.Restricted) throw new JsonException(); return new(String(r, "subject", 300), String(r, "question", 4000), Strings(r, "desiredSections"), Strings(r, "requiredSourceTypes"), Utc(r, "asOf"), Int(r, "maximumAgeDays", 1, 365), vis); }
    private static ListArgs ParseList(string json) { using var d = JsonDocument.Parse(json); var r = Object(d.RootElement, ["freshOnly", "offset", "size", "subject"]); return new(OptionalString(r, "subject", 300), Bool(r, "freshOnly"), Int(r, "offset", 0, int.MaxValue), Int(r, "size", 1, 100)); }
    private static GetArgs ParseGet(string json) { using var d = JsonDocument.Parse(json); var r = Object(d.RootElement, ["reportId", "seriesId", "version"]); return new(ResearchReportId.Parse(String(r, "reportId", 100)), String(r, "seriesId", 200), Int(r, "version", 1, int.MaxValue)); }
    private static JsonElement Object(JsonElement e, string[] names) { if (e.ValueKind != JsonValueKind.Object || e.EnumerateObject().Any(p => !names.Contains(p.Name, StringComparer.Ordinal)) || names.Any(n => !e.TryGetProperty(n, out _))) throw new JsonException(); return e; }
    private static string String(JsonElement r, string n, int max) { var e = r.GetProperty(n); if (e.ValueKind != JsonValueKind.String) throw new JsonException(); var v = e.GetString() ?? ""; if (string.IsNullOrWhiteSpace(v) || v.Length > max) throw new JsonException(); return v; }
    private static string? OptionalString(JsonElement r, string n, int max) => r.GetProperty(n).ValueKind == JsonValueKind.Null ? null : String(r, n, max);
    private static string[] Strings(JsonElement r, string n) { var e = r.GetProperty(n); if (e.ValueKind != JsonValueKind.Array) throw new JsonException(); var a = e.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : throw new JsonException()).ToArray(); if (a.Length is < 1 or > 20 || a.Any(string.IsNullOrWhiteSpace)) throw new JsonException(); return a; }
    private static int Int(JsonElement r, string n, int min, int max) { var e = r.GetProperty(n); if (!e.TryGetInt32(out var v) || v < min || v > max) throw new JsonException(); return v; }
    private static bool Bool(JsonElement r, string n) { var e = r.GetProperty(n); return e.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw new JsonException() }; }
    private static DateTimeOffset Utc(JsonElement r, string n) { if (!DateTimeOffset.TryParseExact(String(r, n, 40), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var v)) throw new JsonException(); return v.ToUniversalTime(); }
    private static string Canonical(JsonElement e) { using var s = new MemoryStream(); using (var w = new Utf8JsonWriter(s)) Write(w, e); return Encoding.UTF8.GetString(s.ToArray()); }
    private static void Write(Utf8JsonWriter w, JsonElement e) { if (e.ValueKind == JsonValueKind.Object) { w.WriteStartObject(); foreach (var p in e.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { w.WritePropertyName(p.Name); Write(w, p.Value); } w.WriteEndObject(); } else if (e.ValueKind == JsonValueKind.Array) { w.WriteStartArray(); foreach (var x in e.EnumerateArray()) Write(w, x); w.WriteEndArray(); } else e.WriteTo(w); }
    private static string Json(object o) => JsonSerializer.Serialize(o, SerializerOptions);
    private static string Timestamp(DateTimeOffset x) => x.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
    private static string Safe(string? x) => Encoding.UTF8.GetByteCount(x ?? "") <= MaximumArgumentsBytes ? x ?? "null" : "{\"redacted\":\"arguments_too_large\"}";
    private static ToolDispatchResult Reject(ModelToolCall c, string code) => Result(c, ToolExecutionOutcome.Rejected, ToolAuthorizationOutcome.Disallowed, code, Json(new SortedDictionary<string, object?> { { "code", code } }));
    private static ToolDispatchResult Result(ModelToolCall c, ToolExecutionOutcome e, ToolAuthorizationOutcome a, string code, string json) => new(new(c.InvocationId, c.Name, c.SchemaVersion, e, json), new(a, code));
    private sealed record RequestArgs(string Subject, string Question, string[] Sections, string[] Sources, DateTimeOffset AsOf, int MaximumAgeDays, ResearchVisibility Visibility);
    private sealed record ListArgs(string? Subject, bool FreshOnly, int Offset, int Size);
    private sealed record GetArgs(ResearchReportId ReportId, string SeriesId, int Version);
}
