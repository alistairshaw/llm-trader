using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reqnroll;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Data;
using Trading.Engine.Runtime;
using Trading.Host;
using Trading.Research;
using Trading.Research.Contracts;
using Trading.Research.Sources;
using Trading.TestInfrastructure;

namespace Trading.AcceptanceTests.Support;

public enum Stage4Case
{
    AcceptRequest, InvalidBlank, InvalidUnbounded, InvalidSource, InvalidBudget, InvalidBot, PrivateInput,
    Deduplicate, Reuse, RefreshExpired, PrivateDeduplication, Publish, ImmutableOrRefresh, Mutation, Refresh,
    PrivateCatalog, FailedPublication, Provenance, PromptInjection, ForbiddenTool, Budget, CompletionNotifications,
    FailureNotifications, TriggerDelivery, Recovery, Shutdown, SharedJourney, ExactVersion, HostJourney,
}

/// <summary>
/// Scenario-scoped Stage 4 application driver. Step definitions select a named use case; this
/// class owns the production host/repository composition, deterministic substitutes, and durable
/// observations. No expected outcome is selected from scenario names or assertion wording.
/// </summary>
public sealed class Stage4ResearchDriver(ScenarioContext scenario) : IAsyncDisposable
{
    private const string Alpha = "01J5QH8M000000000000000101";
    private const string Beta = "01J5QH8M000000000000000201";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "trading-stage4-acceptance", Guid.NewGuid().ToString("N"));
    private Stage4Case? selected;
    private string? parameter;
    private IHost? host;
    private IServiceScope? scope;
    private TradingDbContext? database;
    private CaseObservation? observation;

    public bool IsArranged => selected is not null;

    public void Arrange(Stage4Case value) => selected = selected is null
        ? value
        : throw new InvalidOperationException("The Stage 4 use case is already arranged.");

    public static Stage4Case InvalidCase(string field) => field switch
    {
        "a blank question" => Stage4Case.InvalidBlank,
        "an unbounded question" => Stage4Case.InvalidUnbounded,
        "an unsupported source type" => Stage4Case.InvalidSource,
        "a budget above platform policy" => Stage4Case.InvalidBudget,
        "an unknown requesting Bot" => Stage4Case.InvalidBot,
        _ => throw new InvalidOperationException($"Unknown invalid request fixture: {field}"),
    };

    public Stage4Case BudgetCase(string step)
    {
        parameter = step["Research Run Alpha has a ".Length..].Split(" limit of ", StringSplitOptions.None)[0];
        return Stage4Case.Budget;
    }

    public void SetActionParameter(string action)
    {
        if (selected == Stage4Case.ImmutableOrRefresh)
            selected = action == "an update attempts to replace its findings" ? Stage4Case.Mutation : Stage4Case.Refresh;
        if (selected == Stage4Case.ForbiddenTool)
            parameter = action["the model requests ".Length..];
    }

    public void Act()
    {
        if (selected is null) throw new InvalidOperationException("No Stage 4 use case was arranged.");
        EnsureProductionHostAsync().GetAwaiter().GetResult();
        observation = ExecuteCaseAsync(selected.Value).GetAwaiter().GetResult();
    }

    public void AssertObserved()
    {
        var actual = observation ?? throw new InvalidOperationException("The Stage 4 action has not executed.");
        TestContext.Progress.WriteLine($"Stage4BusinessHash case={selected} hash={actual.BusinessHash}");
        Assert.Multiple(() =>
        {
            Assert.That(actual.Passed, Is.True, actual.Diagnostic);
            Assert.That(actual.RequestIds, Is.Not.Empty, actual.Diagnostic);
            Assert.That(actual.AttemptIds, Is.Not.Empty, actual.Diagnostic);
            Assert.That(actual.ReportIds, Is.Not.Empty, actual.Diagnostic);
            Assert.That(actual.SourceIds, Is.Not.Empty, actual.Diagnostic);
            Assert.That(actual.SubscriptionIds, Is.Not.Empty, actual.Diagnostic);
            Assert.That(actual.TriggerIds, Is.Not.Empty, actual.Diagnostic);
            Assert.That(actual.BotRunIds, Is.Not.Empty, actual.Diagnostic);
            Assert.That(actual.BusinessHash, Does.Match("^[a-f0-9]{64}$"), actual.Diagnostic);
        });
    }

    private async Task<CaseObservation> ExecuteCaseAsync(Stage4Case useCase)
    {
        var db = database ?? throw new InvalidOperationException("Database not initialized.");
        var services = scope!.ServiceProvider;
        var catalog = services.GetRequiredService<IResearchReportCatalogQueries>();
        var clock = services.GetRequiredService<IResearchClock>();
        var requests = new ResearchRequestService(new ResearchRequestDecisionRepository(db), new FreshIds(), clock);
        var alpha = TradingBotId.Parse(Alpha); var beta = TradingBotId.Parse(Beta);
        var alphaPrincipal = Principal(alpha); var betaPrincipal = Principal(beta);
        var before = await CountAsync("research_requests").ConfigureAwait(false);
        var passed = useCase switch
        {
            Stage4Case.AcceptRequest => await SubmitAdditionalAsync(requests, Valid(alpha, alphaPrincipal, clock.UtcNow, "A distinct bounded acceptance question?"),
                result => result.Decision == ResearchRequestDecision.Queued).ConfigureAwait(false),
            Stage4Case.InvalidBlank => await RejectAsync(requests, Valid(alpha, alphaPrincipal, clock.UtcNow, " ")).ConfigureAwait(false),
            Stage4Case.InvalidUnbounded => await RejectAsync(requests, Valid(alpha, alphaPrincipal, clock.UtcNow, new string('q', 4_001))).ConfigureAwait(false),
            Stage4Case.InvalidSource => await RejectAsync(requests, Valid(alpha, alphaPrincipal, clock.UtcNow, "Is this source allowed?") with
            { RequiredSourceTypes = ["public-web"], ApprovedSourceProviders = [FixtureResearchSource.ProviderName] }).ConfigureAwait(false),
            Stage4Case.InvalidBudget => await RejectAsync(requests, Valid(alpha, alphaPrincipal, clock.UtcNow, "Is this budget bounded?") with
            { Budget = new ResearchBudget(TimeSpan.FromMinutes(16), 4_000, new Money(10, Currency.USD), 12, 4, 100_000, 3) }).ConfigureAwait(false),
            Stage4Case.InvalidBot => await RejectAsync(requests, Valid(alpha, new ResearchPrincipal(beta.ToString(), ResearchPrincipalKind.TradingBot), clock.UtcNow, "Is this requester authorized?")).ConfigureAwait(false),
            Stage4Case.PrivateInput => await PrivateInputAsync(requests, alpha, alphaPrincipal, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.Deduplicate => await DeduplicateAsync(alpha, beta, clock.UtcNow, false).ConfigureAwait(false),
            Stage4Case.PrivateDeduplication => await DeduplicateAsync(alpha, beta, clock.UtcNow, true).ConfigureAwait(false),
            Stage4Case.Reuse => await ReuseAsync(requests, alpha, alphaPrincipal, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.RefreshExpired => await ExpiredAsync(catalog, alpha, alphaPrincipal, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.Refresh => await RefreshAsync(requests, catalog, alpha, alphaPrincipal, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.PrivateCatalog => await PrivateCatalogAsync(catalog, alphaPrincipal, betaPrincipal, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.Provenance => await ProvenanceAsync(clock).ConfigureAwait(false),
            Stage4Case.PromptInjection => await InjectionAsync(clock).ConfigureAwait(false),
            Stage4Case.ForbiddenTool => await ForbiddenToolAsync(parameter!, alpha, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.Budget => await BudgetAsync(parameter!, alpha, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.Mutation => await MutationAsync().ConfigureAwait(false),
            Stage4Case.FailedPublication => await FailedPublicationAsync(alpha, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.CompletionNotifications => await NotificationAsync(completed: true, alpha, beta, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.FailureNotifications => await NotificationAsync(completed: false, alpha, beta, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.TriggerDelivery => await TriggerAsync().ConfigureAwait(false),
            Stage4Case.Recovery => await RecoveryAsync(alpha, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.Shutdown => await ShutdownAsync().ConfigureAwait(false),
            Stage4Case.ExactVersion => await ExactVersionAsync(catalog, alphaPrincipal, clock.UtcNow).ConfigureAwait(false),
            Stage4Case.Publish or Stage4Case.SharedJourney or Stage4Case.HostJourney => await PublishedJourneyAsync(catalog, alphaPrincipal, betaPrincipal, clock.UtcNow).ConfigureAwait(false),
            _ => false,
        };
        var after = await CountAsync("research_requests").ConfigureAwait(false);
        if (useCase is Stage4Case.InvalidBlank or Stage4Case.InvalidUnbounded or Stage4Case.InvalidSource or Stage4Case.InvalidBudget or Stage4Case.InvalidBot)
            passed &= after == before;
        return await ObserveAsync(passed, useCase).ConfigureAwait(false);
    }

    private async Task EnsureProductionHostAsync()
    {
        if (database is not null) return;
        Directory.CreateDirectory(directory);
        host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Trading:Mode"] = "Simulated",
            ["Trading:DataDirectory"] = directory,
            ["Trading:SmokeMode"] = "true",
            ["Trading:ShutdownSeconds"] = "5",
            ["Research:Mode"] = "Fixture",
            ["Research:FixtureVersion"] = "v1",
            ["Research:ModelProvider"] = "scripted",
            ["Research:ModelId"] = "research",
            ["Research:ModelVersion"] = "1",
            ["Research:PromptVersion"] = "prompt-v1",
            ["Research:ToolSetVersion"] = "tools-v1",
            ["Research:ReportSchemaVersion"] = "1",
        }));
        await host.StartAsync().ConfigureAwait(false);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30))) await host.WaitForShutdownAsync(timeout.Token).ConfigureAwait(false);
        scope = host.Services.CreateScope(); database = scope.ServiceProvider.GetRequiredService<TradingDbContext>();
        Assert.That(database.Database.GetPendingMigrations(), Is.Empty);
    }

    private async Task<bool> DeduplicateAsync(TradingBotId alpha, TradingBotId beta, DateTimeOffset now, bool privateInputs)
    {
        var ids = new FreshIds(); var service = new ResearchRequestService(new ResearchRequestDecisionRepository(database!), ids, new FixedClock(now));
        var question = privateInputs ? "Private comparison acceptance question?" : "Shared deduplication acceptance question?";
        var first = Valid(alpha, Principal(alpha), now, question);
        var second = Valid(beta, Principal(beta), now, question);
        if (privateInputs)
        {
            first = first with { Visibility = ResearchVisibility.BotPrivate, PrivateInputHash = new string('a', 64) };
            second = second with { Visibility = ResearchVisibility.BotPrivate, PrivateInputHash = new string('b', 64) };
        }
        var a = await service.SubmitAsync(first, default).ConfigureAwait(false);
        var b = await service.SubmitAsync(second, default).ConfigureAwait(false);
        return privateInputs
            ? a.Decision == ResearchRequestDecision.Queued && b.Decision == ResearchRequestDecision.Queued && a.RequestId != b.RequestId
            : a.Decision == ResearchRequestDecision.Queued && b.Decision == ResearchRequestDecision.Subscribed && a.RequestId == b.RequestId;
    }

    private static async Task<bool> SubmitAdditionalAsync(ResearchRequestService service, ResearchRequestCommand command,
        Func<ResearchRequestResult, bool> predicate) => predicate(await service.SubmitAsync(command, default).ConfigureAwait(false));

    private static async Task<bool> RejectAsync(ResearchRequestService service, ResearchRequestCommand command) =>
        (await service.SubmitAsync(command, default).ConfigureAwait(false)).Decision == ResearchRequestDecision.Rejected;

    private static async Task<bool> PrivateInputAsync(ResearchRequestService service, TradingBotId bot, ResearchPrincipal principal, DateTimeOffset now)
    {
        var broadened = await service.SubmitAsync(Valid(bot, principal, now, "Private acceptance information?") with
        { Visibility = ResearchVisibility.Shared, PrivateInputHash = new string('a', 64) }, default).ConfigureAwait(false);
        var privateResult = await service.SubmitAsync(Valid(bot, principal, now, "Private acceptance information?") with
        { Visibility = ResearchVisibility.BotPrivate, PrivateInputHash = new string('a', 64) }, default).ConfigureAwait(false);
        return broadened.Decision == ResearchRequestDecision.Rejected && privateResult.Decision == ResearchRequestDecision.Queued;
    }

    private static async Task<bool> ReuseAsync(ResearchRequestService service, TradingBotId bot, ResearchPrincipal principal, DateTimeOffset now)
    {
        var result = await service.SubmitAsync(Valid(bot, principal, now, "What is the fixture-backed ACME outlook?"), default).ConfigureAwait(false);
        return result.Decision == ResearchRequestDecision.ReusedReport && result.ReportId is not null;
    }

    private static async Task<bool> RefreshAsync(ResearchRequestService service, IResearchReportCatalogQueries catalog,
        TradingBotId bot, ResearchPrincipal principal, DateTimeOffset now)
    {
        var reports = await catalog.SearchAsync(new(principal, now, "ACME", null, false, 0, 20), default).ConfigureAwait(false);
        var first = reports.Single(x => x.Version == 1 && reports.Count(y => y.SeriesId == x.SeriesId) == 2);
        var result = await service.SubmitAsync(Valid(bot, principal, now, "What is the fixture-backed ACME outlook?") with { RefreshReportId = first.Id }, default).ConfigureAwait(false);
        return result.Decision == ResearchRequestDecision.Queued;
    }

    private async Task<bool> ExpiredAsync(IResearchReportCatalogQueries catalog, TradingBotId bot,
        ResearchPrincipal principal, DateTimeOffset originalNow)
    {
        var later = originalNow.AddDays(31);
        var service = new ResearchRequestService(new ResearchRequestDecisionRepository(database!), new FreshIds(), new FixedClock(later));
        var result = await service.SubmitAsync(Valid(bot, principal, originalNow, "What is the fixture-backed ACME outlook?"), default).ConfigureAwait(false);
        var historical = await catalog.SearchAsync(new(principal, later, "ACME", null, false, 0, 20), default).ConfigureAwait(false);
        return result.Decision == ResearchRequestDecision.Queued && historical.Any(report => report.Version == 1 && !report.IsFresh);
    }

    private static async Task<bool> PrivateCatalogAsync(IResearchReportCatalogQueries catalog, ResearchPrincipal alpha,
        ResearchPrincipal beta, DateTimeOffset now)
    {
        var a = await catalog.SearchAsync(new(alpha, now, "ACME", null, false, 0, 20), default).ConfigureAwait(false);
        var b = await catalog.SearchAsync(new(beta, now, "ACME", null, false, 0, 20), default).ConfigureAwait(false);
        var privateReport = a.Single(x => b.All(y => y.Id != x.Id));
        return await catalog.GetAuthorizedAsync(beta, privateReport.Id, default).ConfigureAwait(false) is null &&
            await catalog.GetAuthorizedAsync(alpha, privateReport.Id, default).ConfigureAwait(false) is not null;
    }

    private async Task<bool> ProvenanceAsync(IResearchClock clock)
    {
        var source = new FixtureResearchSource(clock);
        var found = await source.SearchAsync(new(FixtureResearchSource.ProviderName, "ACME", clock.UtcNow), default).ConfigureAwait(false);
        var documents = new List<ResearchSourceResult>();
        foreach (var descriptor in found.Sources)
        {
            var fetched = await source.RetrieveAsync(descriptor.Provider, descriptor.SourceIdentifier, 100_000, default).ConfigureAwait(false);
            if (fetched.Document is not null) documents.Add(fetched.Document);
        }
        var complete = documents.Count == 2 && documents.All(item => !string.IsNullOrWhiteSpace(item.Provider) &&
            !string.IsNullOrWhiteSpace(item.SourceIdentifier) && item.RetrievedAt.Offset == TimeSpan.Zero &&
            item.ContentHash.Length == 64 && !string.IsNullOrWhiteSpace(item.License));
        var reportSources = await CountAsync("research_report_sources").ConfigureAwait(false);
        var fetchedAudits = await ScalarAsync<int>("SELECT COUNT(*) FROM research_tool_invocations WHERE tool_name = 'FetchWebDocument' AND status = 'Succeeded'").ConfigureAwait(false);
        return complete && reportSources == 3 && fetchedAudits == 3;
    }

    private async Task<bool> InjectionAsync(IResearchClock clock)
    {
        var source = new FixtureResearchSource(clock);
        var found = await source.SearchAsync(new(FixtureResearchSource.ProviderName, "ACME commentary", clock.UtcNow), default).ConfigureAwait(false);
        if (found.Sources.Count == 0) return false;
        var document = await source.RetrieveAsync(FixtureResearchSource.ProviderName, found.Sources[0].SourceIdentifier, 100_000, default).ConfigureAwait(false);
        var policy = scope!.ServiceProvider.GetRequiredService<ResearchToolPolicy>();
        var visibility = await ScalarAsync<int>("SELECT COUNT(*) FROM research_requests WHERE visibility = 'BotPrivate'").ConfigureAwait(false);
        return document.Document is not null && document.Document.UntrustedContent.Contains(ResearchEvidenceBoundary.Begin, StringComparison.Ordinal) &&
            document.Document.UntrustedContent.Contains(ResearchEvidenceBoundary.End, StringComparison.Ordinal) &&
            policy.Limit("SubmitOrder") == 0 && visibility == 1;
    }

    private async Task<bool> ForbiddenToolAsync(string tool, TradingBotId bot, DateTimeOffset now)
    {
        var services = scope!.ServiceProvider;
        var ids = new FreshIds();
        var requestService = new ResearchRequestService(new ResearchRequestDecisionRepository(database!), ids, new FixedClock(now));
        var submitted = await requestService.SubmitAsync(Valid(bot, Principal(bot), now, $"May the Research model call {tool}?"), default).ConfigureAwait(false);
        if (submitted.RequestId is null) return false;
        var defaults = services.GetRequiredService<ResearchRunDefaults>();
        var candidate = new ResearchRunAttempt(ids.NewAttemptId(), submitted.RequestId, defaults.Versions, defaults.Budget, now);
        var work = await services.GetRequiredService<IResearchOrchestrationRepository>().TryClaimAsync(submitted.RequestId, candidate, default).ConfigureAwait(false);
        if (work is null) return false;
        var result = await services.GetRequiredService<IResearchToolDispatcher>().DispatchAsync(work.Attempt, Principal(bot),
            new ResearchToolCall($"forbidden-{tool}", tool, 1, "{}"), default).ConfigureAwait(false);
        var audit = await services.GetRequiredService<IResearchRunAttemptRepository>().GetToolAuditAsync(work.Attempt.Id, default).ConfigureAwait(false);
        return !result.Succeeded && result.ResultCode == ResearchResultCodes.UnknownTool && audit.Single().Status == "Rejected";
    }

    private async Task<bool> BudgetAsync(string budget, TradingBotId bot, DateTimeOffset now)
    {
        var clock = new AdjustableClock(now);
        var ids = new FreshIds();
        var requestService = new ResearchRequestService(new ResearchRequestDecisionRepository(database!), ids, clock);
        var submitted = await requestService.SubmitAsync(Valid(bot, Principal(bot), now, $"Exercise the {budget} acceptance boundary?"), default).ConfigureAwait(false);
        if (submitted.RequestId is null) return false;
        var limit = new ResearchBudget(TimeSpan.FromSeconds(60), 1_000, new Money(1, Currency.USD), 4, 2, 4_096, 2);
        var versions = scope!.ServiceProvider.GetRequiredService<ResearchRunDefaults>().Versions;
        var candidate = new ResearchRunAttempt(ids.NewAttemptId(), submitted.RequestId, versions, limit, now);
        var work = await scope.ServiceProvider.GetRequiredService<IResearchOrchestrationRepository>().TryClaimAsync(submitted.RequestId, candidate, default).ConfigureAwait(false);
        if (work is null) return false;
        if (budget == "time") clock.Advance(TimeSpan.FromSeconds(61));
        var count = budget switch { "tool calls" => 5, "documents retrieved" => 3, "consecutive failures" => 3, _ => 1 };
        var calls = Enumerable.Range(1, count).Select(index => new ResearchToolCall($"budget-{index}", "BudgetProbe", 1, "{}")).ToArray();
        var response = new ResearchAssistantResponse(null, calls, budget == "tokens" ? 1_001 : 0, budget == "cost" ? 1.01m : 0);
        var attempts = scope.ServiceProvider.GetRequiredService<IResearchRunAttemptRepository>();
        var loop = new BoundedResearchModelLoop(new BudgetProbeDispatcher(attempts, clock, budget), attempts, clock);
        var session = new ScriptedResearchModelSession([new ScriptedResearchModelStep.Response(response)], new NoDelay());
        var result = await loop.ExecuteAsync(work.Attempt, Principal(bot), "bounded acceptance probe", work.AttemptVersion, session, default).ConfigureAwait(false);
        var stored = await attempts.GetAsync(work.Attempt.Id, default).ConfigureAwait(false);
        var expected = budget == "time" ? ResearchResultCodes.TimedOut : budget == "consecutive failures"
            ? ResearchResultCodes.ConsecutiveFailuresExceeded : ResearchResultCodes.BudgetExceeded;
        return result.ResultCode == expected && stored is not null && stored.Status is not ResearchRunAttemptStatus.Running and not ResearchRunAttemptStatus.WaitingForTool &&
            await ScalarAsync<int>($"SELECT COUNT(*) FROM research_reports WHERE research_run_id = '{work.Attempt.Id}'").ConfigureAwait(false) == 0;
    }

    private async Task<bool> MutationAsync()
    {
        var repository = scope!.ServiceProvider.GetRequiredService<IResearchReportRepository>();
        var id = ResearchReportId.Parse(await ScalarAsync<string>("SELECT id FROM research_reports ORDER BY id LIMIT 1").ConfigureAwait(false));
        var before = await repository.GetAsync(id, default).ConfigureAwait(false);
        var after = await repository.GetAsync(id, default).ConfigureAwait(false);
        return before is not null && after is not null && before.ContentHash == after.ContentHash &&
            typeof(IResearchReportRepository).GetMethods().All(method => method.Name != "UpdateAsync");
    }

    private async Task<bool> FailedPublicationAsync(TradingBotId bot, DateTimeOffset now)
    {
        var services = scope!.ServiceProvider;
        var ids = new FreshIds();
        var service = new ResearchRequestService(new ResearchRequestDecisionRepository(database!), ids, new FixedClock(now));
        var submitted = await service.SubmitAsync(Valid(bot, Principal(bot), now, "Will invalid citations be rejected durably?"), default).ConfigureAwait(false);
        if (submitted.RequestId is null) return false;
        var defaults = services.GetRequiredService<ResearchRunDefaults>();
        var candidate = new ResearchRunAttempt(ids.NewAttemptId(), submitted.RequestId, defaults.Versions, defaults.Budget, now);
        var work = await services.GetRequiredService<IResearchOrchestrationRepository>().TryClaimAsync(submitted.RequestId, candidate, default).ConfigureAwait(false);
        if (work is null) return false;
        try
        {
            await services.GetRequiredService<IResearchReportPublisher>().PublishAsync(work.Request, work.Attempt,
                new ResearchReportDraft("{\"schemaVersion\":1}", [], now.AddDays(-1), null), [], null, default).ConfigureAwait(false);
            return false;
        }
        catch (ResearchPublicationException exception)
        {
            var audits = await services.GetRequiredService<IResearchRunAttemptRepository>().GetToolAuditAsync(work.Attempt.Id, default).ConfigureAwait(false);
            return exception.ResultCode == ResearchResultCodes.CitationInvalid && audits.Single().ToolName == "ValidateReportDraft" &&
                audits.Single().Status == "Rejected" && await ScalarAsync<int>($"SELECT COUNT(*) FROM research_reports WHERE research_run_id = '{work.Attempt.Id}'").ConfigureAwait(false) == 0;
        }
    }

    private async Task<bool> NotificationAsync(bool completed, TradingBotId alpha, TradingBotId beta, DateTimeOffset now)
    {
        if (completed)
            return await ScalarAsync<int>("SELECT COUNT(*) FROM research_subscriptions WHERE notification_status = 'Delivered'").ConfigureAwait(false) == 4;
        var services = scope!.ServiceProvider; var ids = new FreshIds(); var clock = new FixedClock(now);
        var requestService = new ResearchRequestService(new ResearchRequestDecisionRepository(database!), ids, clock);
        var question = "Will every subscriber receive the failed outcome?";
        var first = await requestService.SubmitAsync(Valid(alpha, Principal(alpha), now, question), default).ConfigureAwait(false);
        var second = await requestService.SubmitAsync(Valid(beta, Principal(beta), now, question), default).ConfigureAwait(false);
        if (first.RequestId is null || second.Decision != ResearchRequestDecision.Subscribed) return false;
        var defaults = services.GetRequiredService<ResearchRunDefaults>();
        var candidate = new ResearchRunAttempt(ids.NewAttemptId(), first.RequestId, defaults.Versions, defaults.Budget, now);
        var orchestration = services.GetRequiredService<IResearchOrchestrationRepository>();
        var work = await orchestration.TryClaimAsync(first.RequestId, candidate, default).ConfigureAwait(false);
        if (work is null) return false;
        work.Attempt.Terminate(ResearchRunAttemptStatus.Failed,
            new ResearchUsage(TimeSpan.FromSeconds(1), 0, Money.Zero(Currency.USD), 0, 0, 0, 1), ResearchResultCodes.SourceProviderFailed, now.AddSeconds(1));
        if (await services.GetRequiredService<IResearchRunAttemptRepository>().SaveAsync(work.Attempt, work.AttemptVersion, default).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded) return false;
        if (await orchestration.TerminalizeAsync(first.RequestId, work.Attempt, ResearchRequestStatus.Failed, work.AttemptVersion + 1, default).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded) return false;
        var delivery = new ResearchNotificationDeliveryService(services.GetRequiredService<IResearchNotificationRepository>(), new TriggerIds(), clock);
        var batch = await delivery.DeliverPendingAsync(first.RequestId, 10, 3, default).ConfigureAwait(false);
        return batch.Delivered == 2 && await ScalarAsync<int>($"SELECT COUNT(*) FROM research_subscriptions WHERE research_request_id = '{first.RequestId}' AND notification_status = 'Delivered'").ConfigureAwait(false) == 2 &&
            await ScalarAsync<int>($"SELECT COUNT(*) FROM bot_run_triggers t JOIN research_subscriptions s ON s.id = t.source_id WHERE s.research_request_id = '{first.RequestId}' AND t.source_type = 'ResearchSubscription'").ConfigureAwait(false) == 2;
    }

    private async Task<bool> TriggerAsync()
    {
        var requestId = await ScalarAsync<string>("SELECT research_request_id FROM research_subscriptions WHERE notification_status = 'Delivered' GROUP BY research_request_id HAVING COUNT(*) = 2 LIMIT 1").ConfigureAwait(false);
        var subscriptions = await StringsAsync($"SELECT id FROM research_subscriptions WHERE research_request_id = '{requestId}' ORDER BY id").ConfigureAwait(false);
        var before = await CountAsync("bot_run_triggers").ConfigureAwait(false);
        var repository = scope!.ServiceProvider.GetRequiredService<IResearchNotificationRepository>();
        foreach (var subscription in subscriptions)
        {
            var result = await repository.DeliverAsync(ResearchSubscriptionId.Parse(subscription), BotRunTriggerId.New(),
                scope.ServiceProvider.GetRequiredService<IResearchClock>().UtcNow, default).ConfigureAwait(false);
            if (result is not ResearchNotificationDeliveryResult.AlreadyDelivered) return false;
        }
        var after = await CountAsync("bot_run_triggers").ConfigureAwait(false);
        var duplicates = await ScalarAsync<int>("SELECT COUNT(*) FROM (SELECT trading_bot_id, source_id, COUNT(*) c FROM bot_run_triggers WHERE source_type = 'ResearchSubscription' GROUP BY trading_bot_id, source_id HAVING c > 1)").ConfigureAwait(false);
        return subscriptions.Length == 2 && before == after && duplicates == 0;
    }

    private async Task<bool> RecoveryAsync(TradingBotId bot, DateTimeOffset now)
    {
        var services = scope!.ServiceProvider; var ids = new FreshIds();
        var requestService = new ResearchRequestService(new ResearchRequestDecisionRepository(database!), ids, new FixedClock(now));
        var submitted = await requestService.SubmitAsync(Valid(bot, Principal(bot), now, "Can abandoned Research recover deterministically?"), default).ConfigureAwait(false);
        if (submitted.RequestId is null) return false;
        var defaults = services.GetRequiredService<ResearchRunDefaults>();
        var candidate = new ResearchRunAttempt(ids.NewAttemptId(), submitted.RequestId, defaults.Versions, defaults.Budget, now);
        var repository = services.GetRequiredService<IResearchOrchestrationRepository>();
        var work = await repository.TryClaimAsync(submitted.RequestId, candidate, default).ConfigureAwait(false);
        if (work is null) return false;
        var recovery = new ResearchRestartRecovery(repository, new FixedClock(now.AddMinutes(20)), defaults);
        var recovered = await recovery.RecoverAsync(default).ConfigureAwait(false);
        var stored = await services.GetRequiredService<IResearchRunAttemptRepository>().GetAsync(work.Attempt.Id, default).ConfigureAwait(false);
        var request = await services.GetRequiredService<IResearchRequestRepository>().GetAsync(submitted.RequestId, default).ConfigureAwait(false);
        return recovered == 1 && stored?.ResultCode == ResearchResultCodes.RecoveryExpiredLease && request?.Status == ResearchRequestStatus.Queued;
    }

    private Task<bool> ShutdownAsync() => Task.FromResult(host is not null && !host.Services.GetRequiredService<RuntimeReadiness>().IsReady);

    private async Task<bool> ExactVersionAsync(IResearchReportCatalogQueries catalog, ResearchPrincipal principal, DateTimeOffset now)
    {
        var reports = await catalog.SearchAsync(new(principal, now, "ACME", null, false, 0, 20), default).ConfigureAwait(false);
        var series = reports.GroupBy(x => x.SeriesId).Single(x => x.Count() == 2).OrderBy(x => x.Version).ToArray();
        var exact = await catalog.GetAuthorizedVersionAsync(principal, series[0].SeriesId, 1, default).ConfigureAwait(false);
        if (!series.Select(x => x.Version).SequenceEqual([1, 2]) || exact?.Id != series[0].Id) return false;

        var services = scope!.ServiceProvider;
        var bot = await services.GetRequiredService<ITradingBotRepository>().GetAsync(TradingBotId.Parse(Alpha), default).ConfigureAwait(false);
        var snapshotId = PortfolioDecisionSnapshotId.Parse(await ScalarAsync<string>($"SELECT id FROM portfolio_decision_snapshots WHERE trading_bot_id = '{Alpha}' ORDER BY as_of DESC LIMIT 1").ConfigureAwait(false));
        var claim = await services.GetRequiredService<BotTriggerCoalescingService>().TryClaimAsync(new(
            TradingBotId.Parse(Alpha), bot!.ActiveConfigurationVersionId!, snapshotId, "stage4-acceptance", TimeSpan.FromMinutes(5)), default).ConfigureAwait(false);
        if (claim is not TriggerCoalescingResult.Claimed acquired) return false;
        acquired.Run.BeginReasoning(); acquired.Run.WaitForTool();
        var runs = services.GetRequiredService<IBotRunRepository>();
        if (await runs.SaveAsync(acquired.Run, acquired.Run.Version, default).ConfigureAwait(false) is not PersistenceWriteResult.Succeeded) return false;
        var dispatcher = services.GetRequiredService<IToolDispatcher>();
        var context = new ToolDispatchContext(acquired.Run.Id, acquired.Run.TradingBotId, acquired.Run.PortfolioSnapshotId);
        var listed = await dispatcher.DispatchAsync(context, new ModelToolCall(
            ToolInvocationId.Parse("01J5QH8M000000000000009501"), StageFourTradingTools.ListReports, 1,
            "{\"freshOnly\":false,\"offset\":0,\"size\":20,\"subject\":\"ACME\"}"), default).ConfigureAwait(false);
        var fetched = await dispatcher.DispatchAsync(context, new ModelToolCall(
            ToolInvocationId.Parse("01J5QH8M000000000000009502"), StageFourTradingTools.GetReport, 1,
            $"{{\"reportId\":\"{exact.Id}\",\"seriesId\":\"{exact.ReportSeriesId}\",\"version\":1}}"), default).ConfigureAwait(false);
        var audits = await ScalarAsync<int>($"SELECT COUNT(*) FROM bot_tool_invocations WHERE bot_run_id = '{acquired.Run.Id}' AND status = 'Completed'").ConfigureAwait(false);
        return listed.Result.Outcome == ToolExecutionOutcome.Succeeded && fetched.Result.Outcome == ToolExecutionOutcome.Succeeded &&
            fetched.Result.CanonicalResult.Contains(exact.ContentHash, StringComparison.Ordinal) && audits == 2;
    }

    private async Task<bool> PublishedJourneyAsync(IResearchReportCatalogQueries catalog, ResearchPrincipal alpha,
        ResearchPrincipal beta, DateTimeOffset now)
    {
        var a = await catalog.SearchAsync(new(alpha, now, "ACME", null, false, 0, 20), default).ConfigureAwait(false);
        var b = await catalog.SearchAsync(new(beta, now, "ACME", null, false, 0, 20), default).ConfigureAwait(false);
        var completeFacts = await ScalarAsync<int>("SELECT COUNT(*) FROM research_reports WHERE content_hash IS NOT NULL AND data_cutoff IS NOT NULL AND generated_at IS NOT NULL AND expires_at IS NOT NULL AND report_schema_version IS NOT NULL AND generator_metadata_json IS NOT NULL").ConfigureAwait(false);
        var provenance = await CountAsync("research_report_sources").ConfigureAwait(false);
        var delivered = await ScalarAsync<int>("SELECT COUNT(*) FROM research_subscriptions WHERE notification_status = 'Delivered'").ConfigureAwait(false);
        return a.Count == 3 && b.Count == 2 && completeFacts == 3 && provenance == 3 && delivered == 4 &&
            a.All(x => x.Status == ResearchReportStatus.Published || x.Status == ResearchReportStatus.Superseded) &&
            a.GroupBy(x => x.SeriesId).Any(x => x.Select(y => y.Version).Order().SequenceEqual([1, 2]));
    }

    private async Task<CaseObservation> ObserveAsync(bool passed, Stage4Case useCase)
    {
        var requestIds = await StringsAsync("SELECT id FROM research_requests ORDER BY id").ConfigureAwait(false);
        var attemptIds = await StringsAsync("SELECT id FROM research_runs ORDER BY id").ConfigureAwait(false);
        var reportIds = await StringsAsync("SELECT id FROM research_reports ORDER BY id").ConfigureAwait(false);
        var sourceIds = await StringsAsync("SELECT id FROM research_report_sources ORDER BY id").ConfigureAwait(false);
        var subscriptionIds = await StringsAsync("SELECT id FROM research_subscriptions ORDER BY id").ConfigureAwait(false);
        var triggerIds = await StringsAsync("SELECT id FROM bot_run_triggers ORDER BY id").ConfigureAwait(false);
        var botRunIds = await StringsAsync("SELECT id FROM bot_runs ORDER BY id").ConfigureAwait(false);
        var canonical = string.Join('\n', requestIds.Concat(attemptIds).Concat(reportIds).Concat(sourceIds).Concat(subscriptionIds).Concat(triggerIds));
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        var diagnostic = $"{Diagnostic(useCase)}; requests={string.Join(',', requestIds)}; attempts={string.Join(',', attemptIds)}; reports={string.Join(',', reportIds)}; sources={string.Join(',', sourceIds)}; subscriptions={string.Join(',', subscriptionIds)}; triggers={string.Join(',', triggerIds)}; botRuns={string.Join(',', botRunIds)}";
        return new(passed, requestIds, attemptIds, reportIds, sourceIds, subscriptionIds, triggerIds, botRunIds, hash, diagnostic);
    }

    private async Task<int> CountAsync(string table) => await ScalarAsync<int>($"SELECT COUNT(*) FROM {table}").ConfigureAwait(false);
    private async Task<T> ScalarAsync<T>(string sql)
    {
        var connection = database!.Database.GetDbConnection(); if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync().ConfigureAwait(false))!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
    private async Task<string[]> StringsAsync(string sql)
    {
        var connection = database!.Database.GetDbConnection(); if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.CommandText = sql; await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var values = new List<string>(); while (await reader.ReadAsync().ConfigureAwait(false)) values.Add(reader.GetString(0)); return values.ToArray();
    }

    private static ResearchRequestCommand Valid(TradingBotId bot, ResearchPrincipal principal, DateTimeOffset now, string question) =>
        new(principal, bot, "ACME", question, ["outlook"], [FixtureResearchSource.ProviderName], now,
            ResearchVisibility.Shared, null, null, TimeSpan.FromDays(30), "1", "1",
            new ResearchBudget(TimeSpan.FromMinutes(2), 4_000, new Money(10, Currency.USD), 12, 4, 100_000, 3),
            [FixtureResearchSource.ProviderName]);
    private static ResearchPrincipal Principal(TradingBotId id) => new(id.ToString(), ResearchPrincipalKind.TradingBot);
    private string Diagnostic(Stage4Case useCase) => $"Stage4 scenario={scenario.ScenarioInfo.Title}; case={useCase}; database={Path.Combine(directory, "smoke.db")}; parameter={parameter}";

    public async ValueTask DisposeAsync()
    {
        database = null;
        if (scope is IAsyncDisposable asyncScope) await asyncScope.DisposeAsync().ConfigureAwait(false);
        else scope?.Dispose();
        scope = null;
        if (host is not null)
        {
            if (host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync().ConfigureAwait(false);
            else host.Dispose();
            host = null;
        }
        SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory,
            SqliteTestDatabaseCleanup.HostConnectionString(Path.Combine(directory, "smoke.db")));
    }

    private sealed record CaseObservation(bool Passed, string[] RequestIds, string[] AttemptIds, string[] ReportIds,
        string[] SourceIds, string[] SubscriptionIds, string[] TriggerIds, string[] BotRunIds, string BusinessHash,
        string Diagnostic);
    private sealed class FixedClock(DateTimeOffset now) : IResearchClock { public DateTimeOffset UtcNow { get; } = now; }
    private sealed class AdjustableClock(DateTimeOffset now) : IResearchClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;
        public void Advance(TimeSpan duration) => UtcNow += duration;
    }
    private sealed class NoDelay : IResearchDelay { public Task DelayAsync(TimeSpan delay, CancellationToken token) => Task.CompletedTask; }
    private sealed class BudgetProbeDispatcher(IResearchRunAttemptRepository attempts, IResearchClock clock, string boundary) : IResearchToolDispatcher
    {
        public IReadOnlyList<ResearchToolDefinition> Definitions { get; } = [new("BudgetProbe", 1, "{\"additionalProperties\":false,\"properties\":{},\"required\":[],\"type\":\"object\"}")];
        public async Task<ResearchToolResult> DispatchAsync(ResearchRunAttempt attempt, ResearchPrincipal principal, ResearchToolCall call, CancellationToken token)
        {
            var failed = boundary == "consecutive failures";
            var usage = new ResearchUsage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 1,
                boundary == "documents retrieved" ? 1 : 0, boundary == "bytes retained" ? 4_097 : 0, failed ? 1 : 0);
            var audit = new ResearchToolAudit(call.CallId, attempt.Id, (await attempts.GetToolAuditAsync(attempt.Id, token).ConfigureAwait(false)).Count + 1,
                call.Name, call.SchemaVersion, call.CanonicalArguments, failed ? "Failed" : "Succeeded", clock.UtcNow, clock.UtcNow,
                "{}", failed ? ResearchResultCodes.ProviderFailed : null, failed ? "redacted" : null, "{}");
            _ = await attempts.AppendToolAuditAsync(audit, token).ConfigureAwait(false);
            return new(call.CallId, !failed, failed ? ResearchResultCodes.ProviderFailed : ResearchResultCodes.Success, "{}", usage);
        }
    }
    private sealed class FreshIds : IResearchIdentifierSource
    {
        private int requests = 9001; private int attempts = 9101; private int reports = 9201; private int subscriptions = 9301;
        public ResearchRequestId NewRequestId() => ResearchRequestId.Parse(Id(requests++));
        public ResearchRunAttemptId NewAttemptId() => ResearchRunAttemptId.Parse(Id(attempts++));
        public ResearchReportId NewReportId() => ResearchReportId.Parse(Id(reports++));
        public ResearchSubscriptionId NewSubscriptionId() => ResearchSubscriptionId.Parse(Id(subscriptions++));
        private static string Id(int suffix) => $"01J5QH8M00000000000000{suffix:D4}";
    }
    private sealed class TriggerIds : IResearchNotificationIdentifierSource
    {
        private int triggers = 9401;
        public BotRunTriggerId NewTriggerId() => BotRunTriggerId.Parse($"01J5QH8M00000000000000{triggers++:D4}");
    }
}
