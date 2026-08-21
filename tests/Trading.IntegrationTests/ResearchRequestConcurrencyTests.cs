using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Research;
using Trading.Data;
using Trading.TestInfrastructure;

namespace Trading.IntegrationTests;

[Category("ResearchRequests")]
public sealed class ResearchRequestConcurrencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task RestartedContextSubscribesToExistingEquivalentWork()
    {
        var directory = Path.Combine(Path.GetTempPath(), "research-request-integration", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "research.db");
        try
        {
            await using (var setup = Open(path)) await new DatabaseInitializer(setup).InitializeAsync();
            var firstBot = await Seed(path, "first"); var secondBot = await Seed(path, "second");
            ResearchRequestId queuedId;
            await using (var first = Open(path))
            {
                var queued = (ResearchRequestPersistenceDecision.Queued)await new ResearchRequestDecisionRepository(first)
                    .DecideAsync(Candidate(firstBot), Principal(firstBot), Now, default);
                queuedId = queued.RequestId;
            }
            await using (var restarted = Open(path))
            {
                var subscribed = (ResearchRequestPersistenceDecision.Subscribed)await new ResearchRequestDecisionRepository(restarted)
                    .DecideAsync(Candidate(secondBot), Principal(secondBot), Now, default);
                Assert.That(subscribed.RequestId, Is.EqualTo(queuedId));
                Assert.That((await new ResearchRequestRepository(restarted).GetAsync(queuedId, default))!.Subscriptions, Has.Count.EqualTo(2));
            }
        }
        finally { SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory, SqliteTestDatabaseCleanup.ConnectionString(path)); }
    }

    private static TradingDbContext Open(string path) => new(TradingDbContextFactory.CreateOptions(
        new DatabaseOptions { DatabasePath = path }, AppContext.BaseDirectory));
    private static async Task<TradingBotId> Seed(string path, string name)
    {
        var bot = new TradingBot(TradingBotId.New(), name, Now);
        await using var context = Open(path);
        Assert.That(await new TradingBotRepository(context).AddAsync(bot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        return bot.Id;
    }
    private static AuthorizedResearchRequest Candidate(TradingBotId bot)
    {
        var request = new ResearchRequest(ResearchRequestId.New(), bot, "US:AAPL", "assess durable cash flow",
            Now.AddDays(-1), ResearchVisibility.Shared, new DataFreshness(Now.AddDays(-1), Now, TimeSpan.FromDays(7)),
            "shared-key", Now, [bot]);
        request.BeginValidation(); request.Queue(); var id = ResearchSubscriptionId.New(); request.Subscribe(id, bot, Now);
        return new AuthorizedResearchRequest(request, id, "{}", null);
    }
    private static ResearchPrincipal Principal(TradingBotId bot) => new(bot.ToString(), ResearchPrincipalKind.TradingBot);
}
