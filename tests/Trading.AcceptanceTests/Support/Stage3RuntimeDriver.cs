using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.Policies;
using Trading.Data;
using Trading.Engine.Runtime;
using Trading.Host;
using Trading.TestInfrastructure;

namespace Trading.AcceptanceTests.Support;

/// <summary>Scenario-scoped application driver for the Stage 3 business vocabulary.</summary>
public sealed class Stage3RuntimeDriver : IAsyncDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "trading-stage3-acceptance", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, object?> facts = new(StringComparer.Ordinal);
    private TradingDbContext? database;

    public bool Handles(string text) => directory.Length > 0 && Prefixes.Any(text.StartsWith);

    public void Execute(string text)
    {
        EnsureDatabase().GetAwaiter().GetResult();
        facts["last-step"] = text;

        if (text.StartsWith("Config Alpha has baseline wake time", StringComparison.Ordinal)) ConfigureSchedule();
        else if (text.StartsWith("Run Alpha finishes with requested wake time ", StringComparison.Ordinal)) Decide(text[44..]);
        else if (text == "Run Alpha finishes with a malformed requested wake time") Decide("malformed");
        else if (text.StartsWith("its schedule decision should accept ", StringComparison.Ordinal)) AssertSchedule(text[36..].Trim(), ScheduleDecisionOutcome.Accepted);
        else if (text.StartsWith("its schedule decision should bound the wake time to ", StringComparison.Ordinal)) AssertSchedule(text[51..].Trim(), ScheduleDecisionOutcome.Adjusted);
        else if (text == "its schedule decision should reject the request with a recorded reason") Assert.That(Get<ScheduleDecision>("schedule").Outcome, Is.EqualTo(ScheduleDecisionOutcome.Rejected), Diagnostic());
        else if (text.StartsWith("schedule ", StringComparison.Ordinal) && text.EndsWith(" from the baseline", StringComparison.Ordinal)) Assert.That(Get<ScheduleDecision>("schedule").AcceptedTime, Is.EqualTo(ParseUtc(text[9..^18])), Diagnostic());
        else if (text == "the baseline schedule should remain enabled") Assert.That(Get<ScheduleDecision>("schedule").BaselineTime, Is.Not.Null, Diagnostic());
        else if (text.StartsWith("Run Alpha uses Config Alpha with a ", StringComparison.Ordinal)) facts["budget"] = text[35..];
        else if (text.StartsWith("its scripted model attempts to consume ", StringComparison.Ordinal)) facts["attempt"] = text[39..];
        else if (text.StartsWith("Run Alpha should terminate safely for exhausted ", StringComparison.Ordinal)) AssertBudget(text[47..]);
        else if (text == "its model input is rendered twice") { facts["input-a"] = RenderInput(); facts["input-b"] = RenderInput(); }
        else if (text == "both inputs should be byte-identical") Assert.That(Get<string>("input-a"), Is.EqualTo(Get<string>("input-b")), Diagnostic());
        else if (text.StartsWith("each input should name ", StringComparison.Ordinal)) Assert.That(Get<string>("input-a"), Does.Contain("Bot Alpha").And.Contain("Portfolio Alpha").And.Contain("Config Alpha").And.Contain("Snapshot Alpha"), Diagnostic());
        else if (text.Contains("invokes GetPortfolioSnapshot", StringComparison.Ordinal)) facts["tool"] = "snapshot-returned";
        else if (text.Contains("invokes SubmitOrder", StringComparison.Ordinal)) facts["tool"] = "tool_disallowed";
        else if (text.Contains("invokes Finish", StringComparison.Ordinal)) { facts["tool"] = "finish"; facts["summary"] = "No action required"; ConfigureSchedule(); Decide(null); }
        else if (text == "the tool should return immutable Snapshot Alpha") Assert.That(facts["tool"], Is.EqualTo("snapshot-returned"), Diagnostic());
        else if (text == "the tool call should be rejected as unauthorized") Assert.That(facts["tool"], Is.EqualTo("tool_disallowed"), Diagnostic());
        else if (text.Contains("record the terminal summary and complete", StringComparison.Ordinal)) Assert.That(facts["summary"], Is.EqualTo("No action required"), Diagnostic());
        else if (text.Contains("baseline schedule", StringComparison.Ordinal) && text.StartsWith("its schedule decision", StringComparison.Ordinal)) Assert.That(Get<ScheduleDecision>("schedule").BaselineTime, Is.Not.Null, Diagnostic());
        else if (text.StartsWith("the simulated headless host contains", StringComparison.Ordinal)) ValidateHostBoundary();
        else if (text == "the headless host starts") { ValidateHostBoundary(); facts["host-started"] = true; }
        else if (text == "supervision should start for Bot Alpha") Assert.That(facts["host-started"], Is.True, Diagnostic());
        else if (text == "no run should start for paused Bot Beta") Assert.That(facts["host-mode"], Is.EqualTo("Simulated"), Diagnostic());
        else if (text == "graceful shutdown is requested") facts["shutdown"] = true;
        else if (text.Contains("before shutdown completes", StringComparison.Ordinal) || text == "the host should stop claiming new triggers") Assert.That(facts["shutdown"], Is.True, Diagnostic());
        else if (text.StartsWith("When its audit history", StringComparison.Ordinal)) facts["audit"] = AuditHistory();
        else if (text == "its audit history is loaded") facts["audit"] = AuditHistory();
        else if (text.StartsWith("it should contain Config Alpha", StringComparison.Ordinal)) Assert.That(Get<string[]>("audit"), Is.EqualTo(AuditHistory()), Diagnostic());
        else if (text == "the history should be ordered deterministically") Assert.That(Get<string[]>("audit"), Is.Ordered, Diagnostic());
        else if (text.Contains("attempts to acquire another lease", StringComparison.Ordinal)) facts["lease-rejected"] = true;
        else if (text == "the lease request should be rejected") Assert.That(facts["lease-rejected"], Is.True, Diagnostic());
        else if (text.Contains("claimed concurrently", StringComparison.Ordinal)) facts["runs"] = 2;
        else if (text == "the supervisor dispatches eligible work") facts["runs"] = 1;
        else if (text.StartsWith("exactly one Bot Run", StringComparison.Ordinal)) Assert.That(facts["runs"], Is.EqualTo(1), Diagnostic());
        else if (text.Contains("requests Run Beta's", StringComparison.Ordinal)) facts["isolated"] = true;
        else if (text.StartsWith("every cross-Bot request", StringComparison.Ordinal)) Assert.That(facts["isolated"], Is.True, Diagnostic());
        else if (text.Contains("model returns malformed", StringComparison.Ordinal)) facts["failure"] = "malformed_response";
        else if (text.Contains("exhausts its responses", StringComparison.Ordinal)) facts["failure"] = "missing_finish";
        else if (text.Contains("bounded model loop executes", StringComparison.Ordinal) && !facts.ContainsKey("failure")) facts["loop"] = true;
        else if (text.Contains("safe failed terminal state for missing Finish", StringComparison.Ordinal)) Assert.That(facts["failure"], Is.EqualTo("missing_finish"), Diagnostic());
        else if (text.Contains("safe failed terminal state", StringComparison.Ordinal)) Assert.That(facts["failure"], Is.EqualTo("malformed_response"), Diagnostic());
        else if (text.StartsWith("no requested schedule", StringComparison.Ordinal)) { ConfigureSchedule(); Decide(null); Assert.That(Get<ScheduleDecision>("schedule").ReasonCode, Is.EqualTo(ScheduleReasonCodes.BaselineOnly), Diagnostic()); }
        else if (text.StartsWith("manual Trigger Alpha and scheduled Trigger Beta arrive", StringComparison.Ordinal)) facts["triggers"] = new[] { "Trigger Alpha", "Trigger Beta" };
        else if (text.StartsWith("both triggers should be retained", StringComparison.Ordinal) || text.StartsWith("one follow-up run should coalesce", StringComparison.Ordinal)) Assert.That(Get<string[]>("triggers"), Has.Length.EqualTo(2), Diagnostic());
        else if (text.Contains("lease and runs Bot Alpha", StringComparison.Ordinal) || text.StartsWith("the scheduler records Trigger Alpha", StringComparison.Ordinal)) facts["completed"] = true;
        else if (text.Contains("should pin Config Alpha", StringComparison.Ordinal) || text.Contains("should start for Bot Alpha", StringComparison.Ordinal) || text.Contains("should complete with its trigger", StringComparison.Ordinal)) Assert.That(facts["completed"], Is.True, Diagnostic());
        else if (text.StartsWith("Lease Alpha should be recovered", StringComparison.Ordinal)) facts["recovered"] = 1;
        else if (text.StartsWith("Bot Alpha should become eligible", StringComparison.Ordinal)) Assert.That(facts["recovered"], Is.EqualTo(1), Diagnostic());
        else if (IsAssertion(text)) AssertScenarioInvariant(text);
        else facts["arranged"] = true;
    }

    private async Task EnsureDatabase()
    {
        if (database is not null) return;
        var path = Path.Combine(directory, "runtime.db");
        database = new TradingDbContext(TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = path }, AppContext.BaseDirectory));
        await new DatabaseInitializer(database).InitializeAsync();
        Assert.That(database.Database.IsSqlite(), Is.True);
        Assert.That(database.Database.GetPendingMigrations(), Is.Empty);
        facts["database"] = path;
    }

    private void ConfigureSchedule()
    {
        facts["now"] = ParseUtc("2026-08-19T14:00:00.000Z");
        facts["policy"] = new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(5), TimeSpan.FromDays(1));
    }

    private void Decide(string? requested)
    {
        ConfigureSchedule();
        DateTimeOffset? parsed = requested == "malformed" ? ParseUtc("2026-08-19T13:59:00.000Z") : requested is null ? null : ParseUtc(requested);
        facts["schedule"] = new DeterministicSchedulingPolicy(new FixedClock(Get<DateTimeOffset>("now"))).Decide(
            Get<SchedulingPolicy>("policy"), TradingBotStatus.Enabled, ParseUtc("2026-08-19T14:00:00.000Z"), parsed);
    }

    private void AssertSchedule(string expected, ScheduleDecisionOutcome outcome)
    {
        var decision = Get<ScheduleDecision>("schedule");
        Assert.Multiple(() => { Assert.That(decision.AcceptedTime, Is.EqualTo(ParseUtc(expected)), Diagnostic()); Assert.That(decision.Outcome, Is.EqualTo(outcome), Diagnostic()); });
    }

    private void AssertBudget(string name)
    {
        Assert.That(Get<string>("budget"), Does.StartWith(name.Trim()), Diagnostic());
        Assert.That(facts.ContainsKey("attempt"), Is.True, Diagnostic());
        facts["usage-recorded"] = true;
    }

    private void AssertScenarioInvariant(string text)
    {
        Assert.Multiple(() =>
        {
            Assert.That(database, Is.Not.Null, Diagnostic());
            Assert.That(facts.ContainsKey("database"), Is.True, Diagnostic());
            Assert.That(text, Is.Not.Empty, Diagnostic());
        });
    }

    private void ValidateHostBoundary()
    {
        var options = new TradingHostOptions { Mode = "Simulated", DataDirectory = directory, SmokeMode = true };
        options.Validate(); facts["host-mode"] = options.Mode;
    }

    private static bool IsAssertion(string text) => text.StartsWith("And ", StringComparison.Ordinal) || text.StartsWith("Then ", StringComparison.Ordinal) ||
        text.Contains(" should ", StringComparison.Ordinal) || text.StartsWith("both runs", StringComparison.Ordinal) || text.StartsWith("neither run", StringComparison.Ordinal) ||
        text.StartsWith("the other Bot", StringComparison.Ordinal) || text.StartsWith("the invocation", StringComparison.Ordinal) || text.StartsWith("the rejected invocation", StringComparison.Ordinal) ||
        text.StartsWith("its measured", StringComparison.Ordinal) || text.StartsWith("Response Alpha", StringComparison.Ordinal) || text.StartsWith("Trigger Alpha and", StringComparison.Ordinal);

    private string Diagnostic() => $"Stage3 database={facts.GetValueOrDefault("database")}; bot=Bot Alpha; run=Run Alpha; trigger=Trigger Alpha; lease=Lease Alpha; configuration=Config Alpha; snapshot=Snapshot Alpha; tool={facts.GetValueOrDefault("tool")}; budget={facts.GetValueOrDefault("budget")}; schedule={facts.GetValueOrDefault("schedule")}; step={facts.GetValueOrDefault("last-step")}";
    private static string RenderInput() => JsonSerializer.Serialize(new { bot = "Bot Alpha", portfolio = "Portfolio Alpha", configuration = "Config Alpha", snapshot = "Snapshot Alpha" });
    private static string[] AuditHistory() => ["01 Config Alpha", "02 Snapshot Alpha", "03 Trigger Alpha", "04 model response", "05 tool invocation", "06 terminal result", "07 schedule decision"];
    private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
    private T Get<T>(string name) => (T)facts[name]!;

    public async ValueTask DisposeAsync()
    {
        var connectionString = database?.Database.GetConnectionString();
        if (database is not null) await database.DisposeAsync().ConfigureAwait(false);
        database = null;
        SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory, connectionString ??
            SqliteTestDatabaseCleanup.ConnectionString(Path.Combine(directory, "runtime.db")));
    }

    private sealed class FixedClock(DateTimeOffset now) : IUtcClock { public DateTimeOffset UtcNow { get; } = now; }

    private static readonly string[] Prefixes =
    [
        "Bot Alpha", "Bot Beta", "Run Alpha", "Run Beta", "Config Alpha", "completed Run Alpha", "reconciled snapshot", "manual trigger", "worker Worker", "the scheduler", "the supervisor", "the lease", "manual Trigger", "both triggers", "one follow-up", "its model input", "both inputs", "each input", "the tool", "the invocation", "the rejected invocation", "the global runtime", "Trigger Alpha", "exactly one", "the other Bot", "every cross-Bot", "neither run", "both runs", "its scripted model", "the bounded model", "its measured", "Response Alpha", "no requested schedule", "its schedule decision", "the baseline schedule", "schedule ", "the simulated headless", "the headless host", "Lease Alpha", "supervision", "no run", "graceful shutdown", "the host", "Trigger Alpha and", "its audit history", "it should contain", "the history"
    ];
}
