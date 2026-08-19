using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;

namespace Trading.Data.Tests.Repositories;

[Category("BotRuntimePersistence")]
public sealed class BotRuntimeRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 20, 0, 0, TimeSpan.Zero);
    private static readonly string[] OrderedReasons = ["event", "second", "third"];
    private static readonly string[] ClaimedReasons = ["manual", "risk"];

    [Test]
    public async Task SourcedTriggersAreIdempotentAndUnsourcedTriggersRemainOrdered()
    {
        await using var db = await CreateAsync(); var ids = await SeedAsync(db.Context, "one");
        var repository = new BotRunTriggerRepository(db.Context);
        var sourced = Trigger(ids.Bot, Now.AddMinutes(1), "event", "cash", "42");
        Assert.That(await repository.AppendAsync(sourced, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await repository.AppendAsync(sourced with { Id = BotRunTriggerId.New() }, default), Is.TypeOf<PersistenceWriteResult.UniquenessConflict>());
        Assert.That(await repository.AppendAsync(Trigger(ids.Bot, Now.AddMinutes(3), "third"), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await repository.AppendAsync(Trigger(ids.Bot, Now.AddMinutes(2), "second"), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var pending = await repository.GetPendingAsync(ids.Bot, default);
        Assert.That(pending.Select(x => x.Reason), Is.EqualTo(OrderedReasons));
    }

    [Test]
    public async Task ClaimPinsFactsConsumesEveryTriggerAndRoundTripsAuditExactly()
    {
        await using var db = await CreateAsync(); var ids = await SeedAsync(db.Context, "one");
        var triggers = new BotRunTriggerRepository(db.Context);
        await triggers.AppendAsync(Trigger(ids.Bot, Now.AddMinutes(-2), "manual"), default);
        await triggers.AppendAsync(Trigger(ids.Bot, Now.AddMinutes(-1), "risk", "risk", "r-1"), default);
        var repository = new BotRunRepository(db.Context); var claim = Claim(ids, "host-a");
        var result = await repository.TryClaimAsync(claim, default);
        var acquired = result as BotRunLeaseResult.Acquired;
        Assert.That(acquired, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(acquired!.Run.Triggers.Select(x => x.Reason), Is.EqualTo(ClaimedReasons));
            Assert.That(acquired.Run.ConfigurationVersionId, Is.EqualTo(ids.Configuration));
            Assert.That(acquired.Run.PortfolioSnapshotId, Is.EqualTo(ids.Snapshot));
            Assert.That(acquired.Run.ModelTranscriptJson, Is.EqualTo("{}"));
            Assert.That(acquired.Run.Version, Is.EqualTo(1));
        });
        Assert.That(await triggers.GetPendingAsync(ids.Bot, default), Is.Empty);
        db.Context.ChangeTracker.Clear(); var loaded = await repository.GetAsync(claim.RunId, default);
        Assert.That(loaded!.Triggers, Is.EqualTo(acquired.Run.Triggers));
    }

    [Test]
    public async Task SameBotHasOneActiveRunWhileDifferentBotsCanRunConcurrently()
    {
        await using var db = await CreateAsync(); var first = await SeedAsync(db.Context, "one"); var second = await SeedAsync(db.Context, "two");
        var repository = new BotRunRepository(db.Context);
        Assert.That(await repository.TryClaimAsync(Claim(first, "host-a"), default), Is.TypeOf<BotRunLeaseResult.Acquired>());
        Assert.That(await repository.TryClaimAsync(Claim(first, "host-b"), default), Is.TypeOf<BotRunLeaseResult.ActiveLeaseConflict>());
        Assert.That(await repository.TryClaimAsync(Claim(second, "host-b"), default), Is.TypeOf<BotRunLeaseResult.Acquired>());
    }

    [Test]
    public async Task RenewalIsOwnerAndVersionCheckedAndTerminalSaveReleasesLease()
    {
        await using var db = await CreateAsync(); var ids = await SeedAsync(db.Context, "one"); var repository = new BotRunRepository(db.Context);
        var acquired = (BotRunLeaseResult.Acquired)await repository.TryClaimAsync(Claim(ids, "host-a"), default);
        Assert.That(await repository.RenewLeaseAsync(acquired.Run.Id, "host-b", Now.AddMinutes(10), 1, default), Is.False);
        Assert.That(await repository.RenewLeaseAsync(acquired.Run.Id, "host-a", Now.AddMinutes(10), 1, default), Is.True);
        db.Context.ChangeTracker.Clear(); var run = await repository.GetAsync(acquired.Run.Id, default);
        run!.Fault(new Usage(TimeSpan.FromSeconds(1), 1, new Money(0, Currency.USD), 0, 0, 0), Now.AddMinutes(1));
        Assert.That(await repository.SaveAsync(run, 2, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var saved = await repository.GetAsync(run.Id, default);
        Assert.Multiple(() => { Assert.That(saved!.LeaseOwner, Is.Null); Assert.That(saved.LeaseExpiresAt, Is.Null); Assert.That(saved.Status, Is.EqualTo(BotRunStatus.Faulted)); });
    }

    [Test]
    public async Task FailedClaimRollsBackTriggerConsumptionAndAuditDeletionIsRestricted()
    {
        await using var db = await CreateAsync(); var ids = await SeedAsync(db.Context, "one"); var triggerRepository = new BotRunTriggerRepository(db.Context);
        await triggerRepository.AppendAsync(Trigger(ids.Bot, Now, "pending"), default);
        var repository = new BotRunRepository(db.Context);
        Assert.That(async () => await repository.TryClaimAsync(Claim(ids with { Snapshot = PortfolioDecisionSnapshotId.New() }, "host"), default), Throws.InvalidOperationException);
        Assert.That(await triggerRepository.GetPendingAsync(ids.Bot, default), Has.Count.EqualTo(1));
        var acquired = (BotRunLeaseResult.Acquired)await repository.TryClaimAsync(Claim(ids, "host"), default);
        Assert.That(await repository.GetExpiredLeaseRunIdsAsync(Now.AddMinutes(6), default), Does.Contain(acquired.Run.Id));
        Assert.That(async () => await db.Context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM bot_runs WHERE id = {acquired.Run.Id.ToString()}"), Throws.TypeOf<SqliteException>());
    }

    private static PendingBotRunTrigger Trigger(TradingBotId bot, DateTimeOffset at, string reason, string? sourceType = null, string? sourceId = null) =>
        new(BotRunTriggerId.New(), bot, BotRunTriggerType.Manual, reason, at, at, sourceType, sourceId);
    private static BotRunClaim Claim(Ids ids, string owner) => new(BotRunId.New(), ids.Bot, ids.Configuration, ids.Snapshot, owner,
        Now, Now.AddMinutes(5), new Usage(TimeSpan.Zero, 0, new Money(0, Currency.USD), 0, 0, 0), 1, "{}", "v1");

    private static async Task<Ids> SeedAsync(TradingDbContext context, string suffix)
    {
        var bot = TradingBotId.New(); var config = TradingBotConfigurationVersionId.New(); var portfolio = PortfolioId.New(); var snapshot = PortfolioDecisionSnapshotId.New();
        context.TradingBots.Add(new TradingBotEntity { Id = bot.ToString(), Name = "bot-" + suffix, Status = "Enabled", CreatedAt = Now.ToUnixTimeMilliseconds(), UpdatedAt = Now.ToUnixTimeMilliseconds(), Version = 1 });
        context.TradingBotConfigurationVersions.Add(new TradingBotConfigurationVersionEntity
        {
            Id = config.ToString(),
            TradingBotId = bot.ToString(),
            VersionNumber = 1,
            InvestmentMandateJson = "{}",
            RiskPolicyJson = "{}",
            ToolPolicyJson = "{}",
            RunBudgetJson = "{}",
            SchedulingPolicyJson = "{}",
            ExecutionMode = "PaperTrading",
            ModelConfigurationJson = "{}",
            PromptVersion = "v1",
            ContentHash = new string(suffix[0] is 'o' ? 'a' : 'b', 64),
            CreatedAt = Now.ToUnixTimeMilliseconds()
        });
        context.Portfolios.Add(new PortfolioEntity
        {
            Id = portfolio.ToString(),
            Name = "portfolio-" + suffix,
            BaseCurrency = "USD",
            AssignedTradingBotId = bot.ToString(),
            Status = "Active",
            CapitalAllocationAmount = "100",
            CashReservePolicyJson = "{}",
            CreatedAt = Now.ToUnixTimeMilliseconds(),
            UpdatedAt = Now.ToUnixTimeMilliseconds(),
            Version = 1
        });
        context.PortfolioDecisionSnapshots.Add(new PortfolioDecisionSnapshotEntity
        {
            Id = snapshot.ToString(),
            PortfolioId = portfolio.ToString(),
            TradingBotId = bot.ToString(),
            ConfigurationVersionId = config.ToString(),
            AsOf = Now.ToUnixTimeMilliseconds(),
            ReconciliationStatus = "Reconciled",
            DataFreshnessJson = "{}",
            SnapshotSchemaVersion = 1,
            SnapshotJson = "{}",
            ContentHash = new string(suffix[0] is 'o' ? 'c' : 'd', 64),
            CreatedAt = Now.ToUnixTimeMilliseconds()
        });
        await context.SaveChangesAsync(); context.ChangeTracker.Clear(); return new Ids(bot, config, snapshot);
    }
    private static async Task<TemporarySqliteDatabase> CreateAsync() { var db = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(db.Context).InitializeAsync(); return db; }
    private sealed record Ids(TradingBotId Bot, TradingBotConfigurationVersionId Configuration, PortfolioDecisionSnapshotId Snapshot);
}
