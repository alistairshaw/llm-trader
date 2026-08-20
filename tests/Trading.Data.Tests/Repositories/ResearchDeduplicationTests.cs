using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;

namespace Trading.Data.Tests.Repositories;

[Category("ResearchDeduplication")]
public sealed class ResearchDeduplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task EquivalentRequestsCreateOneRequestAndOneSubscriptionPerBot()
    {
        await using var database = await CreateAsync();
        var firstBot = await SeedBot(database.Context); var secondBot = await SeedBot(database.Context);
        var repository = new ResearchRequestDecisionRepository(database.Context);
        var first = await repository.DecideAsync(Candidate(firstBot), Principal(firstBot), Now, default);
        var repeat = await repository.DecideAsync(Candidate(firstBot), Principal(firstBot), Now, default);
        var subscriber = await repository.DecideAsync(Candidate(secondBot), Principal(secondBot), Now, default);
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.TypeOf<ResearchRequestPersistenceDecision.Queued>());
            Assert.That(repeat, Is.TypeOf<ResearchRequestPersistenceDecision.Subscribed>());
            Assert.That(subscriber, Is.TypeOf<ResearchRequestPersistenceDecision.Subscribed>());
            Assert.That(database.Context.ResearchRequests.Count(), Is.EqualTo(1));
            Assert.That(database.Context.ResearchSubscriptions.Count(), Is.EqualTo(2));
            Assert.That(((ResearchRequestPersistenceDecision.Subscribed)repeat).SubscriptionId,
                Is.EqualTo(((ResearchRequestPersistenceDecision.Queued)first).SubscriptionId));
        });
    }

    [Test]
    public async Task PrivateAndRestrictedRequestsDoNotCrossAuthorizationBoundaries()
    {
        await using var database = await CreateAsync();
        var owner = await SeedBot(database.Context); var stranger = await SeedBot(database.Context);
        var repository = new ResearchRequestDecisionRepository(database.Context);
        await repository.DecideAsync(Candidate(owner, ResearchVisibility.BotPrivate), Principal(owner), Now, default);
        var privateResult = await repository.DecideAsync(Candidate(stranger, ResearchVisibility.BotPrivate), Principal(stranger), Now, default);
        await repository.DecideAsync(Candidate(owner, ResearchVisibility.Restricted, "desk-a"), Principal(owner, "desk-a"), Now, default);
        var restrictedResult = await repository.DecideAsync(Candidate(stranger, ResearchVisibility.Restricted, "desk-b"), Principal(stranger, "desk-b"), Now, default);
        Assert.Multiple(() =>
        {
            Assert.That(privateResult, Is.TypeOf<ResearchRequestPersistenceDecision.Queued>());
            Assert.That(restrictedResult, Is.TypeOf<ResearchRequestPersistenceDecision.Queued>());
            Assert.That(database.Context.ResearchRequests.Count(), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task UnauthorizedRefreshCreatesNoRequestOrSubscription()
    {
        await using var database = await CreateAsync(); var bot = await SeedBot(database.Context);
        var result = await new ResearchRequestDecisionRepository(database.Context).DecideAsync(
            Candidate(bot) with { RefreshReportId = ResearchReportId.New() }, Principal(bot), Now, default);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<ResearchRequestPersistenceDecision.RefreshUnauthorized>());
            Assert.That(database.Context.ResearchRequests.Count(), Is.Zero);
            Assert.That(database.Context.ResearchSubscriptions.Count(), Is.Zero);
        });
    }

    [Test]
    public async Task FreshAuthorizedReportIsReusedAndExplicitRefreshIsLinkedAndQueued()
    {
        await using var database = await CreateAsync(); var bot = await SeedBot(database.Context);
        var repository = new ResearchRequestDecisionRepository(database.Context);
        var queued = (ResearchRequestPersistenceDecision.Queued)await repository.DecideAsync(Candidate(bot), Principal(bot), Now, default);
        var attempt = new ResearchRunAttempt(ResearchRunAttemptId.New(), queued.RequestId,
            new ResearchVersionPins("scripted", "research", "1", "prompt-v1", "tools-v1", "schema-v1"),
            new ResearchBudget(TimeSpan.FromMinutes(5), 1000, new Money(1, Currency.USD), 10, 10, 10_000, 2), Now);
        await new ResearchRequestRepository(database.Context).TryClaimQueuedAsync(queued.RequestId, new ResearchAttemptClaim(attempt, 1), default);
        var report = new ResearchReport(ResearchReportId.New(), "series-a", 1, queued.RequestId, "US:AAPL", "assess durable cash flow",
            ResearchVisibility.Shared, Now.AddDays(-1), Now, Now.AddDays(7), null, "{}", new string('a', 64),
            new ReportProvenance([new SourceCitation("regulatory filings", "10-k", Now.AddDays(-2), Now, new string('b', 64))]),
            new GeneratorMetadata(new ModelConfiguration("scripted", "research", 0, 1000), "prompt-v1", "tools-v1", "schema-v1"));
        await new ResearchReportRepository(database.Context).PublishAsync(report, attempt.Id, default);
        database.Context.ChangeTracker.Clear();
        var reused = await repository.DecideAsync(Candidate(bot), Principal(bot), Now, default);
        var refreshed = await repository.DecideAsync(Candidate(bot) with { RefreshReportId = report.Id }, Principal(bot), Now, default);
        Assert.Multiple(() =>
        {
            Assert.That(reused, Is.EqualTo(new ResearchRequestPersistenceDecision.Reused(report.Id)));
            Assert.That(refreshed, Is.TypeOf<ResearchRequestPersistenceDecision.Queued>());
            Assert.That(database.Context.ResearchRequests.Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ConcurrentEquivalentRequestsSerializeToOneQueuedRequest()
    {
        await using var database = await CreateAsync();
        var firstBot = await SeedBot(database.Context); var secondBot = await SeedBot(database.Context);
        await using var secondContext = new TradingDbContext(TradingDbContextFactory.CreateOptions(
            new DatabaseOptions { DatabasePath = database.DatabasePath }, TestContext.CurrentContext.TestDirectory));
        var first = new ResearchRequestDecisionRepository(database.Context).DecideAsync(Candidate(firstBot), Principal(firstBot), Now, default);
        var second = new ResearchRequestDecisionRepository(secondContext).DecideAsync(Candidate(secondBot), Principal(secondBot), Now, default);
        var results = await Task.WhenAll(first, second);
        database.Context.ChangeTracker.Clear();
        Assert.Multiple(() =>
        {
            Assert.That(results.Count(x => x is ResearchRequestPersistenceDecision.Queued), Is.EqualTo(1));
            Assert.That(results.Count(x => x is ResearchRequestPersistenceDecision.Subscribed), Is.EqualTo(1));
            Assert.That(database.Context.ResearchRequests.Count(), Is.EqualTo(1));
            Assert.That(database.Context.ResearchSubscriptions.Count(), Is.EqualTo(2));
        });
    }

    internal static AuthorizedResearchRequest Candidate(TradingBotId bot,
        ResearchVisibility visibility = ResearchVisibility.Shared, string? group = null)
    {
        var request = new ResearchRequest(ResearchRequestId.New(), bot, "US:AAPL", "assess durable cash flow",
            Now.AddDays(-1), visibility, new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(7)),
            visibility == ResearchVisibility.BotPrivate ? "private-" + bot : visibility == ResearchVisibility.Restricted ? "restricted-" + group : "shared-key",
            Now, [bot], group);
        request.BeginValidation(); request.Queue(); var subscription = ResearchSubscriptionId.New(); request.Subscribe(subscription, bot, Now);
        return new AuthorizedResearchRequest(request, subscription, "{}", null);
    }
    internal static ResearchPrincipal Principal(TradingBotId bot, params string[] groups) => new(bot.ToString(), ResearchPrincipalKind.TradingBot, groups);
    internal static async Task<TradingBotId> SeedBot(TradingDbContext context)
    {
        var id = TradingBotId.New(); context.TradingBots.Add(new TradingBotEntity { Id = id.ToString(), Name = id.ToString(), Status = "Enabled", CreatedAt = Now.ToUnixTimeMilliseconds(), UpdatedAt = Now.ToUnixTimeMilliseconds(), Version = 1 }); await context.SaveChangesAsync(); return id;
    }
    internal static async Task<TemporarySqliteDatabase> CreateAsync() { var db = await TemporarySqliteDatabase.CreateAsync(); await db.Context.Database.MigrateAsync(); return db; }
}
