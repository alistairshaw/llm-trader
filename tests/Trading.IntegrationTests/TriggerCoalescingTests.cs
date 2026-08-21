using NUnit.Framework;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Data;
using Trading.Engine.Runtime;
using Trading.TestInfrastructure;

namespace Trading.IntegrationTests;

[Category("TriggerCoalescing")]
public sealed class TriggerCoalescingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 22, 0, 0, TimeSpan.Zero);
    private static readonly string[] DepositReason = ["deposit posted"];
    private static readonly string[] FollowUpReasons = ["later identity", "earlier identity"];

    [Test]
    public async Task AcknowledgedAndDeduplicatedTriggersSurviveRestart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var bot = await database.SeedBotAsync("alpha");
        await using (var first = database.Open())
        {
            var service = Ingestion(first, database.Ids, Now);
            var request = new TriggerRequest(bot.Bot, BotRunTriggerType.PortfolioEvent, "deposit posted", Now, "ledger", "deposit-1");
            Assert.That((await service.IngestAsync(request, default)).Outcome, Is.EqualTo(TriggerIngestionOutcome.Accepted));
            Assert.That((await service.IngestAsync(request, default)).Outcome, Is.EqualTo(TriggerIngestionOutcome.Duplicate));
        }

        await using var restarted = database.Open();
        var pending = await new BotRunTriggerRepository(restarted).GetPendingAsync(bot.Bot, default);
        Assert.That(pending.Select(x => x.Reason), Is.EqualTo(DepositReason));
    }

    [Test]
    public async Task ActiveRunRetainsEveryReasonForExactlyOneOrderedFollowUpClaim()
    {
        await using var database = await TestDatabase.CreateAsync();
        var bot = await database.SeedBotAsync("alpha");
        await using var context = database.Open();
        var ingestion = Ingestion(context, database.Ids, Now);
        await ingestion.IngestAsync(new(bot.Bot, BotRunTriggerType.Manual, "initial", Now), default);
        var service = Coalescing(context, database.Ids, Now);
        var first = (TriggerCoalescingResult.Claimed)await service.TryClaimAsync(Claim(bot), default);

        await ingestion.IngestAsync(new(bot.Bot, BotRunTriggerType.RiskOrReconciliation, "later identity", Now, "risk", "b"), default);
        await ingestion.IngestAsync(new(bot.Bot, BotRunTriggerType.PortfolioEvent, "earlier identity", Now, "event", "a"), default);
        Assert.That(await service.TryClaimAsync(Claim(bot), default), Is.TypeOf<TriggerCoalescingResult.ActiveRun>());
        first.Run.Fault(new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0), Now.AddSeconds(1));
        Assert.That(await new BotRunRepository(context).SaveAsync(first.Run, 1, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        context.ChangeTracker.Clear();

        var followUp = (TriggerCoalescingResult.Claimed)await service.TryClaimAsync(Claim(bot), default);
        Assert.That(followUp.Run.Triggers.Select(x => x.Reason), Is.EqualTo(FollowUpReasons));
        Assert.That(await new BotRunTriggerRepository(context).GetPendingAsync(bot.Bot, default), Is.Empty);
    }

    [Test]
    public async Task FailedAndIneligibleClaimsRetainTriggers()
    {
        await using var database = await TestDatabase.CreateAsync();
        var enabled = await database.SeedBotAsync("enabled");
        var paused = await database.SeedBotAsync("paused", enabled: false);
        await using var context = database.Open();
        await Ingestion(context, database.Ids, Now).IngestAsync(new(enabled.Bot, BotRunTriggerType.BaselineSchedule, "future", Now.AddMinutes(1)), default);
        Assert.That(await Coalescing(context, database.Ids, Now).TryClaimAsync(Claim(enabled), default), Is.TypeOf<TriggerCoalescingResult.NoEligibleTriggers>());
        await Ingestion(context, database.Ids, Now).IngestAsync(new(enabled.Bot, BotRunTriggerType.Manual, "bad snapshot", Now), default);
        Assert.That(async () => await Coalescing(context, database.Ids, Now).TryClaimAsync(
            Claim(enabled) with { PortfolioSnapshotId = PortfolioDecisionSnapshotId.New() }, default), Throws.InvalidOperationException);
        context.ChangeTracker.Clear();
        Assert.That(await new BotRunTriggerRepository(context).GetPendingAsync(enabled.Bot, default), Has.Count.EqualTo(2));

        await Ingestion(context, database.Ids, Now).IngestAsync(new(paused.Bot, BotRunTriggerType.Manual, "paused", Now), default);
        Assert.That(await Coalescing(context, database.Ids, Now).TryClaimAsync(Claim(paused), default), Is.TypeOf<TriggerCoalescingResult.BotIneligible>());
        Assert.That(await new BotRunTriggerRepository(context).GetPendingAsync(paused.Bot, default), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentClaimsExcludeSameBotAndAllowDifferentBots()
    {
        await using var database = await TestDatabase.CreateAsync();
        var alpha = await database.SeedBotAsync("alpha");
        var beta = await database.SeedBotAsync("beta");
        await using (var seed = database.Open())
        {
            var ingestion = Ingestion(seed, database.Ids, Now);
            await ingestion.IngestAsync(new(alpha.Bot, BotRunTriggerType.Manual, "alpha", Now), default);
            await ingestion.IngestAsync(new(beta.Bot, BotRunTriggerType.BaselineSchedule, "beta", Now), default);
        }

        await using var a1 = database.Open(); await using var a2 = database.Open();
        var sameGate = new AsyncStartGate(2);
        var same = await Task.WhenAll(
            ClaimAfterGateAsync(Coalescing(a1, database.Ids, Now), Claim(alpha), sameGate),
            ClaimAfterGateAsync(Coalescing(a2, database.Ids, Now), Claim(alpha), sameGate));
        Assert.That(same.Count(x => x is TriggerCoalescingResult.Claimed), Is.EqualTo(1));
        Assert.That(same.Count(x => x is TriggerCoalescingResult.ActiveRun or TriggerCoalescingResult.NoEligibleTriggers), Is.EqualTo(1));

        await using var b = database.Open();
        Assert.That(await Coalescing(b, database.Ids, Now).TryClaimAsync(Claim(beta), default), Is.TypeOf<TriggerCoalescingResult.Claimed>());
    }

    private static async Task<TriggerCoalescingResult> ClaimAfterGateAsync(BotTriggerCoalescingService service,
        TriggerClaimRequest request, AsyncStartGate gate)
    { await gate.SignalAndWaitAsync(); return await service.TryClaimAsync(request, default); }

    private static BotTriggerIngestionService Ingestion(TradingDbContext context, TestIds ids, DateTimeOffset now) =>
        new(new BotRunTriggerRepository(context), ids, new FixedClock(now));
    private static BotTriggerCoalescingService Coalescing(TradingDbContext context, TestIds ids, DateTimeOffset now) =>
        new(new TradingBotRepository(context), new BotRunTriggerRepository(context), new BotRunRepository(context), ids, new FixedClock(now));
    private static TriggerClaimRequest Claim(BotFacts bot) => new(bot.Bot, bot.Configuration, bot.Snapshot, "test-host", TimeSpan.FromMinutes(5));

    private sealed class FixedClock(DateTimeOffset now) : IUtcClock { public DateTimeOffset UtcNow => now; }
    private sealed class TestIds : IRuntimeIdentifierGenerator
    {
        private int _runs;
        private int _triggers;
        private int _tools;
        public BotRunId NewBotRunId() => BotRunId.Parse(Value(Interlocked.Increment(ref _runs)));
        public BotRunTriggerId NewTriggerId() => BotRunTriggerId.Parse(Value(Interlocked.Increment(ref _triggers)));
        public ToolInvocationId NewToolInvocationId() => ToolInvocationId.Parse(Value(Interlocked.Increment(ref _tools)));
        private static string Value(int value) => $"01J5QH8M00000000000000{value:D4}";
    }
    private sealed class AsyncStartGate(int participants)
    {
        private int _arrived;
        private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SignalAndWaitAsync() { if (Interlocked.Increment(ref _arrived) == participants) _open.SetResult(); return _open.Task; }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _directory;
        private TestDatabase(string directory, string path) { _directory = directory; Path = path; }
        public string Path { get; }
        public TestIds Ids { get; } = new();
        public static async Task<TestDatabase> CreateAsync()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "trigger-tests", Guid.NewGuid().ToString("N"));
            var database = new TestDatabase(directory, System.IO.Path.Combine(directory, "runtime.db"));
            await using var context = database.Open(); await new DatabaseInitializer(context).InitializeAsync(); return database;
        }
        public TradingDbContext Open() => new(TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = Path }, AppContext.BaseDirectory));
        public async Task<BotFacts> SeedBotAsync(string name, bool enabled = true)
        {
            var bot = new TradingBot(TradingBotId.New(), name, Now);
            var configuration = bot.AddConfiguration(TradingBotConfigurationVersionId.New(),
                new InvestmentMandate("test", TimeSpan.FromDays(30), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])),
                new RiskPolicy([new RiskLimit("position", 100m, "percent")]), new ToolPolicy([new ToolAllowance("Finish", 1)]),
                new RunBudget(TimeSpan.FromMinutes(1), 1000, new Money(1m, Currency.USD), 5, 0, 0),
                new SchedulingPolicy(TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), TimeSpan.FromDays(1)),
                ExecutionMode.PaperTrading, new ModelConfiguration("scripted", "test", 0m, 1000), "v1", Now);
            bot.AssignPortfolio(PortfolioId.New(), Now); bot.ActivateConfiguration(configuration.Id, Now); if (enabled) bot.Enable(Now);
            var portfolio = new Portfolio(bot.PortfolioId!, name + " portfolio", Currency.USD, new Money(100m, Currency.USD), 0m, Now); portfolio.AssignTradingBot(bot.Id);
            var snapshot = new PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId.New(), portfolio.Id, bot.Id, configuration.Id, Now,
                ReconciliationStatus.Reconciled, new Money(100m, Currency.USD), new Money(100m, Currency.USD), Money.Zero(Currency.USD), [], [], 0m, [],
                new DataFreshness(Now, Now, TimeSpan.FromMinutes(5)), Now);
            await using var context = Open();
            Assert.That(await new TradingBotRepository(context).AddAsync(bot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
            Assert.That(await new PortfolioRepository(context).AddAsync(portfolio, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
            Assert.That(await new PortfolioDecisionSnapshotRepository(context).PublishAsync(snapshot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
            return new(bot.Id, configuration.Id, snapshot.Id);
        }
        public ValueTask DisposeAsync()
        {
            SqliteTestDatabaseCleanup.DeleteOwnedDirectory(_directory, SqliteTestDatabaseCleanup.ConnectionString(Path));
            return ValueTask.CompletedTask;
        }
    }
    private sealed record BotFacts(TradingBotId Bot, TradingBotConfigurationVersionId Configuration, PortfolioDecisionSnapshotId Snapshot);
}
