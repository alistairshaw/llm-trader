using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;

namespace Trading.Data.Tests.Repositories;

[Category("ResearchReports")]
[Category("ReportPublication")]
public sealed class ResearchReportPublicationRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 21, 0, 0, TimeSpan.Zero);
    private static readonly int[] ConcurrentVersions = [2, 3];
    private static readonly int[] AllVersions = [1, 2, 3];

    [Test]
    public async Task PublicationCompletesRequestAtomicallyIsIdempotentAndPreservesExactRefreshHistory()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync(); await database.Context.Database.MigrateAsync();
        var bot = await SeedBot(database.Context); var first = await ActiveCompleted(database.Context, bot, "first");
        var repository = new ResearchReportRepository(database.Context); var publication = Candidate(first.request, first.attempt, null, 'a');
        var report1 = await repository.PublishCompletedAsync(publication, default);
        var duplicate = await repository.PublishCompletedAsync(publication with { ReportId = ResearchReportId.New(), ContentHash = new string('b', 64) }, default);
        database.Context.ChangeTracker.Clear(); var completed = await new ResearchRequestRepository(database.Context).GetAsync(first.request.Id, default);
        Assert.Multiple(() => { Assert.That(duplicate.Id, Is.EqualTo(report1.Id)); Assert.That(completed!.Status, Is.EqualTo(ResearchRequestStatus.Completed)); Assert.That(completed.ResultReportId, Is.EqualTo(report1.Id)); });

        var second = await ActiveCompleted(database.Context, bot, "second");
        var report2 = await repository.PublishCompletedAsync(Candidate(second.request, second.attempt, report1.Id, 'c'), default);
        database.Context.ChangeTracker.Clear(); var catalog = new ResearchReportCatalogQueries(database.Context); var principal = new ResearchPrincipal(bot.ToString(), ResearchPrincipalKind.TradingBot);
        var historical = await catalog.GetAuthorizedVersionAsync(principal, report1.ReportSeriesId, 1, default);
        var current = await catalog.GetAuthorizedVersionAsync(principal, report1.ReportSeriesId, 2, default);
        Assert.Multiple(() => { Assert.That(report2.VersionNumber, Is.EqualTo(2)); Assert.That(report2.SupersedesReportId, Is.EqualTo(report1.Id)); Assert.That(historical!.Status, Is.EqualTo(ResearchReportStatus.Superseded)); Assert.That(historical.ContentHash, Is.EqualTo(new string('a', 64))); Assert.That(current!.ContentHash, Is.EqualTo(new string('c', 64))); });
    }

    [Test]
    public async Task InvalidForeignKeyRollsBackReportAndRequestCompletionAndPublishedFactsRejectMutation()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync(); await database.Context.Database.MigrateAsync(); var bot = await SeedBot(database.Context); var active = await ActiveCompleted(database.Context, bot, "rollback");
        var bad = Candidate(active.request, active.attempt, ResearchReportId.New(), 'd');
        Assert.That(async () => await new ResearchReportRepository(database.Context).PublishCompletedAsync(bad, default), Throws.Exception);
        database.Context.ChangeTracker.Clear(); var request = await new ResearchRequestRepository(database.Context).GetAsync(active.request.Id, default);
        Assert.That(request!.Status, Is.EqualTo(ResearchRequestStatus.Running)); Assert.That(await database.Context.ResearchReports.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task ConcurrentRefreshesAllocateDistinctMonotonicVersions()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync(); await database.Context.Database.MigrateAsync(); var bot = await SeedBot(database.Context);
        var originalRun = await ActiveCompleted(database.Context, bot, "original"); var original = await new ResearchReportRepository(database.Context).PublishCompletedAsync(Candidate(originalRun.request, originalRun.attempt, null, 'a'), default);
        var refreshA = await ActiveCompleted(database.Context, bot, "refresh-a"); var refreshB = await ActiveCompleted(database.Context, bot, "refresh-b");
        database.Context.ChangeTracker.Clear();
        var options = TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = database.DatabasePath }, TestContext.CurrentContext.TestDirectory);
        await using var contextA = new TradingDbContext(options); await using var contextB = new TradingDbContext(options);
        var results = await Task.WhenAll(new ResearchReportRepository(contextA).PublishCompletedAsync(Candidate(refreshA.request, refreshA.attempt, original.Id, 'b'), default),
            new ResearchReportRepository(contextB).PublishCompletedAsync(Candidate(refreshB.request, refreshB.attempt, original.Id, 'c'), default));
        Assert.That(results.Select(x => x.VersionNumber).Order(), Is.EqualTo(ConcurrentVersions));
        database.Context.ChangeTracker.Clear(); Assert.That(await database.Context.ResearchReports.Where(x => x.ReportSeriesId == original.ReportSeriesId).Select(x => x.VersionNumber).Order().ToArrayAsync(), Is.EqualTo(AllVersions));
        var published = await database.Context.ResearchReports.SingleAsync(x => x.VersionNumber == 3); published.ContentJson = "{}";
        Assert.That(async () => await database.Context.SaveChangesAsync(), Throws.TypeOf<InvalidOperationException>());
    }

    private static ResearchPublication Candidate(ResearchRequest request, ResearchRunAttempt attempt, ResearchReportId? refresh, char hash) => new(ResearchReportId.New(), request, attempt,
        "{\"schemaVersion\":1}", new string(hash, 64), new ReportProvenance([new SourceCitation("approved-fixtures", "fixture://source", Now.AddDays(-2), Now.AddDays(-1), new string('e', 64))]), Now.AddDays(-1), Now, Now.AddDays(7), refresh,
        new GeneratorMetadata(new ModelConfiguration("scripted", "research@1", 0, 1000), "prompt-v1", "tools-v1", "1"));
    private static async Task<(ResearchRequest request, ResearchRunAttempt attempt)> ActiveCompleted(TradingDbContext context, TradingBotId bot, string key)
    {
        var request = new ResearchRequest(ResearchRequestId.New(), bot, "US:AAPL", "Five-year outlook?", Now.AddDays(-1), ResearchVisibility.Shared, new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(7)), key, Now.AddMinutes(-5));
        request.BeginValidation(); request.Queue(); var requests = new ResearchRequestRepository(context); await requests.AddAsync(request, default);
        var attempt = new ResearchRunAttempt(ResearchRunAttemptId.New(), request.Id, new ResearchVersionPins("scripted", "research", "1", "prompt-v1", "tools-v1", "1"), new ResearchBudget(TimeSpan.FromMinutes(5), 1000, new Money(1, Currency.USD), 10, 5, 10000, 2), Now.AddMinutes(-4));
        var claim = (ResearchClaimResult.Acquired)await requests.TryClaimQueuedAsync(request.Id, new ResearchAttemptClaim(attempt, 1), default); attempt = claim.Attempt;
        attempt.Terminate(ResearchRunAttemptStatus.Completed, new ResearchUsage(TimeSpan.FromMinutes(1), 100, new Money(.1m, Currency.USD), 2, 1, 500, 0), "research.success", Now.AddMinutes(-1));
        await new ResearchRunAttemptRepository(context).SaveAsync(attempt, 1, default);
        return (ResearchRequest.Rehydrate(new ResearchRequestState(request.Id, request.RequestingBotId, request.Subject, request.Question, request.AsOf, ResearchRequestStatus.Running, request.Visibility, request.FreshnessRequirement, request.NormalizedResearchKey, request.RequestedAt, Now.AddMinutes(-4), null, null, false, request.AuthorizedSubscriberIds.ToArray(), request.RestrictedGroup, request.Subscriptions.Select(x => new ResearchSubscriptionState(x.Id, x.TradingBotId, x.SubscribedAt, x.NotificationStatus)).ToArray())), attempt);
    }
    private static async Task<TradingBotId> SeedBot(TradingDbContext context) { var id = TradingBotId.New(); context.TradingBots.Add(new TradingBotEntity { Id = id.ToString(), Name = "publisher-" + id, Status = "Enabled", CreatedAt = Now.ToUnixTimeMilliseconds(), UpdatedAt = Now.ToUnixTimeMilliseconds(), Version = 1 }); await context.SaveChangesAsync(); return id; }
}
