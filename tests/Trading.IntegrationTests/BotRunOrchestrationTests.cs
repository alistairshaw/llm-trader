using NUnit.Framework;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Data;
using Trading.Engine.Runtime;

namespace Trading.IntegrationTests;

[Category("BotRunOrchestration")]
public sealed class BotRunOrchestrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 22, 0, 0, TimeSpan.Zero);

    [TestCase(FinishStatus.Completed, BotRunExecutionOutcome.Completed)]
    [TestCase(FinishStatus.Incomplete, BotRunExecutionOutcome.Completed)]
    public async Task CompleteRunPinsFactsPersistsAuditAndRetainsFollowUp(FinishStatus finishStatus,
        BotRunExecutionOutcome expected)
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Open();
        var clock = new FixedClock(Now.AddMinutes(1));
        var service = Create(context, database.Ids, clock);
        await Ingestion(context, database.Ids, clock).IngestAsync(
            new(database.BotId, BotRunTriggerType.Manual, "initial", clock.UtcNow), default);
        var requested = clock.UtcNow.AddMinutes(1);
        var arguments = $"{{\"nextRunAt\":\"{requested:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}\",\"status\":\"{finishStatus}\",\"summary\":\"done\",\"wakeReason\":\"review\"}}";
        var call = new ModelToolCall(database.Ids.NewToolInvocationId(), StageThreeTools.Finish, 1, arguments);
        var session = new ScriptedLlmClient([new ScriptedModelStep.Response(
            new AssistantResponse(null, [call], new ModelUsage(3, 2, 0.01m), null))], new NoDelay());

        var execution = service.ExecuteAsync(new(database.BotId, "host-a", TimeSpan.FromMinutes(5), session), default);
        await Ingestion(context, database.Ids, clock).IngestAsync(
            new(database.BotId, BotRunTriggerType.PortfolioEvent, "during run", clock.UtcNow, "event", "1"), default);
        var result = await execution;

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(result.ScheduleDecision, Is.Not.Null);
            Assert.That(result.ScheduleDecision!.RequestedTime, Is.EqualTo(requested));
            Assert.That(result.ScheduleDecision.Outcome, Is.EqualTo(ScheduleDecisionOutcome.Adjusted));
            Assert.That(result.RunId, Is.Not.Null);
        });
        var run = await new BotRunRepository(context).GetAsync(result.RunId!, default);
        Assert.Multiple(() =>
        {
            Assert.That(run!.ConfigurationVersionId, Is.EqualTo(database.ConfigurationId));
            Assert.That(run.PortfolioSnapshotId, Is.EqualTo(database.SnapshotId));
            Assert.That(run.InputRenderingHash, Has.Length.EqualTo(64));
            Assert.That(run.LeaseOwner, Is.Null);
            Assert.That(run.RequestedNextRunAt, Is.EqualTo(requested));
            Assert.That(run.AcceptedNextRunAt, Is.EqualTo(result.ScheduleDecision.AcceptedTime));
            Assert.That(run.ToolInvocations, Has.Count.EqualTo(1));
        });
        Assert.That((await new BotRunTriggerRepository(context).GetPendingAsync(database.BotId, default))
            .Select(x => x.Reason), Does.Contain("during run"));
    }

    [TestCase(ModelFailureKind.Timeout, BotRunExecutionOutcome.TimedOut, BotRunStatus.TimedOut)]
    [TestCase(ModelFailureKind.MalformedResponse, BotRunExecutionOutcome.Faulted, BotRunStatus.Faulted)]
    [TestCase(ModelFailureKind.ProviderFailure, BotRunExecutionOutcome.Faulted, BotRunStatus.Faulted)]
    [TestCase(ModelFailureKind.Cancellation, BotRunExecutionOutcome.Cancelled, BotRunStatus.Cancelled)]
    public async Task SafeTerminalPathsReleaseLeaseAndCreateNoActions(ModelFailureKind failure,
        BotRunExecutionOutcome expected, BotRunStatus status)
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Open();
        var clock = new FixedClock(Now.AddMinutes(1));
        await Ingestion(context, database.Ids, clock).IngestAsync(new(database.BotId, BotRunTriggerType.Manual, "run", clock.UtcNow), default);
        var response = new AssistantResponse(null, [], new ModelUsage(1, 1, 0), new ModelFailure(failure, "failure", false));
        var result = await Create(context, database.Ids, clock).ExecuteAsync(new(database.BotId, "host-a",
            TimeSpan.FromMinutes(5), new ScriptedLlmClient([new ScriptedModelStep.Response(response)], new NoDelay())), default);
        var run = await new BotRunRepository(context).GetAsync(result.RunId!, default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(run!.Status, Is.EqualTo(status));
            Assert.That(run.LeaseOwner, Is.Null);
            Assert.That(run.ToolInvocations, Is.Empty);
            Assert.That(run.FinishResult, Is.Null);
        });
    }

    [Test]
    [Category("RuntimeRecoveryOrShutdown")]
    public async Task ExpiredPreModelLeaseIsTerminalizedOnceAndRetainedForOneFollowUpRun()
    {
        await using var database = await Database.CreateAsync(); await using var context = database.Open();
        var claimClock = new FixedClock(Now);
        await Ingestion(context, database.Ids, claimClock).IngestAsync(
            new(database.BotId, BotRunTriggerType.Manual, "recover", Now), default);
        var runs = new BotRunRepository(context);
        var claim = await new BotTriggerCoalescingService(new TradingBotRepository(context),
            new BotRunTriggerRepository(context), runs, database.Ids, claimClock).TryClaimAsync(
            new(database.BotId, database.ConfigurationId, database.SnapshotId, "dead-host", TimeSpan.FromMinutes(1)), default);
        var claimed = (TriggerCoalescingResult.Claimed)claim;
        var recoveryClock = new FixedClock(Now.AddMinutes(1));
        var service = new RuntimeRecoveryService(runs, database.Ids, recoveryClock);

        var first = await service.RecoverExpiredLeasesAsync(default);
        var second = await service.RecoverExpiredLeasesAsync(default);
        var stored = await runs.GetAsync(claimed.Run.Id, default);
        var pending = await new BotRunTriggerRepository(context).GetPendingAsync(database.BotId, default);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new RecoveryResult(1, 0)));
            Assert.That(second, Is.EqualTo(new RecoveryResult(0, 0)));
            Assert.That(stored!.Status, Is.EqualTo(BotRunStatus.Faulted));
            Assert.That(stored.TerminalReason, Is.EqualTo("recovery_pre_model_checkpoint"));
            Assert.That(stored.LeaseOwner, Is.Null);
            Assert.That(pending.Count(x => x.SourceType == "runtime-recovery"), Is.EqualTo(1));
        });
    }

    [Test]
    [Category("RuntimeRecoveryOrShutdown")]
    public async Task ExpiredPostModelLeaseFaultsWithoutImplicitReplay()
    {
        await using var database = await Database.CreateAsync(); await using var context = database.Open();
        var clock = new FixedClock(Now);
        await Ingestion(context, database.Ids, clock).IngestAsync(new(database.BotId, BotRunTriggerType.Manual, "run", Now), default);
        var runs = new BotRunRepository(context);
        var claimed = (TriggerCoalescingResult.Claimed)await new BotTriggerCoalescingService(
            new TradingBotRepository(context), new BotRunTriggerRepository(context), runs, database.Ids, clock)
            .TryClaimAsync(new(database.BotId, database.ConfigurationId, database.SnapshotId, "dead-host", TimeSpan.FromMinutes(1)), default);
        claimed.Run.BeginReasoning();
        Assert.That(await runs.SaveAsync(claimed.Run, claimed.Run.Version, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var service = new RuntimeRecoveryService(runs, database.Ids,
            new FixedClock(Now.AddMinutes(1)));

        var result = await service.RecoverExpiredLeasesAsync(default);
        var stored = await runs.GetAsync(claimed.Run.Id, default);
        var pending = await new BotRunTriggerRepository(context).GetPendingAsync(database.BotId, default);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new RecoveryResult(0, 1)));
            Assert.That(stored!.Status, Is.EqualTo(BotRunStatus.Faulted));
            Assert.That(stored.TerminalReason, Is.EqualTo("recovery_model_execution_interrupted"));
            Assert.That(pending, Is.Empty);
        });
    }

    [Test]
    public async Task MissingTriggerDoesNotCreateRun()
    {
        await using var database = await Database.CreateAsync();
        await using var context = database.Open();
        var result = await Create(context, database.Ids, new FixedClock(Now.AddMinutes(1))).ExecuteAsync(
            new(database.BotId, "host", TimeSpan.FromMinutes(5), new ScriptedLlmClient([], new NoDelay())), default);
        Assert.That(result.Outcome, Is.EqualTo(BotRunExecutionOutcome.NoEligibleTriggers));
        Assert.That(result.RunId, Is.Null);
    }

    [Test]
    public async Task FirstOverBudgetResponseTerminatesAndReleasesLease()
    {
        await using var database = await Database.CreateAsync(); await using var context = database.Open();
        var clock = new FixedClock(Now.AddMinutes(1));
        await Ingestion(context, database.Ids, clock).IngestAsync(new(database.BotId, BotRunTriggerType.Manual, "run", clock.UtcNow), default);
        var response = new AssistantResponse(null, [], new ModelUsage(1000, 1, 0), null);
        var result = await Create(context, database.Ids, clock).ExecuteAsync(new(database.BotId, "host", TimeSpan.FromMinutes(5),
            new ScriptedLlmClient([new ScriptedModelStep.Response(response)], new NoDelay())), default);
        var run = await new BotRunRepository(context).GetAsync(result.RunId!, default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(BotRunExecutionOutcome.BudgetExceeded));
            Assert.That(run!.Status, Is.EqualTo(BotRunStatus.BudgetExceeded));
            Assert.That(run.LeaseOwner, Is.Null);
        });
    }

    [Test]
    public async Task LostLeaseStopsBeforeAnyModelOrToolExecution()
    {
        await using var database = await Database.CreateAsync(); await using var context = database.Open();
        var clock = new FixedClock(Now.AddMinutes(1));
        await Ingestion(context, database.Ids, clock).IngestAsync(new(database.BotId, BotRunTriggerType.Manual, "run", clock.UtcNow), default);
        var inner = new BotRunRepository(context); var rejecting = new RejectingRenewalRepository(inner);
        var bots = new TradingBotRepository(context);
        var input = new BotRunInputService(inner, bots, new PortfolioRepository(context), new PortfolioDecisionSnapshotRepository(context), inner);
        var session = new ScriptedLlmClient([], new NoDelay());
        var service = new BotRunOrchestrationService(bots, new PortfolioQueries(context),
            new BotTriggerCoalescingService(bots, new BotRunTriggerRepository(context), rejecting, database.Ids, clock), rejecting,
            input, new BoundedModelLoop(rejecting, new StageThreeToolDispatcher(rejecting, bots, input, clock), clock),
            new DeterministicSchedulingPolicy(clock), clock);
        var result = await service.ExecuteAsync(new(database.BotId, "host", TimeSpan.FromMinutes(5), session), default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(BotRunExecutionOutcome.LostLease));
            Assert.That(session.Requests, Is.Empty);
            Assert.That(session.ToolResults, Is.Empty);
        });
    }

    private static BotRunOrchestrationService Create(TradingDbContext context, TestIds ids, FixedClock clock)
    {
        var runs = new BotRunRepository(context);
        var bots = new TradingBotRepository(context);
        var input = new BotRunInputService(runs, bots, new PortfolioRepository(context),
            new PortfolioDecisionSnapshotRepository(context), runs);
        var dispatcher = new StageThreeToolDispatcher(runs, bots, input, clock);
        return new BotRunOrchestrationService(bots, new PortfolioQueries(context),
            new BotTriggerCoalescingService(bots, new BotRunTriggerRepository(context), runs, ids, clock), runs,
            input, new BoundedModelLoop(runs, dispatcher, clock), new DeterministicSchedulingPolicy(clock), clock);
    }
    private static BotTriggerIngestionService Ingestion(TradingDbContext context, TestIds ids, FixedClock clock) =>
        new(new BotRunTriggerRepository(context), ids, clock);
    private sealed class FixedClock(DateTimeOffset now) : IUtcClock { public DateTimeOffset UtcNow { get; } = now; }
    private sealed class NoDelay : IAsyncDelay { public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.CompletedTask; }
    private sealed class RejectingRenewalRepository(IBotRunRepository inner) : IBotRunRepository
    {
        public Task<BotRun?> GetAsync(BotRunId id, CancellationToken token) => inner.GetAsync(id, token);
        public Task<BotRunLeaseResult> TryClaimAsync(BotRunClaim claim, CancellationToken token) => inner.TryClaimAsync(claim, token);
        public Task<bool> RenewLeaseAsync(BotRunId id, string owner, DateTimeOffset expiry, long version, CancellationToken token) => Task.FromResult(false);
        public Task<PersistenceWriteResult> SaveAsync(BotRun run, long version, CancellationToken token) => inner.SaveAsync(run, version, token);
        public Task<IReadOnlyList<BotRunId>> GetExpiredLeaseRunIdsAsync(DateTimeOffset now, CancellationToken token) => inner.GetExpiredLeaseRunIdsAsync(now, token);
    }
    private sealed class TestIds : IRuntimeIdentifierGenerator
    {
        private int value;
        private string Next() => $"01J5QH8M00000000000001{Interlocked.Increment(ref value):D4}";
        public BotRunId NewBotRunId() => BotRunId.Parse(Next());
        public BotRunTriggerId NewTriggerId() => BotRunTriggerId.Parse(Next());
        public ToolInvocationId NewToolInvocationId() => ToolInvocationId.Parse(Next());
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string directory;
        private Database(string directory, string path) { this.directory = directory; Path = path; }
        private string Path { get; }
        public TestIds Ids { get; } = new();
        public TradingBotId BotId { get; private set; } = null!;
        public TradingBotConfigurationVersionId ConfigurationId { get; private set; } = null!;
        public PortfolioDecisionSnapshotId SnapshotId { get; private set; } = null!;
        public static async Task<Database> CreateAsync()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "run-orchestration", Guid.NewGuid().ToString("N"));
            var value = new Database(directory, System.IO.Path.Combine(directory, "runtime.db"));
            await using var context = value.Open(); await new DatabaseInitializer(context).InitializeAsync();
            var bot = new TradingBot(TradingBotId.New(), "alpha", Now);
            var configuration = bot.AddConfiguration(TradingBotConfigurationVersionId.New(),
                new InvestmentMandate("test", TimeSpan.FromDays(30), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])),
                new RiskPolicy([]), new ToolPolicy([new ToolAllowance(StageThreeTools.GetPortfolioSnapshot, 2), new ToolAllowance(StageThreeTools.Finish, 1)]),
                new RunBudget(TimeSpan.FromMinutes(10), 1000, new Money(10, Currency.USD), 5, 0, 0),
                new SchedulingPolicy(TimeSpan.FromHours(4), TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)),
                ExecutionMode.ResearchOnly, new ModelConfiguration("scripted", "test", 0, 1000), "v1", Now);
            var portfolioId = PortfolioId.New(); bot.AssignPortfolio(portfolioId, Now); bot.ActivateConfiguration(configuration.Id, Now); bot.Enable(Now);
            var portfolio = new Portfolio(portfolioId, "alpha portfolio", Currency.USD, new Money(100, Currency.USD), 0, Now); portfolio.AssignTradingBot(bot.Id);
            var snapshot = new PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId.New(), portfolio.Id, bot.Id, configuration.Id, Now,
                ReconciliationStatus.Reconciled, new Money(100, Currency.USD), new Money(100, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [],
                new DataFreshness(Now, Now, TimeSpan.FromMinutes(5)), Now);
            Assert.That(await new TradingBotRepository(context).AddAsync(bot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
            Assert.That(await new PortfolioRepository(context).AddAsync(portfolio, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
            Assert.That(await new PortfolioDecisionSnapshotRepository(context).PublishAsync(snapshot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
            value.BotId = bot.Id; value.ConfigurationId = configuration.Id; value.SnapshotId = snapshot.Id; return value;
        }
        public TradingDbContext Open() => new(TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = Path }, AppContext.BaseDirectory));
        public ValueTask DisposeAsync() { if (Directory.Exists(directory)) Directory.Delete(directory, true); return ValueTask.CompletedTask; }
    }
}
