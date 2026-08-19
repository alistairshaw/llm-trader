using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Engine.Runtime;

namespace Trading.Engine.Tests;

[Category("BotRunInput")]
public sealed class BotRunInputServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 14, 30, 0, TimeSpan.Zero);

    [Test]
    public async Task EquivalentFactsProduceGoldenByteIdenticalRenderingAndPersistAuditHash()
    {
        var first = Fixture.Create(); var second = Fixture.Create();
        var firstResult = await first.Service.PrepareAsync(first.Run.Id, default);
        var secondResult = await second.Service.PrepareAsync(second.Run.Id, default);
        var goldenHash = (await File.ReadAllTextAsync(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Fixtures", "bot-run-input-v1.sha256"))).Trim();
        Assert.Multiple(() =>
        {
            Assert.That(firstResult.Content, Is.EqualTo(secondResult.Content));
            Assert.That(firstResult.Sha256Hash, Is.EqualTo(secondResult.Sha256Hash));
            Assert.That(firstResult.Sha256Hash, Is.EqualTo(goldenHash));
            Assert.That(first.Audit.Version, Is.EqualTo(BotRunInputService.CurrentRenderingVersion));
            Assert.That(first.Audit.Hash, Is.EqualTo(firstResult.Sha256Hash));
            Assert.That(firstResult.Content, Does.Not.Contain("credential"));
        });
    }

    [TestCase(BotRunInputFailure.BotMismatch)]
    [TestCase(BotRunInputFailure.ConfigurationMismatch)]
    [TestCase(BotRunInputFailure.PortfolioMismatch)]
    public void RejectsCrossBoundaryIdentityMismatch(BotRunInputFailure mismatch)
    {
        var fixture = Fixture.Create(mismatch);
        var exception = Assert.ThrowsAsync<BotRunInputException>(() => fixture.Service.PrepareAsync(fixture.Run.Id, default));
        Assert.That(exception!.Failure, Is.EqualTo(mismatch));
        Assert.That(fixture.Audit.Hash, Is.Null);
    }

    [Test]
    public async Task GetPortfolioSnapshotReturnsTheRunsPinnedSnapshotEvenWhenANewerSnapshotExists()
    {
        var fixture = Fixture.Create(includeNewerSnapshot: true);
        var result = await fixture.Service.GetPortfolioSnapshotAsync(fixture.Run.Id, default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Snapshot.Id, Is.EqualTo(fixture.Snapshot.Id));
            Assert.That(result.CanonicalContent, Is.EqualTo(fixture.Snapshot.CanonicalContent));
            Assert.That(result.ContentHash, Is.EqualTo(fixture.Snapshot.ContentHash));
        });
    }

    private sealed record Fixture(BotRun Run, PortfolioDecisionSnapshot Snapshot, BotRunInputService Service, FakeAudit Audit)
    {
        public static Fixture Create(BotRunInputFailure? mismatch = null, bool includeNewerSnapshot = false)
        {
            var botId = TradingBotId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAV");
            var otherBotId = TradingBotId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAW");
            var configId = TradingBotConfigurationVersionId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAX");
            var otherConfigId = TradingBotConfigurationVersionId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAY");
            var portfolioId = PortfolioId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAZ");
            var snapshotId = PortfolioDecisionSnapshotId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FB0");
            var bot = new TradingBot(botId, "Golden Bot", Now.AddDays(-2));
            bot.AddConfiguration(configId, new InvestmentMandate("Preserve capital", TimeSpan.FromDays(365),
                    new UniverseDefinition(["Equity"], ["XNYS"], [Currency.USD])),
                new RiskPolicy([new RiskLimit("concentration", 25m, "percent")]),
                new ToolPolicy([new ToolAllowance(StageThreeTools.GetPortfolioSnapshot, 2), new ToolAllowance(StageThreeTools.Finish, 1)]),
                new RunBudget(TimeSpan.FromMinutes(5), 2000, new Money(1.25m, Currency.USD), 3, 0, 0),
                new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromHours(1), TimeSpan.FromDays(7),
                    [new UtcWeeklyWindow(DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(17))]),
                ExecutionMode.ResearchOnly, new ModelConfiguration("scripted", "v1", 0m, 500), "prompt-v1", Now.AddDays(-2));
            bot.ActivateConfiguration(configId, Now.AddDays(-1)); bot.AssignPortfolio(portfolioId, Now.AddDays(-1)); bot.Enable(Now.AddDays(-1));
            var portfolio = new Portfolio(portfolioId, "Primary", Currency.USD, new Money(10000m, Currency.USD), 10m, Now.AddDays(-3));
            portfolio.AssignTradingBot(mismatch == BotRunInputFailure.PortfolioMismatch ? otherBotId : botId);
            var snapshot = CreateSnapshot(snapshotId, portfolioId,
                mismatch == BotRunInputFailure.BotMismatch ? otherBotId : botId,
                mismatch == BotRunInputFailure.ConfigurationMismatch ? otherConfigId : configId, Now);
            var run = new BotRun(BotRunId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FB1"), botId, configId, snapshotId,
                new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0));
            run.AddTrigger(BotRunTriggerId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FB2"), BotRunTriggerType.Manual, "review", Now.AddMinutes(-2), "manual-1");
            run.BeginLeaseAcquisition(Now.AddMinutes(-1)); run.LeaseAcquired("host-a", Now.AddMinutes(4));
            var snapshots = new Dictionary<PortfolioDecisionSnapshotId, PortfolioDecisionSnapshot> { [snapshot.Id] = snapshot };
            if (includeNewerSnapshot)
            {
                var newer = CreateSnapshot(PortfolioDecisionSnapshotId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FB3"), portfolioId, botId, configId, Now.AddMinutes(1));
                snapshots[newer.Id] = newer;
            }
            var audit = new FakeAudit();
            var service = new BotRunInputService(new FakeRunRepository(run), new FakeBotRepository(bot),
                new FakePortfolioRepository(portfolio), new FakeSnapshotRepository(snapshots), audit);
            return new Fixture(run, snapshot, service, audit);
        }

        private static PortfolioDecisionSnapshot CreateSnapshot(PortfolioDecisionSnapshotId id, PortfolioId portfolioId,
            TradingBotId botId, TradingBotConfigurationVersionId configurationId, DateTimeOffset at) =>
            new(id, portfolioId, botId, configurationId, at, ReconciliationStatus.Reconciled,
                new Money(2000m, Currency.USD), new Money(5000m, Currency.USD), new Money(250m, Currency.USD), [], [], 12.5m, [],
                new DataFreshness(at.AddMinutes(-1), at, TimeSpan.FromMinutes(15)), at);
    }

    private sealed class FakeRunRepository(BotRun run) : IBotRunRepository
    {
        public Task<BotRun?> GetAsync(BotRunId id, CancellationToken cancellationToken) => Task.FromResult<BotRun?>(id == run.Id ? run : null);
        public Task<BotRunLeaseResult> TryClaimAsync(BotRunClaim claim, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RenewLeaseAsync(BotRunId runId, string leaseOwner, DateTimeOffset newExpiry, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> SaveAsync(BotRun value, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<BotRunId>> GetExpiredLeaseRunIdsAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class FakeBotRepository(TradingBot bot) : ITradingBotRepository
    {
        public Task<TradingBot?> GetAsync(TradingBotId id, CancellationToken cancellationToken) => Task.FromResult<TradingBot?>(id == bot.Id ? bot : null);
        public Task<PersistenceWriteResult> AddAsync(TradingBot value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> UpdateAsync(TradingBot value, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class FakePortfolioRepository(Portfolio portfolio) : IPortfolioRepository
    {
        public Task<Portfolio?> GetAsync(PortfolioId id, CancellationToken cancellationToken) => Task.FromResult<Portfolio?>(id == portfolio.Id ? portfolio : null);
        public Task<PersistenceWriteResult> AddAsync(Portfolio value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> UpdateAsync(Portfolio value, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class FakeSnapshotRepository(IReadOnlyDictionary<PortfolioDecisionSnapshotId, PortfolioDecisionSnapshot> snapshots) : IPortfolioDecisionSnapshotRepository
    {
        public Task<PortfolioDecisionSnapshot?> GetAsync(PortfolioDecisionSnapshotId id, CancellationToken cancellationToken) => Task.FromResult(snapshots.GetValueOrDefault(id));
        public Task<PersistenceWriteResult> PublishAsync(PortfolioDecisionSnapshot value, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class FakeAudit : IBotRunInputAuditWriter
    {
        public string? Version { get; private set; }
        public string? Hash { get; private set; }
        public Task<PersistenceWriteResult> StoreInputRenderingAsync(BotRunId runId, long expectedVersion, string renderingVersion, string renderingHash, CancellationToken cancellationToken)
        { Version = renderingVersion; Hash = renderingHash; return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
    }
}
