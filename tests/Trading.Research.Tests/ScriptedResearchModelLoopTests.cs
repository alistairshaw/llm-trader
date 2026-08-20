using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Research;
using Trading.Research.Contracts;

namespace Trading.Research.Tests;

[Category("ModelLoop")]
[Category("Budgets")]
public sealed class ScriptedResearchModelLoopTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ValidDraftThenFinishProducesOnlyPublicationCandidateAndPinsEveryRequest()
    {
        var h = new Harness();
        var expected = new ResearchModelExpectation("research safely", h.Attempt.Versions);
        var session = h.Session(
            new ScriptedResearchModelStep.Response(Response(Call(StageFourResearchTools.PublishReportDraft)), Expected: expected),
            new ScriptedResearchModelStep.Response(Response(Call(StageFourResearchTools.FinishResearch)), Expected: expected));
        var result = await h.Run(session);
        Assert.Multiple(() =>
        {
            Assert.That(result.HasPublicationCandidate, Is.True);
            Assert.That(result.ResultCode, Is.EqualTo(ResearchResultCodes.Success));
            Assert.That(h.Attempt.Status, Is.EqualTo(ResearchRunAttemptStatus.Completed));
            Assert.That(result.Transcript, Has.Count.EqualTo(4));
            Assert.That(h.Dispatcher.Calls, Is.EqualTo(new[] { StageFourResearchTools.PublishReportDraft, StageFourResearchTools.FinishResearch }));
        });
    }

    [TestCase("tokens", ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded)]
    [TestCase("cost", ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded)]
    [TestCase("tools", ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded)]
    [TestCase("documents", ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded)]
    [TestCase("bytes", ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded)]
    [TestCase("wall", ResearchRunAttemptStatus.TimedOut, ResearchResultCodes.TimedOut)]
    public async Task EveryResourceBoundaryTerminatesWithoutCandidate(string boundary, ResearchRunAttemptStatus status, string code)
    {
        var h = new Harness(boundary);
        var result = await h.Run(h.Session(new ScriptedResearchModelStep.Response(Response(Call(StageFourResearchTools.SearchWeb)),
            boundary == "wall" ? TimeSpan.FromMinutes(2) : default)));
        Assert.Multiple(() => { Assert.That(result.HasPublicationCandidate, Is.False); Assert.That(result.ResultCode, Is.EqualTo(code)); Assert.That(h.Attempt.Status, Is.EqualTo(status)); });
    }

    [TestCase("empty", ResearchResultCodes.MissingDraft)]
    [TestCase("draft-only", ResearchResultCodes.MissingFinish)]
    [TestCase("finish-only", ResearchResultCodes.MissingDraft)]
    [TestCase("provider", ResearchResultCodes.ProviderFailed)]
    [TestCase("malformed", ResearchResultCodes.MalformedModelResponse)]
    [TestCase("cancel", ResearchResultCodes.Cancelled)]
    public async Task IncompleteMalformedProviderAndCancellationOutcomesAreStable(string scenario, string expected)
    {
        var h = new Harness();
        IResearchModelSession session = scenario switch
        {
            "empty" => h.Session(new ScriptedResearchModelStep.Response(new(null, [], 1, 0))),
            "draft-only" => h.Session(new ScriptedResearchModelStep.Response(Response(Call(StageFourResearchTools.PublishReportDraft)))),
            "finish-only" => h.Session(new ScriptedResearchModelStep.Response(Response(Call(StageFourResearchTools.FinishResearch)))),
            "provider" => h.Session(new ScriptedResearchModelStep.ProviderFault()),
            "malformed" => h.Session(new ScriptedResearchModelStep.Response(new(null, [], -1, 0))),
            _ => h.Session(new ScriptedResearchModelStep.Cancellation())
        };
        var result = await h.Run(session);
        Assert.Multiple(() => { Assert.That(result.HasPublicationCandidate, Is.False); Assert.That(result.ResultCode, Is.EqualTo(expected)); Assert.That(h.Store.Saved, Is.True); });
    }

    [Test]
    public async Task ConsecutiveFailuresAreBoundedAndSuccessResetsTheCounter()
    {
        var h = new Harness(failTools: true);
        var result = await h.Run(h.Session(
            new ScriptedResearchModelStep.Response(Response(Call("bad-one"))),
            new ScriptedResearchModelStep.Response(Response(Call("bad-two")))));
        Assert.Multiple(() => { Assert.That(result.ResultCode, Is.EqualTo(ResearchResultCodes.ConsecutiveFailuresExceeded)); Assert.That(result.Usage.ConsecutiveFailures, Is.EqualTo(2)); });
    }

    [Test]
    public async Task MaterialToolRetriesAreRejectedBeforeDispatchAndCannotDuplicateEffects()
    {
        var h = new Harness();
        var result = await h.Run(h.Session(
            new ScriptedResearchModelStep.Response(Response(Call(StageFourResearchTools.PublishReportDraft))),
            new ScriptedResearchModelStep.Response(Response(Call(StageFourResearchTools.PublishReportDraft)))));
        Assert.Multiple(() => { Assert.That(result.ResultCode, Is.EqualTo(ResearchResultCodes.MalformedModelResponse)); Assert.That(h.Dispatcher.Calls.Count(x => x == StageFourResearchTools.PublishReportDraft), Is.EqualTo(1)); });
    }

    [Test]
    public async Task PreCancelledGlobalCapacityTokenStopsBeforeModelOrToolWork()
    {
        var h = new Harness(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var session = h.Session(new ScriptedResearchModelStep.Response(Response(Call(StageFourResearchTools.SearchWeb))));
        var result = await h.Loop.ExecuteAsync(h.Attempt, new("bot-a", ResearchPrincipalKind.TradingBot),
            "research safely", 1, session, cancellation.Token);
        Assert.Multiple(() => { Assert.That(result.ResultCode, Is.EqualTo(ResearchResultCodes.Cancelled)); Assert.That(h.Dispatcher.Calls, Is.Empty); });
    }

    [Test]
    public async Task TranscriptAndIterationAreBoundedAndFailureDetailIsNotRetained()
    {
        var h = new Harness(limits: new(1, 64, 24));
        var result = await h.Run(h.Session(new ScriptedResearchModelStep.Response(new("secret diagnostic detail", [Call(StageFourResearchTools.SearchWeb)], 1, 0))));
        Assert.Multiple(() => { Assert.That(result.HasPublicationCandidate, Is.False); Assert.That(result.ResultCode, Is.EqualTo(ResearchResultCodes.BudgetExceeded)); Assert.That(result.Transcript, Is.Empty); });
    }

    private static ResearchAssistantResponse Response(params ResearchToolCall[] calls) => new(null, calls, 1, 0.01m);
    private static ResearchToolCall Call(string name) => new(Guid.NewGuid().ToString("N"), name, 1, "{}");

    private sealed class Harness
    {
        public Harness(string? boundary = null, bool failTools = false, ResearchLoopLimits? limits = null)
        {
            Clock = new(); Dispatcher = new(failTools, boundary); Store = new();
            var budget = new ResearchBudget(TimeSpan.FromMinutes(1), boundary == "tokens" ? 1 : 100,
                new Money(boundary == "cost" ? 0.01m : 10m, Currency.USD), boundary == "tools" ? 1 : 20,
                boundary == "documents" ? 1 : 20, boundary == "bytes" ? 10 : 10_000, 2);
            Attempt = new(ResearchRunAttemptId.New(), ResearchRequestId.New(), new("scripted", "research", "1", "prompt-v1", "tools-v1", "report-v1"), budget, Now);
            Attempt.Start(Now); Loop = new(Dispatcher, Store, Clock, limits);
        }
        public Clock Clock { get; }
        public Dispatcher Dispatcher { get; }
        public Store Store { get; }
        public ResearchRunAttempt Attempt { get; }
        public BoundedResearchModelLoop Loop { get; }
        public ScriptedResearchModelSession Session(params ScriptedResearchModelStep[] steps) => new(steps, Clock);
        public Task<ResearchLoopResult> Run(IResearchModelSession session) => Loop.ExecuteAsync(Attempt,
            new("bot-a", ResearchPrincipalKind.TradingBot), "research safely", 1, session, default);
    }
    private sealed class Clock : IResearchClock, IResearchDelay
    {
        public DateTimeOffset UtcNow { get; private set; } = Now;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); UtcNow += delay; return Task.CompletedTask; }
    }
    private sealed class Dispatcher(bool failTools, string? boundary) : IResearchToolDispatcher
    {
        public List<string> Calls { get; } = [];
        public IReadOnlyList<ResearchToolDefinition> Definitions { get; } = StageFourResearchTools.Names.Select(x => new ResearchToolDefinition(x, 1, "{}")).ToArray();
        public Task<ResearchToolResult> DispatchAsync(ResearchRunAttempt attempt, ResearchPrincipal principal, ResearchToolCall call, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls.Add(call.Name);
            var failed = failTools || call.Name.StartsWith("bad", StringComparison.Ordinal);
            var usage = new ResearchUsage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 1, boundary == "documents" ? 1 : 0, boundary == "bytes" ? 10 : 0, 0);
            return Task.FromResult(new ResearchToolResult(call.CallId, !failed, failed ? ResearchResultCodes.SourceProviderFailed : ResearchResultCodes.Success, "{}", usage));
        }
    }
    private sealed class Store : IResearchRunAttemptRepository
    {
        public bool Saved { get; private set; }
        public Task<PersistenceWriteResult> SaveAsync(ResearchRunAttempt attempt, long expectedVersion, CancellationToken token) { Saved = true; return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
        public Task<ResearchRunAttempt?> GetAsync(ResearchRunAttemptId id, CancellationToken token) => Task.FromResult<ResearchRunAttempt?>(null);
        public Task<PersistenceWriteResult> AppendToolAuditAsync(ResearchToolAudit audit, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<ResearchToolAudit>> GetToolAuditAsync(ResearchRunAttemptId id, CancellationToken token) => Task.FromResult<IReadOnlyList<ResearchToolAudit>>([]);
    }
}
