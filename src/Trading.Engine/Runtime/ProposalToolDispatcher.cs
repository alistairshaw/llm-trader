using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Core.Research;

namespace Trading.Engine.Runtime;

public static class StageFiveTradingTools
{
    public const string ProposeTrade = nameof(ProposeTrade);
    public const string ProposeTargetAllocation = nameof(ProposeTargetAllocation);
}

public static class ProposalToolCodes
{
    public const string Recorded = "proposal_recorded";
    public const string UnknownProperty = "unknown_property";
    public const string MissingRequiredProperty = "missing_required_property";
    public const string InvalidIdentifier = "invalid_identifier";
    public const string InvalidDecimal = "invalid_decimal";
    public const string InvalidCurrency = "invalid_currency";
    public const string InvalidInstrument = "invalid_instrument";
    public const string InvalidQuantity = "invalid_quantity";
    public const string InvalidAllocationTotal = "invalid_allocation_total";
    public const string InvalidExpiration = "invalid_expiration";
    public const string PortfolioNotAssigned = "portfolio_not_assigned";
    public const string EvidenceNotVisible = "evidence_not_visible";
    public const string HypothesisNotFrozen = "hypothesis_not_frozen";
    public const string ProposalBudgetExceeded = "proposal_budget_exceeded";
    public const string NotionalBudgetExceeded = "proposal_notional_budget_exceeded";
    public const string IdempotencyConflict = "proposal_idempotency_conflict";
}

public sealed class ProposalToolDispatcher(
    TradingBotResearchToolDispatcher prior,
    IBotRunRepository runs,
    ITradingBotRepository bots,
    IPortfolioDecisionSnapshotRepository snapshots,
    ITradeProposalRepository proposals,
    IHypothesisRepository hypotheses,
    IResearchReportCatalogQueries reports,
    IUtcClock clock) : IToolDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };
    public const int SchemaVersion = 1;
    public const int MaximumArgumentsBytes = 16 * 1024;
    public const int MaximumRationaleCharacters = 4000;
    public const decimal MaximumProposalNotional = 1_000_000_000m;

    public static IReadOnlyList<ToolDefinition> Definitions { get; } =
    [
        .. TradingBotResearchToolDispatcher.Definitions,
        new(StageFiveTradingTools.ProposeTrade, SchemaVersion, "{\"additionalProperties\":false,\"properties\":{\"evidenceReports\":{\"items\":{\"additionalProperties\":false,\"properties\":{\"reportId\":{\"type\":\"string\"},\"seriesId\":{\"type\":\"string\"},\"version\":{\"minimum\":1,\"type\":\"integer\"}},\"required\":[\"reportId\",\"seriesId\",\"version\"],\"type\":\"object\"},\"maxItems\":20,\"type\":\"array\"},\"hypothesisVersionId\":{\"type\":[\"string\",\"null\"]},\"instrumentId\":{\"type\":\"string\"},\"limitCurrency\":{\"type\":[\"string\",\"null\"]},\"limitPrice\":{\"type\":[\"string\",\"null\"]},\"orderType\":{\"enum\":[\"Market\",\"Limit\"]},\"portfolioId\":{\"type\":\"string\"},\"portfolioSnapshotId\":{\"type\":\"string\"},\"proposalId\":{\"type\":\"string\"},\"quantity\":{\"type\":\"string\"},\"quantityUnit\":{\"type\":\"string\"},\"rationale\":{\"maxLength\":4000,\"minLength\":1,\"type\":\"string\"},\"side\":{\"enum\":[\"Buy\",\"Sell\"]},\"timeInForce\":{\"enum\":[\"Day\",\"GoodTillCancelled\"]},\"validUntil\":{\"format\":\"date-time\",\"type\":\"string\"}},\"required\":[\"evidenceReports\",\"hypothesisVersionId\",\"instrumentId\",\"limitCurrency\",\"limitPrice\",\"orderType\",\"portfolioId\",\"portfolioSnapshotId\",\"proposalId\",\"quantity\",\"quantityUnit\",\"rationale\",\"side\",\"timeInForce\",\"validUntil\"],\"type\":\"object\"}"),
        new(StageFiveTradingTools.ProposeTargetAllocation, SchemaVersion, "{\"additionalProperties\":false,\"properties\":{\"evidenceReports\":{\"items\":{\"additionalProperties\":false,\"properties\":{\"reportId\":{\"type\":\"string\"},\"seriesId\":{\"type\":\"string\"},\"version\":{\"minimum\":1,\"type\":\"integer\"}},\"required\":[\"reportId\",\"seriesId\",\"version\"],\"type\":\"object\"},\"maxItems\":20,\"type\":\"array\"},\"hypothesisVersionId\":{\"type\":[\"string\",\"null\"]},\"instrumentId\":{\"type\":\"string\"},\"portfolioId\":{\"type\":\"string\"},\"portfolioSnapshotId\":{\"type\":\"string\"},\"proposalId\":{\"type\":\"string\"},\"rationale\":{\"maxLength\":4000,\"minLength\":1,\"type\":\"string\"},\"targetPercentage\":{\"type\":\"string\"},\"validUntil\":{\"format\":\"date-time\",\"type\":\"string\"}},\"required\":[\"evidenceReports\",\"hypothesisVersionId\",\"instrumentId\",\"portfolioId\",\"portfolioSnapshotId\",\"proposalId\",\"rationale\",\"targetPercentage\",\"validUntil\"],\"type\":\"object\"}"),
    ];
    IReadOnlyList<ToolDefinition> IToolDispatcher.Definitions => Definitions;

    public async Task<ToolDispatchResult> DispatchAsync(ToolDispatchContext context, ModelToolCall toolCall, CancellationToken cancellationToken)
    {
        var call = toolCall;
        var token = cancellationToken;
        if (!IsProposal(call.Name)) return await prior.DispatchAsync(context, call, token).ConfigureAwait(false);
        var auditToken = token.IsCancellationRequested ? CancellationToken.None : token;
        var run = await runs.GetAsync(context.RunId, auditToken).ConfigureAwait(false);
        if (run is null || run.TradingBotId != context.TradingBotId || run.PortfolioSnapshotId != context.SnapshotId)
            return Reject(call, ToolDispatchCodes.RunMismatch);
        if (run.ToolInvocations.Any(x => x.Id == call.InvocationId)) return Reject(call, ToolDispatchCodes.InvocationAlreadyExists);
        if (run.Status != BotRunStatus.WaitingForTool || run.IsTerminal) return Reject(call, ToolDispatchCodes.RunNotActive);
        var bot = await bots.GetAsync(run.TradingBotId, auditToken).ConfigureAwait(false);
        var configuration = bot?.ConfigurationVersions.SingleOrDefault(x => x.Id == run.ConfigurationVersionId);
        if (configuration is null) return Reject(call, ToolDispatchCodes.RunMismatch);

        var started = clock.UtcNow;
        var invocation = run.StartToolInvocation(call.InvocationId, call.Name, Safe(call.CanonicalArguments), started);
        var expected = run.Version;
        if (await runs.SaveAsync(run, expected, auditToken).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded)
            return Reject(call, ToolDispatchCodes.PersistenceConflict);
        var code = ValidateEnvelope(call, run, configuration, token);
        if (code is not null) return await FailAsync(run, invocation, expected + 1, call, code, auditToken).ConfigureAwait(false);

        try
        {
            var arguments = Parse(call);
            code = await AuthorizeAsync(context, run, configuration, arguments, token).ConfigureAwait(false);
            if (code is not null) return await FailAsync(run, invocation, expected + 1, call, code, auditToken).ConfigureAwait(false);
            var proposal = await CreateAsync(run, arguments, token).ConfigureAwait(false);
            var result = await proposals.RecordAsync(proposal, $"{run.Id}:{call.InvocationId}", token).ConfigureAwait(false);
            if (result is ProposalRecordResult.IdempotencyConflict)
                return await FailAsync(run, invocation, expected + 1, call, ProposalToolCodes.IdempotencyConflict, auditToken).ConfigureAwait(false);
            var recorded = result switch { ProposalRecordResult.Recorded x => x.Proposal, ProposalRecordResult.AlreadyRecorded x => x.Proposal, _ => proposal };
            var canonicalResult = Json(new SortedDictionary<string, object?> { ["code"] = ProposalToolCodes.Recorded, ["proposalId"] = recorded.Id.ToString(), ["proposalType"] = recorded.ProposalType.ToString(), ["status"] = recorded.Status.ToString() });
            var usage = UsageAfter(run, started, proposal: true);
            invocation.Complete(canonicalResult, usage, clock.UtcNow); run.RecordModelProgress(run.ModelTranscriptSchemaVersion, run.ModelTranscriptJson, usage);
            if (await runs.SaveAsync(run, expected + 1, token).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded)
                return Reject(call, ToolDispatchCodes.PersistenceConflict);
            return Result(call, ToolExecutionOutcome.Succeeded, ToolAuthorizationOutcome.Authorized, ProposalToolCodes.Recorded, canonicalResult);
        }
        catch (ProposalArgumentException ex) { return await FailAsync(run, invocation, expected + 1, call, ex.Code, auditToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return await FailAsync(run, invocation, expected + 1, call, ToolDispatchCodes.Cancelled, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception) { return await FailAsync(run, invocation, expected + 1, call, ToolDispatchCodes.ToolFailed, auditToken).ConfigureAwait(false); }
    }

    private static string? ValidateEnvelope(ModelToolCall call, BotRun run, TradingBotConfigurationVersion config, CancellationToken token)
    {
        if (token.IsCancellationRequested) return ToolDispatchCodes.Cancelled;
        if (call.SchemaVersion != SchemaVersion) return ToolDispatchCodes.UnsupportedSchemaVersion;
        if (!config.ToolPolicy.IsAllowed(call.Name)) return ToolDispatchCodes.ToolDisallowed;
        if (run.ToolInvocations.Count > config.RunBudget.ToolCallLimit) return ToolDispatchCodes.TotalToolBudgetExceeded;
        if (run.ToolInvocations.Count(x => x.ToolName == call.Name) > config.ToolPolicy.GetCallLimit(call.Name)) return ToolDispatchCodes.PerToolBudgetExceeded;
        if (run.Usage.Proposals >= config.RunBudget.ProposalLimit) return ProposalToolCodes.ProposalBudgetExceeded;
        if (Encoding.UTF8.GetByteCount(call.CanonicalArguments ?? "") > MaximumArgumentsBytes) return ToolDispatchCodes.ArgumentsTooLarge;
        try { using var d = JsonDocument.Parse(call.CanonicalArguments ?? string.Empty); if (Canonical(d.RootElement) != call.CanonicalArguments) return ToolDispatchCodes.NonCanonicalArguments; }
        catch { return ToolDispatchCodes.MalformedArguments; }
        return null;
    }

    private async Task<string?> AuthorizeAsync(ToolDispatchContext context, BotRun run, TradingBotConfigurationVersion config, ProposalArgs args, CancellationToken token)
    {
        if (args.PortfolioSnapshotId != context.SnapshotId) return ToolDispatchCodes.RunMismatch;
        var snapshot = await snapshots.GetAsync(context.SnapshotId, token).ConfigureAwait(false);
        if (snapshot is null || snapshot.TradingBotId != run.TradingBotId || snapshot.ConfigurationVersionId != config.Id) return ToolDispatchCodes.RunMismatch;
        if (snapshot.PortfolioId != args.PortfolioId) return ProposalToolCodes.PortfolioNotAssigned;
        if (args.ValidUntil <= clock.UtcNow) return ProposalToolCodes.InvalidExpiration;
        if (args.Action is DirectTradeAction direct && direct.LimitPrice is not null && direct.LimitPrice.Currency != snapshot.Cash.Currency) return ProposalToolCodes.InvalidCurrency;
        if (args.Action is DirectTradeAction { LimitPrice: not null } priced)
        {
            var notional = priced.LimitPrice * priced.Quantity;
            var configuredLimit = config.RiskPolicy.Limits.SingleOrDefault(x =>
                string.Equals(x.Metric, "ProposalNotional", StringComparison.Ordinal) &&
                string.Equals(x.Unit, notional.Currency.Code, StringComparison.Ordinal))?.Maximum ?? MaximumProposalNotional;
            if (notional.Amount > configuredLimit) return ProposalToolCodes.NotionalBudgetExceeded;
        }
        foreach (var evidence in args.Evidence)
        {
            var report = await reports.GetAuthorizedVersionAsync(new(run.TradingBotId.ToString(), ResearchPrincipalKind.TradingBot), evidence.SeriesId, evidence.Version, token).ConfigureAwait(false);
            if (report is null || report.Id != evidence.ReportId) return ProposalToolCodes.EvidenceNotVisible;
        }
        if (args.HypothesisVersionId is not null)
        {
            var hypothesis = await hypotheses.GetVersionAsync(args.HypothesisVersionId, token).ConfigureAwait(false);
            if (hypothesis is null) return ProposalToolCodes.EvidenceNotVisible;
            if (!hypothesis.IsFrozen) return ProposalToolCodes.HypothesisNotFrozen;
        }
        return null;
    }

    private async Task<TradeProposal> CreateAsync(BotRun run, ProposalArgs args, CancellationToken token)
    {
        var evidence = new List<ReportEvidenceReference>();
        foreach (var item in args.Evidence)
        {
            var report = (await reports.GetAuthorizedVersionAsync(new(run.TradingBotId.ToString(), ResearchPrincipalKind.TradingBot), item.SeriesId, item.Version, token).ConfigureAwait(false))!;
            evidence.Add(new(report.Id, report.ReportSeriesId, report.VersionNumber, report.ContentHash));
        }
        HypothesisEvidenceReference? hypothesis = null;
        if (args.HypothesisVersionId is not null)
        {
            var version = (await hypotheses.GetVersionAsync(args.HypothesisVersionId, token).ConfigureAwait(false))!;
            var hypothesisHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(new SortedDictionary<string, object?> { ["id"] = version.Id.ToString(), ["version"] = version.VersionNumber })))).ToLowerInvariant();
            hypothesis = new(version.Id, hypothesisHash);
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(args.Canonical))).ToLowerInvariant();
        return new(args.ProposalId, run.TradingBotId, run.Id, args.PortfolioId, run.ConfigurationVersionId, run.PortfolioSnapshotId,
            args.InstrumentId, args.Action, args.Rationale, new(1, hash), hypothesis, evidence, clock.UtcNow, args.ValidUntil);
    }

    private static ProposalArgs Parse(ModelToolCall call)
    {
        using var document = JsonDocument.Parse(call.CanonicalArguments); var root = document.RootElement;
        var required = call.Name == StageFiveTradingTools.ProposeTrade
            ? new[] { "evidenceReports", "hypothesisVersionId", "instrumentId", "limitCurrency", "limitPrice", "orderType", "portfolioId", "portfolioSnapshotId", "proposalId", "quantity", "quantityUnit", "rationale", "side", "timeInForce", "validUntil" }
            : ["evidenceReports", "hypothesisVersionId", "instrumentId", "portfolioId", "portfolioSnapshotId", "proposalId", "rationale", "targetPercentage", "validUntil"];
        Object(root, required);
        try
        {
            var proposalId = TradeProposalId.Parse(String(root, "proposalId", 100)); var portfolioId = PortfolioId.Parse(String(root, "portfolioId", 100));
            var snapshotId = PortfolioDecisionSnapshotId.Parse(String(root, "portfolioSnapshotId", 100)); var instrumentId = InstrumentId.Parse(String(root, "instrumentId", 100));
            var rationale = String(root, "rationale", MaximumRationaleCharacters); var validUntil = Utc(root, "validUntil");
            var hypothesis = NullableString(root, "hypothesisVersionId", 100) is { } h ? HypothesisVersionId.Parse(h) : null;
            var evidence = Evidence(root);
            RequestedAction action = call.Name == StageFiveTradingTools.ProposeTrade ? Direct(root) : Allocation(root);
            return new(proposalId, portfolioId, snapshotId, instrumentId, action, rationale, validUntil, hypothesis, evidence, call.CanonicalArguments);
        }
        catch (ProposalArgumentException) { throw; }
        catch (FormatException) { throw new ProposalArgumentException(ProposalToolCodes.InvalidIdentifier); }
        catch (ArgumentException) { throw new ProposalArgumentException(ProposalToolCodes.InvalidIdentifier); }
    }

    private static DirectTradeAction Direct(JsonElement root)
    {
        var side = ExactEnum<TradeSide>(root, "side"); var order = ExactEnum<ProposedOrderType>(root, "orderType"); var tif = ExactEnum<ProposedTimeInForce>(root, "timeInForce");
        var quantity = Decimal(root, "quantity", positive: true); var unit = String(root, "quantityUnit", 20);
        decimal? price = NullableDecimal(root, "limitPrice", positive: true); var currencyText = NullableString(root, "limitCurrency", 3);
        if ((order == ProposedOrderType.Limit) != (price is not null && currencyText is not null)) throw new ProposalArgumentException(ProposalToolCodes.InvalidDecimal);
        try { return new DirectTradeAction(side, new Quantity(quantity, unit), order, price is null ? null : new Price(price.Value, new Currency(currencyText!)), tif); }
        catch (ArgumentOutOfRangeException) { throw new ProposalArgumentException(ProposalToolCodes.InvalidQuantity); }
        catch (ArgumentException) { throw new ProposalArgumentException(ProposalToolCodes.InvalidCurrency); }
    }
    private static TargetAllocationAction Allocation(JsonElement root)
    {
        var target = Decimal(root, "targetPercentage", positive: false);
        if (target < 0 || target > 100) throw new ProposalArgumentException(ProposalToolCodes.InvalidAllocationTotal);
        return new TargetAllocationAction(new Percentage(target));
    }
    private static EvidenceArg[] Evidence(JsonElement root)
    {
        var element = root.GetProperty("evidenceReports"); if (element.ValueKind != JsonValueKind.Array) throw new ProposalArgumentException(ToolDispatchCodes.MalformedArguments);
        var values = new List<EvidenceArg>(); foreach (var item in element.EnumerateArray()) { Object(item, ["reportId", "seriesId", "version"]); try { values.Add(new(ResearchReportId.Parse(String(item, "reportId", 100)), String(item, "seriesId", 200), Integer(item, "version", 1))); } catch (FormatException) { throw new ProposalArgumentException(ProposalToolCodes.InvalidIdentifier); } }
        if (values.Count > 20 || values.Select(x => x.ReportId).Distinct().Count() != values.Count) throw new ProposalArgumentException(ToolDispatchCodes.MalformedArguments); return values.ToArray();
    }
    private static void Object(JsonElement element, string[] required)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new ProposalArgumentException(ToolDispatchCodes.MalformedArguments);
        if (element.EnumerateObject().Any(p => !required.Contains(p.Name, StringComparer.Ordinal))) throw new ProposalArgumentException(ProposalToolCodes.UnknownProperty);
        if (required.Any(p => !element.TryGetProperty(p, out _))) throw new ProposalArgumentException(ProposalToolCodes.MissingRequiredProperty);
    }
    private static string String(JsonElement root, string name, int max) { var e = root.GetProperty(name); if (e.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(e.GetString()) || e.GetString()!.Length > max) throw new ProposalArgumentException(ToolDispatchCodes.MalformedArguments); return e.GetString()!; }
    private static string? NullableString(JsonElement root, string name, int max) => root.GetProperty(name).ValueKind == JsonValueKind.Null ? null : String(root, name, max);
    private static decimal Decimal(JsonElement root, string name, bool positive) { var text = String(root, name, 64); if (!decimal.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var value) || FormatDecimal(value) != text) throw new ProposalArgumentException(ProposalToolCodes.InvalidDecimal); if (positive && value <= 0) throw new ProposalArgumentException(ProposalToolCodes.InvalidQuantity); return value; }
    private static decimal? NullableDecimal(JsonElement root, string name, bool positive) => root.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Decimal(root, name, positive);
    private static int Integer(JsonElement root, string name, int min) { if (!root.GetProperty(name).TryGetInt32(out var value) || value < min) throw new ProposalArgumentException(ToolDispatchCodes.MalformedArguments); return value; }
    private static T ExactEnum<T>(JsonElement root, string name) where T : struct, Enum { var text = String(root, name, 50); if (!Enum.TryParse<T>(text, false, out var value) || value.ToString() != text) throw new ProposalArgumentException(ToolDispatchCodes.MalformedArguments); return value; }
    private static DateTimeOffset Utc(JsonElement root, string name) { if (!DateTimeOffset.TryParseExact(String(root, name, 40), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var value)) throw new ProposalArgumentException(ProposalToolCodes.InvalidExpiration); return value.ToUniversalTime(); }
    private async Task<ToolDispatchResult> FailAsync(BotRun run, ToolInvocation invocation, long version, ModelToolCall call, string code, CancellationToken token) { var usage = UsageAfter(run, clock.UtcNow, false); invocation.Fail(code, usage, clock.UtcNow); run.RecordModelProgress(run.ModelTranscriptSchemaVersion, run.ModelTranscriptJson, usage); await runs.SaveAsync(run, version, token).ConfigureAwait(false); return Reject(call, code); }
    private Usage UsageAfter(BotRun run, DateTimeOffset started, bool proposal) => new(run.Usage.Elapsed + (clock.UtcNow - started), run.Usage.Tokens, run.Usage.Cost, run.Usage.ToolCalls + 1, run.Usage.ResearchRequests, run.Usage.Proposals + (proposal ? 1 : 0));
    private static bool IsProposal(string name) => name is StageFiveTradingTools.ProposeTrade or StageFiveTradingTools.ProposeTargetAllocation;
    private static string Safe(string? value) => Encoding.UTF8.GetByteCount(value ?? "") <= MaximumArgumentsBytes ? value ?? "null" : "{\"redacted\":\"arguments_too_large\"}";
    private static string Json(object value) => JsonSerializer.Serialize(value, SerializerOptions);
    private static string FormatDecimal(decimal value) => value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture);
    private static string Canonical(JsonElement e) { using var s = new MemoryStream(); using (var w = new Utf8JsonWriter(s)) Write(w, e); return Encoding.UTF8.GetString(s.ToArray()); }
    private static void Write(Utf8JsonWriter w, JsonElement e) { if (e.ValueKind == JsonValueKind.Object) { w.WriteStartObject(); foreach (var p in e.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { w.WritePropertyName(p.Name); Write(w, p.Value); } w.WriteEndObject(); } else if (e.ValueKind == JsonValueKind.Array) { w.WriteStartArray(); foreach (var x in e.EnumerateArray()) Write(w, x); w.WriteEndArray(); } else e.WriteTo(w); }
    private static ToolDispatchResult Reject(ModelToolCall call, string code) => Result(call, ToolExecutionOutcome.Rejected, ToolAuthorizationOutcome.Disallowed, code, Json(new SortedDictionary<string, object?> { ["code"] = code }));
    private static ToolDispatchResult Result(ModelToolCall call, ToolExecutionOutcome outcome, ToolAuthorizationOutcome authorization, string code, string json) => new(new(call.InvocationId, call.Name, call.SchemaVersion, outcome, json), new(authorization, code));
    private sealed record EvidenceArg(ResearchReportId ReportId, string SeriesId, int Version);
    private sealed record ProposalArgs(TradeProposalId ProposalId, PortfolioId PortfolioId, PortfolioDecisionSnapshotId PortfolioSnapshotId, InstrumentId InstrumentId, RequestedAction Action, string Rationale, DateTimeOffset ValidUntil, HypothesisVersionId? HypothesisVersionId, EvidenceArg[] Evidence, string Canonical);
    private sealed class ProposalArgumentException(string code) : Exception(code) { public string Code { get; } = code; }
}
