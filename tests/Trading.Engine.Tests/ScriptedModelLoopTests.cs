using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Engine.Runtime;

namespace Trading.Engine.Tests;

[Category("ScriptedModelLoop")]
public sealed class ScriptedModelLoopTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task FinishProducesDeterministicCompletedTranscriptAndUsage()
    {
        var first = Fixture.Create(); var second = Fixture.Create();
        var firstResult = await first.Execute(FinishResponse(10, 5, 0.125m));
        var secondResult = await second.Execute(FinishResponse(10, 5, 0.125m));
        Assert.Multiple(() =>
        {
            Assert.That(firstResult.Outcome, Is.EqualTo(RunOutcome.Completed));
            Assert.That(first.Run.Status, Is.EqualTo(BotRunStatus.Completed));
            Assert.That(first.Run.Usage.Tokens, Is.EqualTo(15));
            Assert.That(first.Run.Usage.Cost.Amount, Is.EqualTo(0.125m));
            Assert.That(first.Run.Usage.ToolCalls, Is.EqualTo(1));
            Assert.That(first.Run.ModelTranscriptJson, Is.EqualTo(second.Run.ModelTranscriptJson));
            Assert.That(first.Run.ModelTranscriptJson, Does.Contain("\"kind\":\"assistant\"").And.Contain("\"kind\":\"tool\""));
            Assert.That(first.Session.ToolResults, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task SnapshotResultReturnsToModelBeforeFinish()
    {
        var fixture = Fixture.Create();
        var snapshotCall = new ModelToolCall(ToolInvocationId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FC1"), StageThreeTools.GetPortfolioSnapshot, 1,
            $"{{\"snapshotId\":\"{fixture.Run.PortfolioSnapshotId}\"}}");
        var result = await fixture.Execute(
            new AssistantResponse("inspect", [snapshotCall], new ModelUsage(1, 1, 0), null), FinishResponse());
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Completed));
            Assert.That(fixture.Session.Requests, Has.Count.EqualTo(2));
            Assert.That(fixture.Session.ToolResults, Has.Count.EqualTo(2));
            Assert.That(fixture.Run.ToolInvocations.Select(x => x.ToolName), Is.EqualTo(new[] { StageThreeTools.GetPortfolioSnapshot, StageThreeTools.Finish }));
        });
    }

    [Test]
    public async Task ExhaustedScriptTerminatesSafelyWithoutInferredAction()
    {
        var fixture = Fixture.Create();
        var result = await fixture.Execute(new AssistantResponse("thinking", [], new ModelUsage(1, 1, 0), null));
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Faulted));
            Assert.That(fixture.Run.TerminalReason, Is.EqualTo(ModelLoopCodes.MissingFinish));
            Assert.That(fixture.Run.FinishResult, Is.Null);
            Assert.That(fixture.Run.ToolInvocations, Is.Empty);
        });
    }

    [TestCase(ModelFailureKind.Timeout, RunOutcome.TimedOut, ModelLoopCodes.WallClockExceeded)]
    [TestCase(ModelFailureKind.MalformedResponse, RunOutcome.Faulted, ModelLoopCodes.MalformedResponse)]
    [TestCase(ModelFailureKind.ProviderFailure, RunOutcome.Faulted, ModelLoopCodes.ProviderFailure)]
    [TestCase(ModelFailureKind.Cancellation, RunOutcome.Cancelled, ModelLoopCodes.Cancelled)]
    public async Task ModelFailuresHaveStableSafeTerminalResults(ModelFailureKind kind, RunOutcome expected, string code)
    {
        var fixture = Fixture.Create();
        var result = await fixture.Execute(new AssistantResponse(null, [], new ModelUsage(2, 3, 0.01m), new ModelFailure(kind, "redacted", false)));
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(fixture.Run.TerminalReason, Is.EqualTo(code));
            Assert.That(fixture.Run.Usage.Tokens, Is.EqualTo(5));
            Assert.That(fixture.Run.FinishResult, Is.Null);
        });
    }

    [TestCase("tokens", ModelLoopCodes.TokenBudgetExceeded)]
    [TestCase("cost", ModelLoopCodes.CostBudgetExceeded)]
    [TestCase("tools", ModelLoopCodes.ToolBudgetExceeded)]
    [TestCase("research", ModelLoopCodes.ResearchBudgetExceeded)]
    [TestCase("proposals", ModelLoopCodes.ProposalBudgetExceeded)]
    public async Task ExactBudgetBoundaryStopsBeforeNextAction(string boundary, string code)
    {
        var initial = boundary switch
        {
            "tools" => new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 1, 0, 0),
            "research" => new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 1, 0),
            "proposals" => new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 1),
            _ => new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0),
        };
        var budget = new RunBudget(TimeSpan.FromMinutes(1), boundary == "tokens" ? 5 : 100,
            new Money(boundary == "cost" ? 0.25m : 1m, Currency.USD), boundary == "tools" ? 1 : 3, 0, 0);
        var fixture = Fixture.Create(budget, initial);
        var response = new AssistantResponse("boundary", [], new ModelUsage(boundary == "tokens" ? 5 : 0, 0, boundary == "cost" ? 0.25m : 0), null);
        var result = await fixture.Execute(response);
        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.BudgetExceeded));
            Assert.That(fixture.Run.TerminalReason, Is.EqualTo(code));
            Assert.That(fixture.Session.Requests.Count, Is.EqualTo(boundary is "tools" or "research" or "proposals" ? 0 : 1));
        });
    }

    [Test]
    public async Task WallClockAndIterationLimitsStopWithoutFurtherModelAction()
    {
        var timed = Fixture.Create(clockNow: Now.AddMinutes(2), budget: new RunBudget(TimeSpan.FromMinutes(1), 100, new Money(1, Currency.USD), 3, 0, 0));
        var timeout = await timed.Execute(FinishResponse());
        var iterated = Fixture.Create(limits: new ModelLoopLimits(2, 3));
        var iteration = await iterated.Execute(
            new AssistantResponse("one", [], new ModelUsage(0, 0, 0), null),
            new AssistantResponse("two", [], new ModelUsage(0, 0, 0), null), FinishResponse());
        Assert.Multiple(() =>
        {
            Assert.That(timeout.Outcome, Is.EqualTo(RunOutcome.TimedOut));
            Assert.That(timed.Session.Requests, Is.Empty);
            Assert.That(iteration.Outcome, Is.EqualTo(RunOutcome.BudgetExceeded));
            Assert.That(iterated.Run.TerminalReason, Is.EqualTo(ModelLoopCodes.IterationBudgetExceeded));
            Assert.That(iterated.Session.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task ConsecutiveEmptyResponsesAndCancellationAreBounded()
    {
        var failed = Fixture.Create(limits: new ModelLoopLimits(5, 2));
        var failure = await failed.Execute(
            new AssistantResponse(null, [], new ModelUsage(0, 0, 0), null),
            new AssistantResponse(null, [], new ModelUsage(0, 0, 0), null), FinishResponse());
        var cancelled = Fixture.Create(); using var source = new CancellationTokenSource(); source.Cancel();
        var cancellation = await cancelled.ExecuteWithCancellation([FinishResponse()], source.Token);
        Assert.Multiple(() =>
        {
            Assert.That(failure.Outcome, Is.EqualTo(RunOutcome.Faulted));
            Assert.That(failed.Run.TerminalReason, Is.EqualTo(ModelLoopCodes.ConsecutiveFailuresExceeded));
            Assert.That(failed.Session.Requests, Has.Count.EqualTo(2));
            Assert.That(cancellation.Outcome, Is.EqualTo(RunOutcome.Cancelled));
            Assert.That(cancelled.Session.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task ScriptedClientSupportsDelayProviderExceptionAndExplicitCancellation()
    {
        var delay = new CapturingDelay();
        var client = new ScriptedLlmClient([
            new ScriptedModelStep.Response(FinishResponse(), TimeSpan.FromSeconds(3)),
            new ScriptedModelStep.ProviderFault("provider"),
            new ScriptedModelStep.Cancel()], delay);
        var request = new ModelRequest(BotRunId.New(), "input", []);
        _ = await client.GetNextResponseAsync(request, default);
        Assert.ThrowsAsync<InvalidOperationException>(() => client.GetNextResponseAsync(request, default));
        Assert.ThrowsAsync<OperationCanceledException>(() => client.GetNextResponseAsync(request, default));
        Assert.That(delay.Delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(3) }));
    }

    [Test]
    public void ScriptedClientRejectsAnUnexpectedRequestInOrder()
    {
        var expectedId = BotRunId.New();
        var expectation = new ScriptedRequestExpectation(expectedId, "expected", [(StageThreeTools.Finish, 1)]);
        var client = new ScriptedLlmClient([new ScriptedModelStep.Response(FinishResponse(), Expectation: expectation)], new CapturingDelay());
        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => client.GetNextResponseAsync(new ModelRequest(expectedId, "wrong", StageThreeToolDispatcher.Definitions), default));
        Assert.That(exception!.Message, Does.Contain("did not match"));
    }

    private static AssistantResponse FinishResponse(long input = 0, long output = 0, decimal cost = 0) => new("done",
        [new ModelToolCall(ToolInvocationId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FC0"), StageThreeTools.Finish, 1,
            "{\"status\":\"Completed\",\"summary\":\"No action.\"}")], new ModelUsage(input, output, cost), null);

    private sealed class Fixture
    {
        private Fixture(BotRun run, ScriptedLlmClient session, BoundedModelLoop loop, DeterministicBotRunInput input)
        { Run = run; Session = session; Loop = loop; Input = input; }
        public BotRun Run { get; }
        public ScriptedLlmClient Session { get; private set; }
        public BoundedModelLoop Loop { get; }
        private DeterministicBotRunInput Input { get; }
        public static Fixture Create(RunBudget? budget = null, Usage? initialUsage = null, DateTimeOffset? clockNow = null, ModelLoopLimits? limits = null)
        {
            var botId = TradingBotId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAV"); var configId = TradingBotConfigurationVersionId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAX");
            var portfolioId = PortfolioId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAZ"); var snapshotId = PortfolioDecisionSnapshotId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FB0");
            var actualBudget = budget ?? new RunBudget(TimeSpan.FromMinutes(5), 100, new Money(1, Currency.USD), 3, 0, 0);
            var bot = new TradingBot(botId, "bot", Now.AddDays(-1));
            var configuration = bot.AddConfiguration(configId, new InvestmentMandate("test", TimeSpan.FromDays(1), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])),
                new RiskPolicy([]), new ToolPolicy([new ToolAllowance(StageThreeTools.GetPortfolioSnapshot, 2), new ToolAllowance(StageThreeTools.Finish, 1)]), actualBudget,
                new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1), TimeSpan.FromDays(2)), ExecutionMode.ResearchOnly,
                new ModelConfiguration("scripted", "v1", 0, 100), "p1", Now.AddDays(-1));
            var portfolio = new Portfolio(portfolioId, "portfolio", Currency.USD, new Money(100, Currency.USD), 0, Now.AddDays(-1)); portfolio.AssignTradingBot(botId);
            var snapshot = new PortfolioDecisionSnapshot(snapshotId, portfolioId, botId, configId, Now, ReconciliationStatus.Reconciled,
                new Money(1, Currency.USD), new Money(2, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [], new DataFreshness(Now, Now, TimeSpan.FromMinutes(1)), Now);
            var run = new BotRun(BotRunId.Parse("01ARZ3NDEKTSV4RRFFQ69G5FB1"), botId, configId, snapshotId, initialUsage ?? new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0));
            run.BeginLeaseAcquisition(Now.AddSeconds(-1)); run.LeaseAcquired("host", Now.AddMinutes(10)); run.BeginReasoning();
            var repository = new FakeRunRepository(run); var clock = new FakeClock(clockNow ?? Now);
            var dispatcher = new StageThreeToolDispatcher(repository, new FakeBotRepository(bot), new FakeInput(snapshot), clock);
            var session = new ScriptedLlmClient([], new CapturingDelay());
            var input = new DeterministicBotRunInput("1", "input", new string('a', 64), run, bot, configuration, portfolio, snapshot);
            return new Fixture(run, session, new BoundedModelLoop(repository, dispatcher, clock, limits), input);
        }
        public Task<RunResult> Execute(params AssistantResponse[] responses) => ReplaceAndExecute(responses, default);
        public Task<RunResult> ExecuteWithCancellation(AssistantResponse[] responses, CancellationToken token) => ReplaceAndExecute(responses, token);
        private Task<RunResult> ReplaceAndExecute(AssistantResponse[] responses, CancellationToken token)
        {
            var steps = responses.Select(x => (ScriptedModelStep)new ScriptedModelStep.Response(x));
            Session = new ScriptedLlmClient(steps, new CapturingDelay());
            return Loop.ExecuteAsync(Input, Session, token);
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : IUtcClock { public DateTimeOffset UtcNow => now; }
    private sealed class CapturingDelay : IAsyncDelay { public List<TimeSpan> Delays { get; } = []; public Task DelayAsync(TimeSpan duration, CancellationToken token) { token.ThrowIfCancellationRequested(); Delays.Add(duration); return Task.CompletedTask; } }
    private sealed class FakeRunRepository(BotRun run) : IBotRunRepository
    {
        public Task<BotRun?> GetAsync(BotRunId id, CancellationToken token) => Task.FromResult<BotRun?>(id == run.Id ? run : null);
        public Task<PersistenceWriteResult> SaveAsync(BotRun value, long version, CancellationToken token) => Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded());
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
        public Task<PinnedPortfolioSnapshot> GetPortfolioSnapshotAsync(BotRunId id, CancellationToken token) => Task.FromResult(new PinnedPortfolioSnapshot(snapshot.CanonicalContent, snapshot.ContentHash, snapshot.SnapshotSchemaVersion, snapshot));
        public Task<DeterministicBotRunInput> PrepareAsync(BotRunId id, CancellationToken token) => throw new NotSupportedException();
    }
}
