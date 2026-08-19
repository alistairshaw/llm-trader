using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Engine.Runtime;

namespace Trading.Engine.Tests;

[Category("ToolDispatch")]
public sealed class StageThreeToolDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 16, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task RegistryContainsExactlyTheTwoVersionOneTools()
    {
        Assert.That(StageThreeToolDispatcher.Definitions.Select(x => (x.Name, x.SchemaVersion)), Is.EqualTo(new[]
        { (StageThreeTools.GetPortfolioSnapshot, 1), (StageThreeTools.Finish, 1) }));
        await Task.CompletedTask;
    }

    [Test]
    public async Task SnapshotReturnsOnlyPinnedContentAndPersistsStartAndTerminalAudit()
    {
        var fixture = Fixture.Create();
        var result = await fixture.Dispatch(StageThreeTools.GetPortfolioSnapshot,
            $"{{\"snapshotId\":\"{fixture.Run.PortfolioSnapshotId}\"}}");
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ToolExecutionOutcome.Succeeded));
            Assert.That(result.Result.CanonicalResult, Does.Contain(fixture.Run.PortfolioSnapshotId.ToString()));
            Assert.That(fixture.Repository.Saves, Is.EqualTo(2));
            Assert.That(fixture.Run.ToolInvocations.Single().Status, Is.EqualTo(ToolInvocationStatus.Completed));
            Assert.That(fixture.Run.ToolInvocations.Single().Usage!.ToolCalls, Is.EqualTo(1));
        });
    }

    [TestCase("Other", 1, "{}", ToolAuthorizationOutcome.UnknownTool, ToolDispatchCodes.UnknownTool)]
    [TestCase(StageThreeTools.Finish, 2, "{}", ToolAuthorizationOutcome.UnsupportedSchemaVersion, ToolDispatchCodes.UnsupportedSchemaVersion)]
    [TestCase(StageThreeTools.Finish, 1, "{\"status\":\"Completed\",\"summary\":\"ok\",\"extra\":1}", ToolAuthorizationOutcome.InvalidArguments, ToolDispatchCodes.MalformedArguments)]
    [TestCase(StageThreeTools.Finish, 1, "{ \"status\":\"Completed\",\"summary\":\"ok\"}", ToolAuthorizationOutcome.InvalidArguments, ToolDispatchCodes.NonCanonicalArguments)]
    public async Task RejectionsHaveStableCodesAndAreAudited(string name, int version, string arguments,
        ToolAuthorizationOutcome authorization, string code)
    {
        var fixture = Fixture.Create();
        var result = await fixture.Dispatch(name, arguments, version);
        Assert.Multiple(() =>
        {
            Assert.That(result.Authorization.Outcome, Is.EqualTo(authorization));
            Assert.That(result.Authorization.Reason, Is.EqualTo(code));
            Assert.That(result.Result.CanonicalResult, Is.EqualTo($"{{\"code\":\"{code}\"}}"));
            Assert.That(fixture.Run.ToolInvocations.Single().Error, Is.EqualTo(code));
            Assert.That(fixture.Repository.Saves, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ToolAbsentFromPinnedPolicyIsRejectedWithoutExecution()
    {
        var fixture = Fixture.Create(allowedTools: [new ToolAllowance(StageThreeTools.Finish, 1)]);
        var result = await fixture.Dispatch(StageThreeTools.GetPortfolioSnapshot,
            $"{{\"snapshotId\":\"{fixture.Run.PortfolioSnapshotId}\"}}");
        Assert.That(result.Authorization.Reason, Is.EqualTo(ToolDispatchCodes.ToolDisallowed));
        Assert.That(fixture.Input.Calls, Is.Zero);
    }

    [Test]
    public async Task OversizedArgumentsAreRedactedAndCancellationIsAuditedWithoutExecution()
    {
        var oversized = Fixture.Create();
        var largeResult = await oversized.Dispatch(StageThreeTools.Finish, "{\"summary\":\"" + new string('s', StageThreeToolDispatcher.MaximumArgumentsBytes) + "\"}");
        var cancelled = Fixture.Create(); using var source = new CancellationTokenSource(); source.Cancel();
        var cancelledResult = await cancelled.Dispatcher.DispatchAsync(
            new ToolDispatchContext(cancelled.Run.Id, cancelled.Run.TradingBotId, cancelled.Run.PortfolioSnapshotId),
            new ModelToolCall(ToolInvocationId.New(), StageThreeTools.Finish, 1, "{\"status\":\"Completed\",\"summary\":\"done\"}"), source.Token);
        Assert.Multiple(() =>
        {
            Assert.That(largeResult.Authorization.Reason, Is.EqualTo(ToolDispatchCodes.ArgumentsTooLarge));
            Assert.That(oversized.Run.ToolInvocations.Single().Arguments, Is.EqualTo("{\"redacted\":\"arguments_too_large\"}"));
            Assert.That(cancelledResult.Authorization.Reason, Is.EqualTo(ToolDispatchCodes.Cancelled));
            Assert.That(cancelled.Run.ToolInvocations.Single().Error, Is.EqualTo(ToolDispatchCodes.Cancelled));
            Assert.That(cancelled.Input.Calls, Is.Zero);
        });
    }

    [Test]
    public async Task SnapshotIdentityMustMatchTheRunBeforeToolExecution()
    {
        var fixture = Fixture.Create();
        var result = await fixture.Dispatch(StageThreeTools.GetPortfolioSnapshot,
            $"{{\"snapshotId\":\"{PortfolioDecisionSnapshotId.New()}\"}}");
        Assert.That(result.Authorization.Reason, Is.EqualTo(ToolDispatchCodes.RunMismatch));
        Assert.That(fixture.Input.Calls, Is.Zero);
    }

    [Test]
    public async Task PerToolAndTotalBudgetsAreEnforcedFromPinnedConfiguration()
    {
        var perTool = Fixture.Create(allowedTools: [new ToolAllowance(StageThreeTools.GetPortfolioSnapshot, 1), new ToolAllowance(StageThreeTools.Finish, 1)]);
        perTool.Run.StartToolInvocation(ToolInvocationId.New(), StageThreeTools.GetPortfolioSnapshot, "{}", Now.AddSeconds(-2)).Fail("old", perTool.Run.Usage, Now.AddSeconds(-1));
        var perResult = await perTool.Dispatch(StageThreeTools.GetPortfolioSnapshot, $"{{\"snapshotId\":\"{perTool.Run.PortfolioSnapshotId}\"}}");
        var total = Fixture.Create(toolLimit: 0);
        var totalResult = await total.Dispatch(StageThreeTools.Finish, "{\"status\":\"Completed\",\"summary\":\"done\"}");
        Assert.Multiple(() =>
        {
            Assert.That(perResult.Authorization.Reason, Is.EqualTo(ToolDispatchCodes.PerToolBudgetExceeded));
            Assert.That(totalResult.Authorization.Reason, Is.EqualTo(ToolDispatchCodes.TotalToolBudgetExceeded));
        });
    }

    [Test]
    public async Task FinishCompletesOnceWithPairedUtcWakeRequestAndBlocksLaterCalls()
    {
        var fixture = Fixture.Create();
        var result = await fixture.Dispatch(StageThreeTools.Finish,
            "{\"nextRunAt\":\"2026-08-20T16:00:00.000Z\",\"status\":\"Completed\",\"summary\":\"No action.\",\"wakeReason\":\"Daily review\"}");
        var repeated = await fixture.Dispatch(StageThreeTools.Finish, "{\"status\":\"Completed\",\"summary\":\"again\"}");
        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ToolExecutionOutcome.Succeeded));
            Assert.That(fixture.Run.Status, Is.EqualTo(BotRunStatus.Completed));
            Assert.That(fixture.Run.FinishResult!.WakeReason, Is.EqualTo("Daily review"));
            Assert.That(repeated.Authorization.Reason, Is.EqualTo(ToolDispatchCodes.FinishAlreadyCalled));
            Assert.That(fixture.Run.ToolInvocations, Has.Count.EqualTo(1));
        });
    }

    private sealed record Fixture(BotRun Run, FakeRunRepository Repository, FakeInput Input, StageThreeToolDispatcher Dispatcher)
    {
        public static Fixture Create(IEnumerable<ToolAllowance>? allowedTools = null, int toolLimit = 3)
        {
            var botId = TradingBotId.New(); var configId = TradingBotConfigurationVersionId.New(); var portfolioId = PortfolioId.New();
            var snapshot = new PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId.New(), portfolioId, botId, configId, Now,
                ReconciliationStatus.Reconciled, new Money(1, Currency.USD), new Money(2, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [],
                new DataFreshness(Now, Now, TimeSpan.FromMinutes(1)), Now);
            var bot = new TradingBot(botId, "bot", Now.AddDays(-1));
            bot.AddConfiguration(configId, new InvestmentMandate("test", TimeSpan.FromDays(1), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])),
                new RiskPolicy([]), new ToolPolicy(allowedTools ?? [new ToolAllowance(StageThreeTools.GetPortfolioSnapshot, 2), new ToolAllowance(StageThreeTools.Finish, 1)]),
                new RunBudget(TimeSpan.FromMinutes(1), 100, new Money(1, Currency.USD), toolLimit, 0, 0),
                new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1), TimeSpan.FromDays(2)), ExecutionMode.ResearchOnly,
                new ModelConfiguration("scripted", "v1", 0, 100), "p1", Now.AddDays(-1));
            var run = new BotRun(BotRunId.New(), botId, configId, snapshot.Id, new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0));
            run.BeginLeaseAcquisition(Now.AddMinutes(-1)); run.LeaseAcquired("host", Now.AddMinutes(5)); run.BeginReasoning(); run.WaitForTool();
            var repository = new FakeRunRepository(run); var input = new FakeInput(snapshot);
            return new Fixture(run, repository, input, new StageThreeToolDispatcher(repository, new FakeBotRepository(bot), input, new FakeClock()));
        }
        public Task<ToolDispatchResult> Dispatch(string name, string arguments, int version = 1) => Dispatcher.DispatchAsync(
            new ToolDispatchContext(Run.Id, Run.TradingBotId, Run.PortfolioSnapshotId), new ModelToolCall(ToolInvocationId.New(), name, version, arguments), default);
    }

    private sealed class FakeClock : IUtcClock { private int ticks; public DateTimeOffset UtcNow => Now.AddMilliseconds(ticks++); }
    private sealed class FakeRunRepository(BotRun run) : IBotRunRepository
    {
        public int Saves { get; private set; }
        public Task<BotRun?> GetAsync(BotRunId id, CancellationToken token) => Task.FromResult<BotRun?>(id == run.Id ? run : null);
        public Task<PersistenceWriteResult> SaveAsync(BotRun value, long version, CancellationToken token) { Saves++; return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
        public Task<BotRunLeaseResult> TryClaimAsync(BotRunClaim claim, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> RenewLeaseAsync(BotRunId id, string owner, DateTimeOffset expiry, long version, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<BotRunId>> GetExpiredLeaseRunIdsAsync(DateTimeOffset now, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class FakeBotRepository(TradingBot bot) : ITradingBotRepository
    {
        public Task<TradingBot?> GetAsync(TradingBotId id, CancellationToken token) => Task.FromResult<TradingBot?>(id == bot.Id ? bot : null);
        public Task<PersistenceWriteResult> AddAsync(TradingBot value, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> UpdateAsync(TradingBot value, long version, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class FakeInput(PortfolioDecisionSnapshot snapshot) : IBotRunInputService
    {
        public int Calls { get; private set; }
        public Task<PinnedPortfolioSnapshot> GetPortfolioSnapshotAsync(BotRunId id, CancellationToken token) { Calls++; return Task.FromResult(new PinnedPortfolioSnapshot(snapshot.CanonicalContent, snapshot.ContentHash, snapshot.SnapshotSchemaVersion, snapshot)); }
        public Task<DeterministicBotRunInput> PrepareAsync(BotRunId id, CancellationToken token) => throw new NotSupportedException();
    }
}
