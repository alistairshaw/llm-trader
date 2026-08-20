using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Core.Research;

namespace Trading.Engine.Runtime;

public enum BotRunInputFailure
{
    RunNotFound,
    BotNotFound,
    ConfigurationNotFound,
    PortfolioNotFound,
    SnapshotNotFound,
    BotMismatch,
    ConfigurationMismatch,
    PortfolioMismatch,
    AuditConcurrencyConflict,
}

public sealed class BotRunInputException(BotRunInputFailure failure, string message) : InvalidOperationException(message)
{
    public BotRunInputFailure Failure { get; } = failure;
}

public sealed record DeterministicBotRunInput(string RenderingVersion, string Content, string Sha256Hash,
    BotRun Run, TradingBot Bot, TradingBotConfigurationVersion Configuration, Portfolio Portfolio,
    PortfolioDecisionSnapshot Snapshot, IReadOnlyList<ResearchReportSummary>? AuthorizedReports = null);

public sealed record PinnedPortfolioSnapshot(string CanonicalContent, string ContentHash, int SchemaVersion,
    PortfolioDecisionSnapshot Snapshot);

public interface IBotRunInputService
{
    Task<DeterministicBotRunInput> PrepareAsync(BotRunId runId, CancellationToken cancellationToken);
    Task<PinnedPortfolioSnapshot> GetPortfolioSnapshotAsync(BotRunId runId, CancellationToken cancellationToken);
}

public sealed class BotRunInputService(
    IBotRunRepository runRepository,
    ITradingBotRepository botRepository,
    IPortfolioRepository portfolioRepository,
    IPortfolioDecisionSnapshotRepository snapshotRepository,
    IBotRunInputAuditWriter auditWriter,
    IResearchReportCatalogQueries? researchCatalog = null) : IBotRunInputService
{
    public const string CurrentRenderingVersion = "1";

    public async Task<DeterministicBotRunInput> PrepareAsync(BotRunId runId, CancellationToken cancellationToken)
    {
        var facts = await LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        var reports = researchCatalog is null ? [] : await researchCatalog.SearchAsync(
            new ResearchReportSearch(new ResearchPrincipal(facts.Bot.Id.ToString(), ResearchPrincipalKind.TradingBot),
                facts.Snapshot.AsOf, FreshOnly: false, Size: 100), cancellationToken).ConfigureAwait(false);
        var content = BotRunInputRenderer.Render(facts.Run, facts.Bot, facts.Configuration, facts.Portfolio, facts.Snapshot, reports);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        var write = await auditWriter.StoreInputRenderingAsync(runId, facts.Run.Version, CurrentRenderingVersion, hash, cancellationToken)
            .ConfigureAwait(false);
        if (write is not PersistenceWriteResult.Succeeded)
            throw new BotRunInputException(BotRunInputFailure.AuditConcurrencyConflict, "The Bot Run input audit record changed concurrently.");
        facts.Run.RecordInputRendering(CurrentRenderingVersion, hash);
        return new DeterministicBotRunInput(CurrentRenderingVersion, content, hash, facts.Run, facts.Bot,
            facts.Configuration, facts.Portfolio, facts.Snapshot, reports);
    }

    public async Task<PinnedPortfolioSnapshot> GetPortfolioSnapshotAsync(BotRunId runId, CancellationToken cancellationToken)
    {
        var facts = await LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        return new PinnedPortfolioSnapshot(facts.Snapshot.CanonicalContent, facts.Snapshot.ContentHash,
            facts.Snapshot.SnapshotSchemaVersion, facts.Snapshot);
    }

    private async Task<LoadedFacts> LoadAsync(BotRunId runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runId);
        var run = await runRepository.GetAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? Fail<BotRun>(BotRunInputFailure.RunNotFound, "The Bot Run does not exist.");
        var bot = await botRepository.GetAsync(run.TradingBotId, cancellationToken).ConfigureAwait(false)
            ?? Fail<TradingBot>(BotRunInputFailure.BotNotFound, "The pinned Trading Bot does not exist.");
        if (bot.Id != run.TradingBotId)
            Fail(BotRunInputFailure.BotMismatch, "The loaded Trading Bot does not match the Bot Run.");
        var configuration = bot.ConfigurationVersions.SingleOrDefault(item => item.Id == run.ConfigurationVersionId)
            ?? Fail<TradingBotConfigurationVersion>(BotRunInputFailure.ConfigurationNotFound,
                "The pinned configuration does not belong to the Bot Run's Trading Bot.");
        var snapshot = await snapshotRepository.GetAsync(run.PortfolioSnapshotId, cancellationToken).ConfigureAwait(false)
            ?? Fail<PortfolioDecisionSnapshot>(BotRunInputFailure.SnapshotNotFound, "The pinned Portfolio Decision Snapshot does not exist.");
        if (snapshot.TradingBotId != run.TradingBotId)
            Fail(BotRunInputFailure.BotMismatch, "The pinned snapshot belongs to another Trading Bot.");
        if (snapshot.ConfigurationVersionId != run.ConfigurationVersionId)
            Fail(BotRunInputFailure.ConfigurationMismatch, "The pinned snapshot uses another configuration version.");
        var portfolio = await portfolioRepository.GetAsync(snapshot.PortfolioId, cancellationToken).ConfigureAwait(false)
            ?? Fail<Portfolio>(BotRunInputFailure.PortfolioNotFound, "The pinned Portfolio does not exist.");
        if (portfolio.Id != snapshot.PortfolioId || portfolio.AssignedTradingBotId != run.TradingBotId ||
            (bot.PortfolioId is not null && bot.PortfolioId != portfolio.Id))
            Fail(BotRunInputFailure.PortfolioMismatch, "The Portfolio assignment does not match the Bot Run and snapshot.");
        return new LoadedFacts(run, bot, configuration, portfolio, snapshot);
    }

    private static T Fail<T>(BotRunInputFailure failure, string message) => throw new BotRunInputException(failure, message);
    private static void Fail(BotRunInputFailure failure, string message) => throw new BotRunInputException(failure, message);
    private sealed record LoadedFacts(BotRun Run, TradingBot Bot, TradingBotConfigurationVersion Configuration,
        Portfolio Portfolio, PortfolioDecisionSnapshot Snapshot);
}

internal static class BotRunInputRenderer
{
    public static string Render(BotRun run, TradingBot bot, TradingBotConfigurationVersion configuration,
        Portfolio portfolio, PortfolioDecisionSnapshot snapshot, IReadOnlyList<ResearchReportSummary>? reports = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("renderingVersion", BotRunInputService.CurrentRenderingVersion);
            WriteIdentity(writer, run, bot, configuration, portfolio, snapshot);
            WriteTriggers(writer, run);
            WriteBot(writer, bot);
            WriteConfiguration(writer, configuration);
            WritePortfolio(writer, portfolio);
            WriteSnapshot(writer, snapshot);
            WriteReports(writer, reports ?? []);
            WritePreviousRun(writer, bot);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteReports(Utf8JsonWriter writer, IEnumerable<ResearchReportSummary> reports)
    {
        writer.WritePropertyName("authorizedReports"); writer.WriteStartArray();
        foreach (var report in reports.OrderBy(x => x.SeriesId, StringComparer.Ordinal).ThenBy(x => x.Version))
        {
            writer.WriteStartObject(); writer.WriteString("reportId", report.Id.ToString()); writer.WriteString("seriesId", report.SeriesId);
            writer.WriteNumber("version", report.Version); writer.WriteString("subject", report.Subject); writer.WriteString("status", report.Status.ToString());
            writer.WriteString("dataCutoff", Timestamp(report.DataCutoff)); writer.WriteString("generatedAt", Timestamp(report.GeneratedAt));
            writer.WriteString("expiresAt", Timestamp(report.ExpiresAt)); writer.WriteBoolean("isFresh", report.IsFresh); writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteIdentity(Utf8JsonWriter writer, BotRun run, TradingBot bot,
        TradingBotConfigurationVersion configuration, Portfolio portfolio, PortfolioDecisionSnapshot snapshot)
    {
        writer.WritePropertyName("identity"); writer.WriteStartObject();
        writer.WriteString("botRunId", run.Id.ToString()); writer.WriteString("tradingBotId", bot.Id.ToString());
        writer.WriteString("configurationVersionId", configuration.Id.ToString()); writer.WriteString("portfolioId", portfolio.Id.ToString());
        writer.WriteString("portfolioSnapshotId", snapshot.Id.ToString()); writer.WriteEndObject();
    }

    private static void WriteTriggers(Utf8JsonWriter writer, BotRun run)
    {
        writer.WritePropertyName("triggers"); writer.WriteStartArray();
        foreach (var trigger in run.Triggers.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id.ToString(), StringComparer.Ordinal))
        {
            writer.WriteStartObject(); writer.WriteString("id", trigger.Id.ToString()); writer.WriteString("type", trigger.Type.ToString());
            writer.WriteString("reason", trigger.Reason); writer.WriteString("occurredAt", Timestamp(trigger.OccurredAt));
            if (trigger.SourceId is null) writer.WriteNull("sourceId"); else writer.WriteString("sourceId", trigger.SourceId);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteBot(Utf8JsonWriter writer, TradingBot bot)
    {
        writer.WritePropertyName("bot"); writer.WriteStartObject(); writer.WriteString("name", bot.Name);
        writer.WriteString("status", bot.Status.ToString()); writer.WriteEndObject();
    }

    private static void WriteConfiguration(Utf8JsonWriter writer, TradingBotConfigurationVersion value)
    {
        writer.WritePropertyName("configuration"); writer.WriteStartObject();
        writer.WriteNumber("versionNumber", value.VersionNumber); writer.WriteString("executionMode", value.ExecutionMode.ToString());
        writer.WriteString("promptVersion", value.PromptVersion);
        writer.WritePropertyName("mandate"); writer.WriteStartObject(); writer.WriteString("objective", value.InvestmentMandate.Objective);
        writer.WriteNumber("investmentHorizonTicks", value.InvestmentMandate.InvestmentHorizon.Ticks);
        WriteStrings(writer, "assetClasses", value.InvestmentMandate.Universe.AssetClasses);
        WriteStrings(writer, "markets", value.InvestmentMandate.Universe.Markets);
        WriteStrings(writer, "currencies", value.InvestmentMandate.Universe.Currencies.Select(x => x.Code)); writer.WriteEndObject();
        writer.WritePropertyName("riskPolicy"); writer.WriteStartObject(); writer.WriteBoolean("tradingHalted", value.RiskPolicy.TradingHalted);
        writer.WritePropertyName("limits"); writer.WriteStartArray();
        foreach (var limit in value.RiskPolicy.Limits.OrderBy(x => x.Metric, StringComparer.Ordinal))
        { writer.WriteStartObject(); writer.WriteString("metric", limit.Metric); writer.WriteString("minimum", Decimal(limit.Minimum)); writer.WriteString("maximum", Decimal(limit.Maximum)); writer.WriteString("unit", limit.Unit); writer.WriteEndObject(); }
        writer.WriteEndArray(); writer.WriteEndObject();
        writer.WritePropertyName("toolPolicy"); writer.WriteStartArray();
        foreach (var tool in value.ToolPolicy.AllowedTools.OrderBy(x => x.ToolName, StringComparer.Ordinal))
        { writer.WriteStartObject(); writer.WriteString("name", tool.ToolName); writer.WriteNumber("callLimit", tool.CallLimit); writer.WriteEndObject(); }
        writer.WriteEndArray();
        var budget = value.RunBudget; writer.WritePropertyName("runBudget"); writer.WriteStartObject(); writer.WriteNumber("wallClockTicks", budget.WallClock.Ticks);
        writer.WriteNumber("tokenLimit", budget.TokenLimit); writer.WriteString("costLimit", Decimal(budget.CostLimit.Amount)); writer.WriteString("costCurrency", budget.CostLimit.Currency.Code);
        writer.WriteNumber("toolCallLimit", budget.ToolCallLimit); writer.WriteNumber("researchRequestLimit", budget.ResearchRequestLimit); writer.WriteNumber("proposalLimit", budget.ProposalLimit); writer.WriteEndObject();
        var schedule = value.SchedulingPolicy; writer.WritePropertyName("schedule"); writer.WriteStartObject(); writer.WriteNumber("baselineCadenceTicks", schedule.BaselineCadence.Ticks);
        writer.WriteNumber("minimumRequestedWakeDelayTicks", schedule.MinimumRequestedWakeDelay.Ticks); writer.WriteNumber("maximumRequestedWakeDelayTicks", schedule.MaximumRequestedWakeDelay.Ticks);
        writer.WritePropertyName("utcWindows"); writer.WriteStartArray(); foreach (var window in schedule.Windows.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime))
        { writer.WriteStartObject(); writer.WriteString("day", window.DayOfWeek.ToString()); writer.WriteNumber("startTicks", window.StartTime.Ticks); writer.WriteNumber("endTicks", window.EndTime.Ticks); writer.WriteEndObject(); }
        writer.WriteEndArray(); writer.WriteEndObject(); writer.WriteEndObject();
    }

    private static void WritePortfolio(Utf8JsonWriter writer, Portfolio value)
    {
        writer.WritePropertyName("portfolio"); writer.WriteStartObject(); writer.WriteString("name", value.Name);
        writer.WriteString("status", value.Status.ToString()); writer.WriteString("baseCurrency", value.BaseCurrency.Code);
        writer.WriteString("capitalAllocation", Decimal(value.CapitalAllocation.Amount)); writer.WriteString("cashReservePercentage", Decimal(value.CashReservePercentage)); writer.WriteEndObject();
    }

    private static void WriteSnapshot(Utf8JsonWriter writer, PortfolioDecisionSnapshot value)
    {
        writer.WritePropertyName("snapshot"); writer.WriteStartObject(); writer.WriteNumber("schemaVersion", value.SnapshotSchemaVersion);
        writer.WriteString("contentHash", value.ContentHash); writer.WriteString("asOf", Timestamp(value.AsOf));
        writer.WriteString("createdAt", Timestamp(value.CreatedAt)); writer.WriteString("reconciliationStatus", value.ReconciliationStatus.ToString());
        writer.WritePropertyName("content"); using var document = JsonDocument.Parse(value.CanonicalContent); document.RootElement.WriteTo(writer);
        writer.WriteEndObject();
    }

    private static void WritePreviousRun(Utf8JsonWriter writer, TradingBot bot)
    {
        writer.WritePropertyName("previousRun"); writer.WriteStartObject();
        if (bot.LastCompletedRunId is null) writer.WriteNull("runId"); else writer.WriteString("runId", bot.LastCompletedRunId.ToString());
        if (bot.RequestedNextRunAt is null) writer.WriteNull("requestedNextRunAt"); else writer.WriteString("requestedNextRunAt", Timestamp(bot.RequestedNextRunAt.Value));
        if (bot.AcceptedNextRunAt is null) writer.WriteNull("acceptedNextRunAt"); else writer.WriteString("acceptedNextRunAt", Timestamp(bot.AcceptedNextRunAt.Value));
        writer.WriteEndObject();
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    { writer.WritePropertyName(name); writer.WriteStartArray(); foreach (var value in values.Order(StringComparer.Ordinal)) writer.WriteStringValue(value); writer.WriteEndArray(); }
    private static string Timestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
    private static string Decimal(decimal value) => value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture);
}
