using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Research;
using Trading.Data;
using Trading.Research;
using Trading.Research.Contracts;

namespace Trading.Host;

internal sealed class ResearchIdentifiers(bool deterministic) : IResearchIdentifierSource, IResearchNotificationIdentifierSource
{
    private readonly Queue<string> requests = new(["01J5QH8M000000000000000301", "01J5QH8M000000000000000302", "01J5QH8M000000000000000303", "01J5QH8M000000000000000304"]);
    private readonly Queue<string> attempts = new(["01J5QH8M000000000000000401", "01J5QH8M000000000000000402", "01J5QH8M000000000000000403"]);
    private readonly Queue<string> reports = new(["01J5QH8M000000000000000501", "01J5QH8M000000000000000502", "01J5QH8M000000000000000503"]);
    private readonly Queue<string> subscriptions = new(["01J5QH8M000000000000000601", "01J5QH8M000000000000000602", "01J5QH8M000000000000000603", "01J5QH8M000000000000000604"]);
    private readonly Queue<string> triggers = new(["01J5QH8M000000000000000701", "01J5QH8M000000000000000702", "01J5QH8M000000000000000703", "01J5QH8M000000000000000704"]);

    public ResearchRequestId NewRequestId() => ResearchRequestId.Parse(deterministic ? Next(requests) : ResearchRequestId.New().ToString());
    public ResearchRunAttemptId NewAttemptId() => ResearchRunAttemptId.Parse(deterministic ? Next(attempts) : ResearchRunAttemptId.New().ToString());
    public ResearchReportId NewReportId() => ResearchReportId.Parse(deterministic ? Next(reports) : ResearchReportId.New().ToString());
    public ResearchSubscriptionId NewSubscriptionId() => ResearchSubscriptionId.Parse(deterministic ? Next(subscriptions) : ResearchSubscriptionId.New().ToString());
    public BotRunTriggerId NewTriggerId() => BotRunTriggerId.Parse(deterministic ? Next(triggers) : BotRunTriggerId.New().ToString());
    private static string Next(Queue<string> values) { lock (values) return values.Dequeue(); }
}

internal sealed class ImmediateResearchDelay : IResearchDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class DurableResearchArtifactStore : IResearchArtifactStore
{
    public Task WriteDraftAsync(ResearchRunAttemptId attemptId, ResearchReportDraft draft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask; // The canonical draft is durably captured by the tool audit before publication.
    }
}

internal sealed class HostResearchCatalog(IResearchReportCatalogQueries queries) : IResearchReportCatalog
{
    public async Task<IReadOnlyList<ResearchCatalogEntry>> ListAsync(ResearchCatalogQuery query, CancellationToken cancellationToken)
    {
        var values = await queries.SearchAsync(new(query.Principal, query.At, query.Subject, query.NormalizedKey,
            FreshOnly: false, Offset: 0, Size: 100), cancellationToken).ConfigureAwait(false);
        return values.Select(x => new ResearchCatalogEntry(x.Id, x.SeriesId, x.Version, x.Subject, x.Status,
            x.DataCutoff, x.GeneratedAt, x.ExpiresAt, x.IsFresh)).ToArray();
    }

    public Task<ResearchReport?> GetAsync(ResearchPrincipal principal, ResearchReportId exactReportId,
        CancellationToken cancellationToken) => queries.GetAuthorizedAsync(principal, exactReportId, cancellationToken);
}

internal sealed class FixtureResearchModelSessionFactory(IResearchClock clock, IResearchDelay delay) : IResearchModelSessionFactory
{
    private const string Provider = "approved-fixtures";
    private const string SourceId = "fixture://regulatory/acme/2025-annual";
    private const string Hash = "72b4dda5698410b4c4072537bfe87f598315ad2316a3ff6c164ea1d8227d8925";
    private const string Published = "2026-02-20T14:00:00.000Z";

    public IResearchModelSession Create(ResearchRequest request, ResearchRunAttempt attempt)
    {
        var id = attempt.Id.ToString();
        var retrieved = Utc(clock.UtcNow);
        var cutoff = "2025-12-31T23:59:59.000Z";
        var refresh = Utc(clock.UtcNow.AddDays(7));
        var citation = $"{{\"contentHash\":\"{Hash}\",\"provider\":\"{Provider}\",\"publishedAt\":\"{Published}\",\"retrievedAt\":\"{retrieved}\",\"sourceIdentifier\":\"{SourceId}\"}}";
        var content = $"{{\"applicabilityLimits\":[\"fixture-only\"],\"claims\":[\"ACME evidence reviewed\"],\"conclusions\":{{}},\"contradictoryEvidence\":[\"none in fixture\"],\"executiveSummary\":\"Deterministic ACME fixture report {id}\",\"materialRisks\":[\"fixture scope\"],\"methodologyAndCalculations\":\"bounded fixture review\",\"schemaVersion\":1,\"supportingEvidence\":[\"annual filing\"],\"timeHorizons\":[\"long\"],\"uncertaintyAndMissingInformation\":[\"external evidence excluded\"]}}";
        var calls = new[]
        {
            new ResearchToolCall($"{id}:1", StageFourResearchTools.FetchWebDocument, 1, $"{{\"attemptId\":\"{id}\",\"maximumBytes\":10000,\"provider\":\"{Provider}\",\"sourceIdentifier\":\"{SourceId}\"}}"),
            new ResearchToolCall($"{id}:2", StageFourResearchTools.PublishReportDraft, 1, $"{{\"attemptId\":\"{id}\",\"citations\":[{citation}],\"content\":{content},\"dataCutoff\":\"{cutoff}\",\"recommendedRefreshAt\":\"{refresh}\"}}"),
            new ResearchToolCall($"{id}:3", StageFourResearchTools.FinishResearch, 1, $"{{\"attemptId\":\"{id}\",\"recommendedRefreshAt\":\"{refresh}\",\"status\":\"Completed\",\"summary\":\"fixture research complete\"}}")
        };
        return new ScriptedResearchModelSession([new ScriptedResearchModelStep.Response(new(null, calls, 120, 0))], delay);
    }

    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}

internal static partial class ResearchSmoke
{
    public static async Task RunAsync(IServiceProvider services, ILogger logger, CancellationToken token)
    {
        var requests = services.GetRequiredService<ResearchRequestService>();
        var defaults = services.GetRequiredService<ResearchRunDefaults>();
        var clock = services.GetRequiredService<IResearchClock>();
        var first = await requests.SubmitAsync(Command(SmokeFixture.BotId, ResearchVisibility.Shared, defaults, clock.UtcNow), token);
        var second = await requests.SubmitAsync(Command(SmokeFixture.BotTwoId, ResearchVisibility.Shared, defaults, clock.UtcNow), token);
        var results = await services.GetRequiredService<ResearchRunSupervisor>().DrainAsync(token);
        var catalog = services.GetRequiredService<IResearchReportCatalogQueries>();
        var shared = await catalog.SearchAsync(new(Principal(SmokeFixture.BotId), clock.UtcNow, "ACME", null, false, 0, 10), token);
        var report = shared.Single();

        var privateRequest = await requests.SubmitAsync(Command(SmokeFixture.BotId, ResearchVisibility.BotPrivate, defaults, clock.UtcNow, "Private fixture outlook?"), token);
        _ = await services.GetRequiredService<ResearchRunSupervisor>().DrainAsync(token);
        var privateReports = await catalog.SearchAsync(new(Principal(SmokeFixture.BotId), clock.UtcNow, "ACME", null, false, 0, 10), token);
        var denied = await catalog.GetAuthorizedAsync(Principal(SmokeFixture.BotTwoId), privateReports.Single(x => x.Id != report.Id).Id, token) is null;

        var refresh = await requests.SubmitAsync(Command(SmokeFixture.BotId, ResearchVisibility.Shared, defaults, clock.UtcNow, refresh: report.Id), token);
        _ = await services.GetRequiredService<ResearchRunSupervisor>().DrainAsync(token);
        var versions = await catalog.SearchAsync(new(Principal(SmokeFixture.BotId), clock.UtcNow, "ACME", null, false, 0, 10), token);
        var latest = versions.Where(x => x.SeriesId == report.SeriesId).OrderByDescending(x => x.Version).First();
        var exact = await catalog.GetAuthorizedAsync(Principal(SmokeFixture.BotId), report.Id, token) ?? throw new InvalidOperationException("Smoke report was not readable.");
        ResearchSmokeResult(logger, SmokeFixture.BotId.ToString(), SmokeFixture.BotTwoId.ToString(), first.Decision.ToString(), second.Decision.ToString(), report.Id.ToString(), exact.ContentHash, denied, latest.Version, latest.Id.ToString(), results.Count, privateRequest.Decision.ToString(), refresh.Decision.ToString());
    }

    private static ResearchRequestCommand Command(TradingBotId bot, ResearchVisibility visibility, ResearchRunDefaults defaults,
        DateTimeOffset now, string question = "What is the fixture-backed ACME outlook?", ResearchReportId? refresh = null) =>
        new(Principal(bot), bot, "ACME", question, ["outlook"], ["approved-fixtures"], now, visibility, null, null,
            TimeSpan.FromDays(30), "1", "1", defaults.Budget, ["approved-fixtures"], refresh);

    private static ResearchPrincipal Principal(TradingBotId id) => new(id.ToString(), ResearchPrincipalKind.TradingBot);

    [LoggerMessage(10, LogLevel.Information, "Research smoke BotA={BotA} BotB={BotB} First={FirstDecision} Second={SecondDecision} SharedReport={SharedReport} SharedHash={SharedHash} PrivateDenied={PrivateDenied} LatestVersion={LatestVersion} LatestReport={LatestReport} InitialRuns={InitialRuns} Private={PrivateDecision} Refresh={RefreshDecision} Shutdown=recoverable")]
    private static partial void ResearchSmokeResult(ILogger logger, string botA, string botB, string firstDecision,
        string secondDecision, string sharedReport, string sharedHash, bool privateDenied, int latestVersion,
        string latestReport, int initialRuns, string privateDecision, string refreshDecision);
}
