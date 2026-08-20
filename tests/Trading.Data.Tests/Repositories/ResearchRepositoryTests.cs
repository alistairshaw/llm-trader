using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;

namespace Trading.Data.Tests.Repositories;

[Category("ResearchRepositories")]
[Category("ResearchCatalog")]
[Category("ResearchToolAudit")]
public sealed class ResearchRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 18, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedTools = ["SearchWeb", "FetchWebDocument"];

    [Test]
    public async Task RequestClaimAndAttemptRoundTripWithOptimisticConcurrency()
    {
        await using var database = await CreateAsync(); var bot = await SeedBot(database.Context, "owner");
        var request = NewRequest(bot, ResearchVisibility.Shared); request.BeginValidation(); request.Queue();
        var requests = new ResearchRequestRepository(database.Context);
        Assert.That(await requests.AddAsync(request, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear(); var loaded = await requests.GetAsync(request.Id, default);
        Assert.Multiple(() => { Assert.That(loaded!.NormalizedResearchKey, Is.EqualTo("aapl-five-year")); Assert.That(loaded.Status, Is.EqualTo(ResearchRequestStatus.Queued)); });

        var attempt = NewAttempt(request.Id);
        var claim = await requests.TryClaimQueuedAsync(request.Id, new ResearchAttemptClaim(attempt, 1), default);
        Assert.That(claim, Is.TypeOf<ResearchClaimResult.Acquired>());
        Assert.That(await requests.TryClaimQueuedAsync(request.Id, new ResearchAttemptClaim(NewAttempt(request.Id), 2), default), Is.TypeOf<ResearchClaimResult.ConcurrencyConflict>());
        var attempts = new ResearchRunAttemptRepository(database.Context); database.Context.ChangeTracker.Clear();
        var active = await attempts.GetAsync(attempt.Id, default);
        Assert.Multiple(() => { Assert.That(active!.Status, Is.EqualTo(ResearchRunAttemptStatus.Running)); Assert.That(active.Budget.TokenLimit, Is.EqualTo(1000)); Assert.That(active.Versions.PromptVersion, Is.EqualTo("prompt-v1")); });
        active!.Terminate(ResearchRunAttemptStatus.Completed, Usage(), "research.success", Now.AddMinutes(1));
        Assert.That(await attempts.SaveAsync(active, 1, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await attempts.SaveAsync(active, 1, default), Is.EqualTo(new PersistenceWriteResult.ConcurrencyConflict(1, 2)));
    }

    [Test]
    public async Task ToolAuditIsAppendOnlyAndOrdered()
    {
        await using var database = await CreateAsync(); var bot = await SeedBot(database.Context, "audit"); var request = NewRequest(bot, ResearchVisibility.Shared); request.BeginValidation(); request.Queue();
        var requests = new ResearchRequestRepository(database.Context); await requests.AddAsync(request, default); var attempt = NewAttempt(request.Id);
        await requests.TryClaimQueuedAsync(request.Id, new ResearchAttemptClaim(attempt, 1), default);
        var repository = new ResearchRunAttemptRepository(database.Context);
        var second = new ResearchToolAudit(Guid.NewGuid().ToString("N"), attempt.Id, 2, "FetchWebDocument", 1, "{}", "Succeeded", Now, Now.AddSeconds(1), "{}", null, null, "{}");
        var first = second with { Id = Guid.NewGuid().ToString("N"), SequenceNumber = 1, ToolName = "SearchWeb" };
        Assert.That(await repository.AppendToolAuditAsync(second, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await repository.AppendToolAuditAsync(first, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That((await repository.GetToolAuditAsync(attempt.Id, default)).Select(x => x.ToolName), Is.EqualTo(ExpectedTools));
        Assert.That(await repository.AppendToolAuditAsync(first, default), Is.TypeOf<PersistenceWriteResult.UniquenessConflict>());
    }

    [Test]
    public async Task PublishedReportRoundTripsAndCatalogEnforcesVisibilityFreshnessAndExactVersion()
    {
        await using var database = await CreateAsync(); var owner = await SeedBot(database.Context, "owner"); var stranger = await SeedBot(database.Context, "stranger");
        var shared = await Publish(database.Context, owner, ResearchVisibility.Shared, "shared", 1, Now.AddDays(1));
        var privateReport = await Publish(database.Context, owner, ResearchVisibility.BotPrivate, "private", 1, Now.AddDays(1));
        var expired = await Publish(database.Context, owner, ResearchVisibility.Shared, "expired", 1, Now.AddMinutes(-1));
        var restricted = await Publish(database.Context, owner, ResearchVisibility.Restricted, "restricted", 1, Now.AddDays(1));
        database.Context.ChangeTracker.Clear(); var catalog = new ResearchReportCatalogQueries(database.Context);
        var outsider = new ResearchPrincipal(stranger.ToString(), ResearchPrincipalKind.TradingBot);
        var results = await catalog.SearchAsync(new ResearchReportSearch(outsider, Now, FreshOnly: true), default);
        Assert.That(results.Select(x => x.Id), Is.EqualTo(new[] { shared.Id }));
        Assert.That(await catalog.GetAuthorizedAsync(outsider, privateReport.Id, default), Is.Null);
        Assert.That(await catalog.GetAuthorizedAsync(outsider, restricted.Id, default), Is.Null);
        Assert.That((await catalog.GetAuthorizedAsync(new ResearchPrincipal(stranger.ToString(), ResearchPrincipalKind.TradingBot, ["desk-a"]), restricted.Id, default))!.Id, Is.EqualTo(restricted.Id));
        Assert.That((await catalog.GetAuthorizedVersionAsync(new ResearchPrincipal(owner.ToString(), ResearchPrincipalKind.TradingBot), "private", 1, default))!.Id, Is.EqualTo(privateReport.Id));
        Assert.That((await new ResearchReportRepository(database.Context).GetAsync(expired.Id, default))!.ContentHash, Is.EqualTo(expired.ContentHash));
        Assert.That(database.Context.ChangeTracker.Entries(), Is.Empty);
    }

    [Test]
    public async Task CatalogUsesResearchIndexesAndPublishedFactsCannotBeDeleted()
    {
        await using var database = await CreateAsync(); var owner = await SeedBot(database.Context, "plan"); var report = await Publish(database.Context, owner, ResearchVisibility.Shared, "series", 1, Now.AddDays(1));
        await using var command = database.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT * FROM research_reports WHERE subject_id = $subject ORDER BY generated_at DESC";
        command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("$subject", "US:AAPL"));
        await using var reader = await command.ExecuteReaderAsync(); var plan = new List<string>(); while (await reader.ReadAsync()) plan.Add(reader.GetString(3));
        Assert.That(string.Join(' ', plan), Does.Contain("IX_research_reports_subject_id_generated_at"));
        Assert.That(async () => await database.Context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM research_reports WHERE id = {report.Id.ToString()}"), Throws.Exception);
    }

    private static async Task<ResearchReport> Publish(TradingDbContext context, TradingBotId bot, ResearchVisibility visibility, string series, int version, DateTimeOffset expires)
    {
        var request = NewRequest(bot, visibility); request.BeginValidation(); request.Queue(); var requests = new ResearchRequestRepository(context); await requests.AddAsync(request, default);
        var attempt = NewAttempt(request.Id); await requests.TryClaimQueuedAsync(request.Id, new ResearchAttemptClaim(attempt, 1), default);
        var generatedAt = expires <= Now ? expires.AddHours(-1) : Now;
        var report = new ResearchReport(ResearchReportId.New(), series, version, request.Id, "US:AAPL", "five year outlook", visibility, generatedAt.AddDays(-1), generatedAt, expires,
            version == 1 ? null : ResearchReportId.New(), "{\"thesis\":\"durable\"}", new string(series[0] is 's' ? 'a' : series[0] is 'p' ? 'b' : 'c', 64),
            new ReportProvenance([new SourceCitation("SEC", "10-k", Now.AddDays(-2), Now.AddDays(-1), new string('d', 64))]),
            new GeneratorMetadata(new ModelConfiguration("scripted", "research", 0, 1000), "prompt-v1", "tools-v1", "report-v1"));
        Assert.That(await new ResearchReportRepository(context).PublishAsync(report, attempt.Id, default), Is.TypeOf<PersistenceWriteResult.Succeeded>()); return report;
    }
    private static ResearchRequest NewRequest(TradingBotId bot, ResearchVisibility visibility) => new(ResearchRequestId.New(), bot, "US:AAPL", "five year outlook", Now.AddDays(-1), visibility,
        new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(7)), "aapl-five-year", Now, [bot], visibility == ResearchVisibility.Restricted ? "desk-a" : null);
    private static ResearchRunAttempt NewAttempt(ResearchRequestId requestId) => new(ResearchRunAttemptId.New(), requestId,
        new ResearchVersionPins("scripted", "research", "1", "prompt-v1", "tools-v1", "report-v1"),
        new ResearchBudget(TimeSpan.FromMinutes(5), 1000, new Money(1, Currency.USD), 10, 10, 10000, 2), Now);
    private static ResearchUsage Usage() => new(TimeSpan.FromMinutes(1), 100, new Money(0.1m, Currency.USD), 1, 1, 100, 0);
    private static async Task<TradingBotId> SeedBot(TradingDbContext context, string name)
    {
        var id = TradingBotId.New(); context.TradingBots.Add(new TradingBotEntity { Id = id.ToString(), Name = name + id, Status = "Enabled", CreatedAt = Now.ToUnixTimeMilliseconds(), UpdatedAt = Now.ToUnixTimeMilliseconds(), Version = 1 }); await context.SaveChangesAsync(); return id;
    }
    private static async Task<TemporarySqliteDatabase> CreateAsync() { var database = await TemporarySqliteDatabase.CreateAsync(); await database.Context.Database.MigrateAsync(); return database; }
}
