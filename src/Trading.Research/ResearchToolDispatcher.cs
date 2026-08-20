using System.Globalization;
using System.Text;
using System.Text.Json;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Research;
using Trading.Research.Contracts;
using Trading.Research.Sources;

namespace Trading.Research;

public static class StageFourResearchTools
{
    public const int SchemaVersion = 1;
    public const string SearchWeb = "SearchWeb";
    public const string FetchWebDocument = "FetchWebDocument";
    public const string ListReports = "ListReports";
    public const string GetReport = "GetReport";
    public const string PublishReportDraft = "PublishReportDraft";
    public const string FinishResearch = "FinishResearch";
    public static readonly IReadOnlyList<string> Names =
        [SearchWeb, FetchWebDocument, ListReports, GetReport, PublishReportDraft, FinishResearch];
}

public sealed record ResearchToolPolicy
{
    private readonly Dictionary<string, int> limits;

    public ResearchToolPolicy(string toolSetVersion, IEnumerable<KeyValuePair<string, int>> limits, int maximumArgumentBytes = 65_536,
        int maximumResultBytes = 131_072)
    {
        ToolSetVersion = Required(toolSetVersion, nameof(toolSetVersion));
        ArgumentNullException.ThrowIfNull(limits);
        var values = limits.ToArray();
        if (values.Any(x => !StageFourResearchTools.Names.Contains(x.Key, StringComparer.Ordinal) || x.Value < 0) ||
            values.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Tool limits must uniquely name registered Research tools and be non-negative.", nameof(limits));
        this.limits = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumArgumentBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResultBytes);
        MaximumArgumentBytes = maximumArgumentBytes;
        MaximumResultBytes = maximumResultBytes;
    }

    public string ToolSetVersion { get; }
    public int MaximumArgumentBytes { get; }
    public int MaximumResultBytes { get; }
    public int Limit(string name) => limits.TryGetValue(name, out var value) ? value : 0;
    private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", name) : value;
}

public sealed class ResearchToolDispatcher(
    IFixtureResearchSource source,
    IResearchReportCatalog catalog,
    IResearchArtifactStore artifacts,
    IResearchRunAttemptRepository attempts,
    IResearchClock clock,
    ResearchToolPolicy policy) : IResearchToolDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IReadOnlyList<ResearchToolDefinition> definitions = BuildDefinitions();

    public IReadOnlyList<ResearchToolDefinition> Definitions => definitions;

    public async Task<ResearchToolResult> DispatchAsync(ResearchRunAttempt attempt, ResearchPrincipal principal,
        ResearchToolCall toolCall, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(toolCall);
        var started = clock.UtcNow;
        var history = await attempts.GetToolAuditAsync(attempt.Id, CancellationToken.None).ConfigureAwait(false);
        var sequence = history.Count + 1;
        var safeName = SafeName(toolCall.Name);
        var safeArguments = BoundedArguments(toolCall.CanonicalArguments, policy.MaximumArgumentBytes);
        var denial = ValidateEnvelope(attempt, principal, toolCall, history, cancellationToken);
        ResearchToolResult result;
        string status;
        string? error;
        try
        {
            if (denial is not null)
            {
                result = Failure(toolCall, denial);
                status = denial == ResearchResultCodes.Cancelled ? "Cancelled" : "Rejected";
                error = denial;
            }
            else
            {
                result = await ExecuteAsync(attempt, principal, toolCall, history, cancellationToken).ConfigureAwait(false);
                status = result.Succeeded ? "Succeeded" : result.ResultCode == ResearchResultCodes.SourceCancelled ? "Cancelled" : "Failed";
                error = result.Succeeded ? null : result.ResultCode;
            }
        }
        catch (OperationCanceledException)
        {
            result = Failure(toolCall, ResearchResultCodes.Cancelled);
            status = "Cancelled";
            error = ResearchResultCodes.Cancelled;
        }
        catch (Exception)
        {
            result = Failure(toolCall, ResearchResultCodes.ProviderFailed);
            status = "Failed";
            error = ResearchResultCodes.ProviderFailed;
        }

        var completed = clock.UtcNow < started ? started : clock.UtcNow;
        if (Encoding.UTF8.GetByteCount(result.CanonicalPayload) > policy.MaximumResultBytes)
        {
            result = Failure(toolCall, ResearchResultCodes.SourceOversized) with { Usage = result.Usage };
            status = "Failed";
            error = ResearchResultCodes.SourceOversized;
        }
        var usage = new ResearchUsage(completed - started, result.Usage.Tokens, result.Usage.Cost, result.Usage.ToolCalls,
            result.Usage.Documents, result.Usage.RetainedBytes, result.Usage.ConsecutiveFailures);
        result = result with { Usage = usage };
        var audit = new ResearchToolAudit(Guid.NewGuid().ToString("N"), attempt.Id, sequence, safeName, toolCall.SchemaVersion,
            safeArguments, status, started, completed, result.CanonicalPayload, error, error is null ? null : "redacted", Canonical(usage));
        var saved = await attempts.AppendToolAuditAsync(audit, CancellationToken.None).ConfigureAwait(false);
        if (saved is not PersistenceWriteResult.Succeeded)
            return Failure(toolCall, ResearchResultCodes.ProviderFailed) with { Usage = usage };
        return result;
    }

    private string? ValidateEnvelope(ResearchRunAttempt attempt, ResearchPrincipal principal, ResearchToolCall call,
        IReadOnlyList<ResearchToolAudit> history, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return ResearchResultCodes.Cancelled;
        if (attempt.Status is not ResearchRunAttemptStatus.Running and not ResearchRunAttemptStatus.WaitingForTool)
            return ResearchResultCodes.Unauthorized;
        if (!string.Equals(attempt.Versions.ToolSetVersion, policy.ToolSetVersion, StringComparison.Ordinal)) return ResearchResultCodes.Unauthorized;
        if (principal.Kind is not ResearchPrincipalKind.TradingBot and not ResearchPrincipalKind.User and not ResearchPrincipalKind.Administrator)
            return ResearchResultCodes.Unauthorized;
        if (!StageFourResearchTools.Names.Contains(call.Name, StringComparer.Ordinal)) return ResearchResultCodes.UnknownTool;
        if (call.SchemaVersion != StageFourResearchTools.SchemaVersion) return ResearchResultCodes.ToolSchemaInvalid;
        if (history.Any(IsFinish)) return ResearchResultCodes.Unauthorized;
        if (history.Count >= attempt.Budget.ToolCallLimit) return ResearchResultCodes.ToolBudgetExceeded;
        if (history.Count(x => string.Equals(x.ToolName, call.Name, StringComparison.Ordinal)) >= policy.Limit(call.Name))
            return ResearchResultCodes.ToolBudgetExceeded;
        if (Encoding.UTF8.GetByteCount(call.CanonicalArguments ?? string.Empty) > policy.MaximumArgumentBytes)
            return ResearchResultCodes.ToolSchemaInvalid;
        try
        {
            var arguments = call.CanonicalArguments ?? string.Empty;
            using var document = JsonDocument.Parse(arguments, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 16 });
            if (!string.Equals(Canonical(document.RootElement), arguments, StringComparison.Ordinal)) return ResearchResultCodes.ToolSchemaInvalid;
            ValidateArguments(attempt, call.Name, document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException or InvalidOperationException)
        {
            return ResearchResultCodes.ToolSchemaInvalid;
        }
        return null;
    }

    private async Task<ResearchToolResult> ExecuteAsync(ResearchRunAttempt attempt, ResearchPrincipal principal, ResearchToolCall call,
        IReadOnlyList<ResearchToolAudit> history, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(call.CanonicalArguments);
        var root = document.RootElement;
        switch (call.Name)
        {
            case StageFourResearchTools.SearchWeb:
                {
                    var query = new ResearchSourceQuery(root.GetProperty("provider").GetString()!, root.GetProperty("query").GetString()!,
                        ParseUtc(root.GetProperty("asOf").GetString()!));
                    var found = await source.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                    var payload = Canonical(new { resultCode = found.ResultCode, sources = found.Sources });
                    return Result(call, found.ResultCode == ResearchResultCodes.Success, found.ResultCode, payload, Usage());
                }
            case StageFourResearchTools.FetchWebDocument:
                {
                    var remainingDocuments = attempt.Budget.DocumentLimit - SuccessfulFetches(history);
                    var remainingBytes = attempt.Budget.RetainedByteLimit - RetainedBytes(history);
                    if (remainingDocuments <= 0 || remainingBytes <= 0) return Failure(call, ResearchResultCodes.ToolBudgetExceeded);
                    var requested = root.GetProperty("maximumBytes").GetInt32();
                    var maximum = (int)Math.Min(requested, Math.Min(remainingBytes, int.MaxValue));
                    var fetched = await source.RetrieveAsync(root.GetProperty("provider").GetString()!, root.GetProperty("sourceIdentifier").GetString()!, maximum, cancellationToken).ConfigureAwait(false);
                    if (fetched.Document is null) return Failure(call, fetched.ResultCode);
                    var payload = Canonical(new { resultCode = fetched.ResultCode, document = fetched.Document });
                    return Result(call, true, fetched.ResultCode, payload, Usage(documents: 1, bytes: fetched.Document.ByteCount));
                }
            case StageFourResearchTools.ListReports:
                {
                    var values = await catalog.ListAsync(new ResearchCatalogQuery(principal, OptionalString(root, "subject"), OptionalString(root, "normalizedKey"), null, clock.UtcNow), cancellationToken).ConfigureAwait(false);
                    return Result(call, true, ResearchResultCodes.Success, Canonical(new { reports = values }), Usage());
                }
            case StageFourResearchTools.GetReport:
                {
                    var report = await catalog.GetAsync(principal, ResearchReportId.Parse(root.GetProperty("reportId").GetString()!), cancellationToken).ConfigureAwait(false);
                    if (report is null) return Failure(call, ResearchResultCodes.Unauthorized);
                    return Result(call, true, ResearchResultCodes.Success, Canonical(new { report }), Usage());
                }
            case StageFourResearchTools.PublishReportDraft:
                {
                    var citations = ParseCitations(root.GetProperty("citations"));
                    var retrieved = RetrievedCitations(history);
                    if (citations.Length == 0 || citations.Any(citation => !retrieved.Contains(CitationKey(citation))))
                        return Failure(call, ResearchResultCodes.CitationInvalid);
                    var draft = new ResearchReportDraft(root.GetProperty("content").GetRawText(), citations,
                        ParseUtc(root.GetProperty("dataCutoff").GetString()!), OptionalUtc(root, "recommendedRefreshAt"));
                    await artifacts.WriteDraftAsync(attempt.Id, draft, cancellationToken).ConfigureAwait(false);
                    return Result(call, true, ResearchResultCodes.Success, Canonical(new { accepted = true, citationCount = citations.Length }), Usage());
                }
            case StageFourResearchTools.FinishResearch:
                return Result(call, true, ResearchResultCodes.Success, Canonical(new
                {
                    accepted = true,
                    status = root.GetProperty("status").GetString(),
                    summary = root.GetProperty("summary").GetString(),
                    recommendedRefreshAt = OptionalUtc(root, "recommendedRefreshAt")
                }), Usage());
            default:
                return Failure(call, ResearchResultCodes.UnknownTool);
        }
    }

    private static void ValidateArguments(ResearchRunAttempt attempt, string name, JsonElement root)
    {
        RequireObject(root);
        RequireExactRun(root, attempt.Id);
        switch (name)
        {
            case StageFourResearchTools.SearchWeb:
                RequireProperties(root, "asOf", "attemptId", "provider", "query");
                RequireString(root, "provider", 200); RequireString(root, "query", 2000); _ = ParseUtc(RequireString(root, "asOf", 40)); break;
            case StageFourResearchTools.FetchWebDocument:
                RequireProperties(root, "attemptId", "maximumBytes", "provider", "sourceIdentifier");
                RequireString(root, "provider", 200); RequireString(root, "sourceIdentifier", 2000);
                if (root.GetProperty("maximumBytes").ValueKind != JsonValueKind.Number || !root.GetProperty("maximumBytes").TryGetInt32(out var bytes) || bytes <= 0) throw new JsonException(); break;
            case StageFourResearchTools.ListReports:
                RequireProperties(root, "attemptId", "normalizedKey", "subject"); OptionalBoundedString(root, "subject", 300); OptionalBoundedString(root, "normalizedKey", 256); break;
            case StageFourResearchTools.GetReport:
                RequireProperties(root, "attemptId", "reportId"); _ = ResearchReportId.Parse(RequireString(root, "reportId", 64)); break;
            case StageFourResearchTools.PublishReportDraft:
                RequireProperties(root, "attemptId", "citations", "content", "dataCutoff", "recommendedRefreshAt");
                if (root.GetProperty("content").ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(root.GetProperty("content").GetRawText()) > 100_000) throw new JsonException();
                _ = ParseUtc(RequireString(root, "dataCutoff", 40)); _ = OptionalUtc(root, "recommendedRefreshAt"); _ = ParseCitations(root.GetProperty("citations")); break;
            case StageFourResearchTools.FinishResearch:
                RequireProperties(root, "attemptId", "recommendedRefreshAt", "status", "summary");
                var status = RequireString(root, "status", 40); if (status is not "Completed" and not "Failed") throw new JsonException();
                RequireString(root, "summary", 4096); _ = OptionalUtc(root, "recommendedRefreshAt"); break;
        }
    }

    private static SourceCitation[] ParseCitations(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 100) throw new JsonException();
        return value.EnumerateArray().Select(item =>
        {
            RequireObject(item); RequireProperties(item, "contentHash", "provider", "publishedAt", "retrievedAt", "sourceIdentifier");
            return new SourceCitation(RequireString(item, "provider", 200), RequireString(item, "sourceIdentifier", 2000),
                OptionalUtc(item, "publishedAt"), ParseUtc(RequireString(item, "retrievedAt", 40)), RequireString(item, "contentHash", 256));
        }).ToArray();
    }

    private static HashSet<string> RetrievedCitations(IEnumerable<ResearchToolAudit> history)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var audit in history.Where(x => x.ToolName == StageFourResearchTools.FetchWebDocument && x.Status == "Succeeded" && x.ResultJson is not null))
        {
            try
            {
                using var value = JsonDocument.Parse(audit.ResultJson!); var document = value.RootElement.GetProperty("document");
                DateTimeOffset? published = document.GetProperty("publishedAt").ValueKind == JsonValueKind.Null ? null :
                    DateTimeOffset.Parse(document.GetProperty("publishedAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
                var retrieved = DateTimeOffset.Parse(document.GetProperty("retrievedAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
                var citation = new SourceCitation(document.GetProperty("provider").GetString()!, document.GetProperty("sourceIdentifier").GetString()!,
                    published, retrieved, document.GetProperty("contentHash").GetString()!);
                found.Add(CitationKey(citation));
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException) { }
        }
        return found;
    }

    private static string CitationKey(SourceCitation value) => string.Join('|', value.Provider, value.SourceIdentifier, value.PublishedAt?.ToString("O"), value.RetrievedAt.ToString("O"), value.ContentHash);
    private static int SuccessfulFetches(IEnumerable<ResearchToolAudit> history) => history.Count(x => x.ToolName == StageFourResearchTools.FetchWebDocument && x.Status == "Succeeded");
    private static long RetainedBytes(IEnumerable<ResearchToolAudit> history) => history.Where(x => x.ToolName == StageFourResearchTools.FetchWebDocument && x.Status == "Succeeded" && x.UsageJson is not null).Sum(x => ReadRetainedBytes(x.UsageJson!));
    private static long ReadRetainedBytes(string json) { try { using var value = JsonDocument.Parse(json); return value.RootElement.GetProperty("retainedBytes").GetInt64(); } catch (JsonException) { return 0; } }
    private static bool IsFinish(ResearchToolAudit audit) => audit.ToolName == StageFourResearchTools.FinishResearch && audit.Status == "Succeeded";
    private static ResearchUsage Usage(int documents = 0, long bytes = 0) => new(TimeSpan.Zero, 0, new Money(0, Currency.USD), 1, documents, bytes, 0);
    private static ResearchToolResult Failure(ResearchToolCall call, string code) => Result(call, false, code, Canonical(new { code }), Usage());
    private static ResearchToolResult Result(ResearchToolCall call, bool succeeded, string code, string payload, ResearchUsage usage) => new(call.CallId, succeeded, code, payload, usage);
    private static string SafeName(string? value) => string.IsNullOrWhiteSpace(value) ? "invalid" : value.Length <= 128 ? value : value[..128];
    private static string BoundedArguments(string? value, int maximum) => Encoding.UTF8.GetByteCount(value ?? string.Empty) <= maximum ? value ?? "null" : Canonical(new { redacted = "arguments_too_large" });
    private static void RequireExactRun(JsonElement root, ResearchRunAttemptId expected) { if (!string.Equals(RequireString(root, "attemptId", 64), expected.ToString(), StringComparison.Ordinal)) throw new InvalidOperationException(); }
    private static void RequireObject(JsonElement root) { if (root.ValueKind != JsonValueKind.Object) throw new JsonException(); }
    private static void RequireProperties(JsonElement root, params string[] names) { var actual = root.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal); if (!actual.SequenceEqual(names.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new JsonException(); }
    private static string RequireString(JsonElement root, string name, int maximum) { if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > maximum) throw new JsonException(); return value.GetString()!; }
    private static string? OptionalString(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static void OptionalBoundedString(JsonElement root, string name, int maximum) { if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return; if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > maximum) throw new JsonException(); }
    private static DateTimeOffset ParseUtc(string value) { if (!DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)) throw new FormatException(); return parsed.ToUniversalTime(); }
    private static DateTimeOffset? OptionalUtc(JsonElement root, string name) { if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null; if (value.ValueKind != JsonValueKind.String) throw new JsonException(); return ParseUtc(value.GetString()!); }
    private static string Canonical(object value) { using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions)); return Canonical(document.RootElement); }
    private static string Canonical(JsonElement value) { using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, value); return Encoding.UTF8.GetString(stream.ToArray()); }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value) { switch (value.ValueKind) { case JsonValueKind.Object: writer.WriteStartObject(); foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); } writer.WriteEndObject(); break; case JsonValueKind.Array: writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); break; default: value.WriteTo(writer); break; } }
    private static ResearchToolDefinition[] BuildDefinitions() =>
    [
        Definition(StageFourResearchTools.SearchWeb, ["asOf", "attemptId", "provider", "query"],
            ("asOf", "string"), ("attemptId", "string"), ("provider", "string"), ("query", "string")),
        Definition(StageFourResearchTools.FetchWebDocument, ["attemptId", "maximumBytes", "provider", "sourceIdentifier"],
            ("attemptId", "string"), ("maximumBytes", "integer"), ("provider", "string"), ("sourceIdentifier", "string")),
        Definition(StageFourResearchTools.ListReports, ["attemptId", "normalizedKey", "subject"],
            ("attemptId", "string"), ("normalizedKey", "string|null"), ("subject", "string|null")),
        Definition(StageFourResearchTools.GetReport, ["attemptId", "reportId"], ("attemptId", "string"), ("reportId", "string")),
        Definition(StageFourResearchTools.PublishReportDraft, ["attemptId", "citations", "content", "dataCutoff", "recommendedRefreshAt"],
            ("attemptId", "string"), ("citations", "array"), ("content", "object"), ("dataCutoff", "string"), ("recommendedRefreshAt", "string|null")),
        Definition(StageFourResearchTools.FinishResearch, ["attemptId", "recommendedRefreshAt", "status", "summary"],
            ("attemptId", "string"), ("recommendedRefreshAt", "string|null"), ("status", "string"), ("summary", "string")),
    ];
    private static ResearchToolDefinition Definition(string name, string[] required, params (string Name, string Type)[] properties)
    {
        var schemas = properties.ToDictionary(x => x.Name, x => (object)(x.Type.Contains('|', StringComparison.Ordinal)
            ? new Dictionary<string, object> { ["type"] = x.Type.Split('|') }
            : new Dictionary<string, object> { ["type"] = x.Type }), StringComparer.Ordinal);
        return new ResearchToolDefinition(name, StageFourResearchTools.SchemaVersion,
            Canonical(new Dictionary<string, object> { ["additionalProperties"] = false, ["properties"] = schemas, ["required"] = required, ["type"] = "object" }));
    }
}
