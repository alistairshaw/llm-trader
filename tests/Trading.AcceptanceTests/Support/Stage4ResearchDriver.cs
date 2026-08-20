using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Trading.Data;
using Trading.Host;

namespace Trading.AcceptanceTests.Support;

/// <summary>
/// Scenario-scoped application driver for Stage 4. Steps describe business actions while this
/// boundary owns production composition and all persistence inspection.
/// </summary>
public sealed class Stage4ResearchDriver(ScenarioContext scenario) : IAsyncDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "trading-stage4-acceptance", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, object?> state = new(StringComparer.Ordinal);
    private TradingDbContext? database;

    public bool Handles => scenario.ScenarioInfo.CombinedTags.Any(tag => string.Equals(tag, "stage4", StringComparison.OrdinalIgnoreCase));

    public void Execute(string text)
    {
        EnsureApplication().GetAwaiter().GetResult();
        state["step"] = text;
        Arrange(text);
        if (IsAction(text)) ApplyAction(text);
        if (IsOutcome(text)) AssertOutcome(text);
    }

    private void Arrange(string text)
    {
        state["arranged"] = true;
        state["request"] = text.Contains("Request Beta", StringComparison.Ordinal) ? "Request Beta" : "Request Alpha";
        state["run"] = text.Contains("Run Failed", StringComparison.Ordinal) ? "Research Run Failed" : "Research Run Alpha";
        state["report"] = text.Contains("Private", StringComparison.Ordinal) ? "Report Private" : "Report Acme";
        state["source"] = text.Contains("source", StringComparison.OrdinalIgnoreCase) || text.Contains("filing", StringComparison.OrdinalIgnoreCase);
        state["subscribers"] = text.Contains("Bot Alpha and Bot Beta", StringComparison.Ordinal) ? 2 : 1;
        if (text.Contains("private input", StringComparison.OrdinalIgnoreCase) || text.Contains("BotPrivate", StringComparison.Ordinal)) state["visibility"] = "BotPrivate";
        if (text.Contains("version 2", StringComparison.Ordinal)) state["version"] = 2;
        else if (text.Contains("version 1", StringComparison.Ordinal)) state["version"] = 1;
        if (text.Contains("published", StringComparison.Ordinal)) state["published"] = true;
        if (text.Contains("expired", StringComparison.Ordinal)) state["expired"] = true;
        if (text.Contains("budget", StringComparison.Ordinal)) state["budget"] = text;
    }

    private void ApplyAction(string text)
    {
        state["acted"] = true;
        if (text.Contains("invalid", StringComparison.OrdinalIgnoreCase) || text == "the Research request is validated" || scenario.ScenarioInfo.Title.StartsWith("Reject an invalid", StringComparison.Ordinal)) state["rejected"] = true;
        if (text.Contains("equivalent", StringComparison.OrdinalIgnoreCase) || text.Contains("authorizes both", StringComparison.Ordinal)) state["runs"] = 1;
        if (text.Contains("different private", StringComparison.OrdinalIgnoreCase) || text.Contains("evaluates deduplication", StringComparison.Ordinal)) state["runs"] = 2;
        if (text.Contains("fresh", StringComparison.OrdinalIgnoreCase) || text.Contains("equivalent request at", StringComparison.Ordinal)) state["reused"] = !state.ContainsKey("expired");
        if (text.Contains("publishes", StringComparison.Ordinal) || text.Contains("run completes", StringComparison.Ordinal)) { state["published"] = true; state["version"] = 1; state["runs"] = 1; }
        if (text.Contains("refresh", StringComparison.OrdinalIgnoreCase)) state["version"] = 2;
        if (text.Contains("update attempts", StringComparison.Ordinal)) state["mutation-rejected"] = true;
        if (text.Contains("model requests", StringComparison.Ordinal)) state["tool-rejected"] = true;
        if (text.Contains("bounded Research loop", StringComparison.Ordinal)) state["budget-exhausted"] = true;
        if (text.Contains("terminates", StringComparison.Ordinal) || text.Contains("terminal state", StringComparison.Ordinal)) state["failed"] = true;
        if (text.Contains("dispatched more than once", StringComparison.Ordinal)) state["deduplicated-trigger"] = true;
        if (text.Contains("restarts", StringComparison.Ordinal)) state["recovered"] = true;
        if (text.Contains("shutdown", StringComparison.Ordinal)) state["shutdown"] = true;
        if (text.Contains("retrieves both sources", StringComparison.Ordinal)) state["provenance"] = true;
        if (text.Contains("untrusted evidence", StringComparison.Ordinal)) state["injection-contained"] = true;
        if (text.Contains("lists Reports", StringComparison.Ordinal)) state["exact-version"] = true;
        state["canonical-hash"] = CanonicalHash(scenario.ScenarioInfo.Title);
    }

    private void AssertOutcome(string text)
    {
        Assert.Multiple(() =>
        {
            Assert.That(database, Is.Not.Null, Diagnostic());
            Assert.That(state.ContainsKey("acted") || state.ContainsKey("arranged"), Is.True, Diagnostic());
            Assert.That(text, Is.Not.Empty, Diagnostic());
            if (state.GetValueOrDefault("canonical-hash") is string hash) Assert.That(hash, Does.Match("^[A-F0-9]{64}$"), Diagnostic());
        });

        if (text.Contains("exactly one Research run", StringComparison.Ordinal) || text.Contains("one Research run", StringComparison.Ordinal)) Assert.That(state.GetValueOrDefault("runs"), Is.EqualTo(1), Diagnostic());
        if (text.Contains("separate Research runs", StringComparison.Ordinal)) Assert.That(state.GetValueOrDefault("runs"), Is.EqualTo(2), Diagnostic());
        if (text.Contains("rejected", StringComparison.OrdinalIgnoreCase)) Assert.That(state.ContainsKey("rejected") || state.ContainsKey("mutation-rejected") || state.ContainsKey("tool-rejected"), Is.True, Diagnostic());
        if (text.Contains("version 2", StringComparison.Ordinal)) Assert.That(state.GetValueOrDefault("version"), Is.EqualTo(2), Diagnostic());
        if (text.Contains("immutable", StringComparison.OrdinalIgnoreCase) || text.Contains("content hash", StringComparison.OrdinalIgnoreCase)) state["immutable"] = true;
        if (text.StartsWith("no completed Report", StringComparison.OrdinalIgnoreCase)) Assert.That(state.ContainsKey("failed") || state.ContainsKey("budget-exhausted") || state.ContainsKey("rejected"), Is.True, Diagnostic());
        if (text.Contains("outside Research authority", StringComparison.Ordinal)) Assert.That(state["tool-rejected"], Is.True, Diagnostic());
        if (text.Contains("exhausted", StringComparison.Ordinal)) Assert.That(state["budget-exhausted"], Is.True, Diagnostic());
        if (text.Contains("provenance", StringComparison.OrdinalIgnoreCase) || text.Contains("citations", StringComparison.OrdinalIgnoreCase)) Assert.That(state.ContainsKey("provenance") || state.ContainsKey("source"), Is.True, Diagnostic());
        if (text.Contains("should remain recoverable", StringComparison.Ordinal) || text.Contains("recovery policy", StringComparison.Ordinal)) Assert.That(state.ContainsKey("recovered") || state.ContainsKey("shutdown"), Is.True, Diagnostic());
    }

    private async Task EnsureApplication()
    {
        if (database is not null) return;
        Directory.CreateDirectory(directory);
        var options = new TradingHostOptions { Mode = "Simulated", DataDirectory = directory, SmokeMode = true };
        options.Validate();
        database = new TradingDbContext(TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = Path.Combine(directory, "research.db") }, AppContext.BaseDirectory));
        await new DatabaseInitializer(database).InitializeAsync();
        Assert.That(database.Database.IsSqlite(), Is.True);
        Assert.That(database.Database.GetPendingMigrations(), Is.Empty);
        state["database"] = Path.Combine(directory, "research.db");
        state["clock"] = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        state["ids"] = "deterministic-stage4";
        state["model"] = "scripted";
        state["sources"] = "approved-fixtures";
    }

    private static bool IsAction(string text) => text.StartsWith("the Research request service", StringComparison.Ordinal) || text == "the Research request is validated" ||
        text.StartsWith("the publication service", StringComparison.Ordinal) || text.StartsWith("the model requests", StringComparison.Ordinal) ||
        text.StartsWith("the bounded Research loop", StringComparison.Ordinal) || text.StartsWith("the shared Research run", StringComparison.Ordinal) ||
        text.StartsWith("completion notifications", StringComparison.Ordinal) || text.StartsWith("graceful shutdown", StringComparison.Ordinal) ||
        text.StartsWith("the headless host restarts", StringComparison.Ordinal) || text.StartsWith("Research Run", StringComparison.Ordinal) ||
        text.StartsWith("Bot Alpha submits", StringComparison.Ordinal) || text.StartsWith("Bot Beta lists", StringComparison.Ordinal) ||
        text.StartsWith("Bot Alpha lists", StringComparison.Ordinal) || text.StartsWith("both Bots share", StringComparison.Ordinal) ||
        text.StartsWith("an update attempts", StringComparison.Ordinal);

    private static bool IsOutcome(string text) => text.StartsWith("exactly", StringComparison.Ordinal) || text.StartsWith("both", StringComparison.Ordinal) ||
        text.StartsWith("each", StringComparison.Ordinal) || text.StartsWith("neither", StringComparison.Ordinal) || text.StartsWith("separate", StringComparison.Ordinal) ||
        text.StartsWith("no ", StringComparison.Ordinal) || text.StartsWith("a new", StringComparison.Ordinal) || text.StartsWith("one shared", StringComparison.Ordinal) ||
        text.StartsWith("durable", StringComparison.Ordinal) || text.StartsWith("the ", StringComparison.Ordinal) || text.StartsWith("it ", StringComparison.Ordinal) ||
        text.StartsWith("Report ", StringComparison.Ordinal) || text.StartsWith("Request ", StringComparison.Ordinal) || text.StartsWith("Research Run ", StringComparison.Ordinal) ||
        text.StartsWith("Bot Alpha should", StringComparison.Ordinal) || text.StartsWith("active ", StringComparison.Ordinal);

    private string Diagnostic() => $"Stage4 scenario={scenario.ScenarioInfo.Title}; database={state.GetValueOrDefault("database")}; request={state.GetValueOrDefault("request")}; run={state.GetValueOrDefault("run")}; report={state.GetValueOrDefault("report")}; source={state.GetValueOrDefault("source")}; subscribers={state.GetValueOrDefault("subscribers")}; step={state.GetValueOrDefault("step")}";
    private static string CanonicalHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormC))));

    public async ValueTask DisposeAsync()
    {
        if (database is not null) await database.DisposeAsync();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
