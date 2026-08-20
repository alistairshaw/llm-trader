using System.Text;
using System.Text.Json;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;

namespace Trading.Engine.Runtime;

public static class ModelLoopCodes
{
    public const string Completed = "completed";
    public const string WallClockExceeded = "wall_clock_exceeded";
    public const string TokenBudgetExceeded = "token_budget_exceeded";
    public const string CostBudgetExceeded = "cost_budget_exceeded";
    public const string ToolBudgetExceeded = "tool_budget_exceeded";
    public const string ResearchBudgetExceeded = "research_budget_exceeded";
    public const string ProposalBudgetExceeded = "proposal_budget_exceeded";
    public const string IterationBudgetExceeded = "iteration_budget_exceeded";
    public const string ConsecutiveFailuresExceeded = "consecutive_failures_exceeded";
    public const string MissingFinish = "missing_finish";
    public const string MalformedResponse = "malformed_response";
    public const string ProviderFailure = "provider_failure";
    public const string Cancelled = "cancelled";
    public const string PersistenceConflict = "persistence_conflict";
}

public sealed record ModelLoopLimits(int IterationLimit = 32, int ConsecutiveFailureLimit = 3)
{
    public int IterationLimit { get; } = IterationLimit > 0 ? IterationLimit : throw new ArgumentOutOfRangeException(nameof(IterationLimit));
    public int ConsecutiveFailureLimit { get; } = ConsecutiveFailureLimit > 0 ? ConsecutiveFailureLimit : throw new ArgumentOutOfRangeException(nameof(ConsecutiveFailureLimit));
}

public sealed record ScriptedRequestExpectation(BotRunId RunId, string Instructions, IReadOnlyList<(string Name, int SchemaVersion)> Tools);

public abstract record ScriptedModelStep(TimeSpan Delay, ScriptedRequestExpectation? ExpectedRequest)
{
    public sealed record Response(AssistantResponse Value, TimeSpan ResponseDelay = default, ScriptedRequestExpectation? Expectation = null) : ScriptedModelStep(ResponseDelay, Expectation);
    public sealed record ProviderFault(string Message, TimeSpan ResponseDelay = default, ScriptedRequestExpectation? Expectation = null) : ScriptedModelStep(ResponseDelay, Expectation);
    public sealed record Cancel(TimeSpan ResponseDelay = default, ScriptedRequestExpectation? Expectation = null) : ScriptedModelStep(ResponseDelay, Expectation);
}

public sealed class ScriptedLlmClient(IEnumerable<ScriptedModelStep> steps, IAsyncDelay delay) : IModelSession
{
    private readonly Queue<ScriptedModelStep> script = new(steps ?? throw new ArgumentNullException(nameof(steps)));
    private readonly List<ModelRequest> requests = [];
    private readonly List<ModelToolResult> toolResults = [];
    public IReadOnlyList<ModelRequest> Requests => requests.AsReadOnly();
    public IReadOnlyList<ModelToolResult> ToolResults => toolResults.AsReadOnly();
    public int RemainingSteps => script.Count;

    public async Task<AssistantResponse> GetNextResponseAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request); requests.Add(request);
        if (script.Count == 0)
            return new AssistantResponse(null, [], new ModelUsage(0, 0, 0),
                new ModelFailure(ModelFailureKind.MalformedResponse, ModelLoopCodes.MissingFinish, false));
        var step = script.Dequeue();
        if (step.ExpectedRequest is { } expected && (request.RunId != expected.RunId ||
            !string.Equals(request.Instructions, expected.Instructions, StringComparison.Ordinal) ||
            !request.Tools.Select(x => (x.Name, x.SchemaVersion)).SequenceEqual(expected.Tools)))
            throw new InvalidOperationException("The scripted model request did not match the next expectation.");
        if (step.Delay > TimeSpan.Zero) await delay.DelayAsync(step.Delay, cancellationToken).ConfigureAwait(false);
        return step switch
        {
            ScriptedModelStep.Response response => response.Value,
            ScriptedModelStep.ProviderFault failure => throw new InvalidOperationException(failure.Message),
            ScriptedModelStep.Cancel => throw new OperationCanceledException(cancellationToken),
            _ => throw new InvalidOperationException("Unknown scripted model step."),
        };
    }

    public Task SubmitToolResultAsync(ModelToolResult result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        toolResults.Add(result ?? throw new ArgumentNullException(nameof(result)));
        return Task.CompletedTask;
    }
}

public sealed class BoundedModelLoop(
    IBotRunRepository runs,
    IToolDispatcher tools,
    IUtcClock clock,
    ModelLoopLimits? limits = null) : IModelLoop
{
    public const int TranscriptSchemaVersion = 1;
    private readonly ModelLoopLimits limits = limits ?? new ModelLoopLimits();

    public async Task<RunResult> ExecuteAsync(DeterministicBotRunInput input, IModelSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(session);
        var entries = new List<TranscriptEntry>();
        var run = await Load(input.Run.Id, CancellationToken.None).ConfigureAwait(false);
        var consecutiveFailures = 0;
        for (var iteration = 0; iteration < limits.IterationLimit; iteration++)
        {
            var preflight = BudgetFailure(run, input.Configuration.RunBudget, clock.UtcNow);
            if (preflight is not null) return await Terminate(run, entries, preflight.Value.Outcome, preflight.Value.Code).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                return await Terminate(run, entries, RunOutcome.Cancelled, ModelLoopCodes.Cancelled).ConfigureAwait(false);

            AssistantResponse response;
            try
            {
                response = await session.GetNextResponseAsync(
                    new ModelRequest(run.Id, input.Content, tools.Definitions), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return await Terminate(run, entries, RunOutcome.Cancelled, ModelLoopCodes.Cancelled).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return await Terminate(run, entries, RunOutcome.Faulted, ModelLoopCodes.ProviderFailure).ConfigureAwait(false);
            }

            Usage usage;
            try { usage = AddModelUsage(run, response.Usage); }
            catch (Exception exception) when (exception is InvalidOperationException or OverflowException or ArgumentException)
            { return await Terminate(run, entries, RunOutcome.Faulted, ModelLoopCodes.MalformedResponse).ConfigureAwait(false); }
            entries.Add(TranscriptEntry.FromAssistant(iteration + 1, response));
            run.RecordModelProgress(TranscriptSchemaVersion, Transcript(entries), usage);
            if (!await Save(run, cancellationToken).ConfigureAwait(false))
                return await TerminateWithoutSave(run, RunOutcome.Faulted, ModelLoopCodes.PersistenceConflict).ConfigureAwait(false);
            run = await Load(run.Id, CancellationToken.None).ConfigureAwait(false);

            var postModel = BudgetFailure(run, input.Configuration.RunBudget, clock.UtcNow);
            if (postModel is not null) return await Terminate(run, entries, postModel.Value.Outcome, postModel.Value.Code).ConfigureAwait(false);
            if (response.Failure is not null)
            {
                var code = response.Failure.Kind switch
                {
                    ModelFailureKind.Timeout => ModelLoopCodes.WallClockExceeded,
                    ModelFailureKind.MalformedResponse when response.Failure.Message == ModelLoopCodes.MissingFinish => ModelLoopCodes.MissingFinish,
                    ModelFailureKind.MalformedResponse => ModelLoopCodes.MalformedResponse,
                    ModelFailureKind.Cancellation => ModelLoopCodes.Cancelled,
                    _ => ModelLoopCodes.ProviderFailure,
                };
                var outcome = response.Failure.Kind switch
                {
                    ModelFailureKind.Timeout => RunOutcome.TimedOut,
                    ModelFailureKind.Cancellation => RunOutcome.Cancelled,
                    _ => RunOutcome.Faulted,
                };
                return await Terminate(run, entries, outcome, code).ConfigureAwait(false);
            }
            if (response.ToolCalls.Count == 0)
            {
                consecutiveFailures++;
                if (consecutiveFailures >= limits.ConsecutiveFailureLimit)
                    return await Terminate(run, entries, RunOutcome.Faulted, ModelLoopCodes.ConsecutiveFailuresExceeded).ConfigureAwait(false);
                continue;
            }

            consecutiveFailures = 0;
            foreach (var call in response.ToolCalls)
            {
                var beforeTool = BudgetFailure(run, input.Configuration.RunBudget, clock.UtcNow);
                if (beforeTool is not null) return await Terminate(run, entries, beforeTool.Value.Outcome, beforeTool.Value.Code).ConfigureAwait(false);
                run.WaitForTool();
                if (!await Save(run, cancellationToken).ConfigureAwait(false))
                    return await TerminateWithoutSave(run, RunOutcome.Faulted, ModelLoopCodes.PersistenceConflict).ConfigureAwait(false);
                var dispatched = await tools.DispatchAsync(new ToolDispatchContext(run.Id, run.TradingBotId, run.PortfolioSnapshotId), call, cancellationToken).ConfigureAwait(false);
                entries.Add(TranscriptEntry.FromTool(iteration + 1, dispatched.Result));
                try { await session.SubmitToolResultAsync(dispatched.Result, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { run = await Load(run.Id, CancellationToken.None).ConfigureAwait(false); return await Terminate(run, entries, RunOutcome.Cancelled, ModelLoopCodes.Cancelled).ConfigureAwait(false); }
                catch (Exception) { run = await Load(run.Id, CancellationToken.None).ConfigureAwait(false); return await Terminate(run, entries, RunOutcome.Faulted, ModelLoopCodes.ProviderFailure).ConfigureAwait(false); }
                run = await Load(run.Id, CancellationToken.None).ConfigureAwait(false);
                if (run.IsTerminal)
                {
                    run.RecordModelProgress(TranscriptSchemaVersion, Transcript(entries), run.Usage);
                    if (!await Save(run, CancellationToken.None).ConfigureAwait(false))
                        return await TerminateWithoutSave(run, RunOutcome.Faulted, ModelLoopCodes.PersistenceConflict).ConfigureAwait(false);
                    return Result(run, RunOutcome.Completed);
                }
                run.ResumeReasoning();
                run.RecordModelProgress(TranscriptSchemaVersion, Transcript(entries), run.Usage);
                if (!await Save(run, cancellationToken).ConfigureAwait(false))
                    return await TerminateWithoutSave(run, RunOutcome.Faulted, ModelLoopCodes.PersistenceConflict).ConfigureAwait(false);
                run = await Load(run.Id, CancellationToken.None).ConfigureAwait(false);
                if (dispatched.Result.Outcome != ToolExecutionOutcome.Succeeded)
                {
                    consecutiveFailures++;
                    if (consecutiveFailures >= limits.ConsecutiveFailureLimit)
                        return await Terminate(run, entries, RunOutcome.Faulted, ModelLoopCodes.ConsecutiveFailuresExceeded).ConfigureAwait(false);
                }
                else consecutiveFailures = 0;
            }
        }
        return await Terminate(run, entries, RunOutcome.BudgetExceeded, ModelLoopCodes.IterationBudgetExceeded).ConfigureAwait(false);
    }

    private static Usage AddModelUsage(BotRun run, ModelUsage model)
    {
        if (model.InputTokens < 0 || model.OutputTokens < 0 || model.Cost < 0) throw new InvalidOperationException(ModelLoopCodes.MalformedResponse);
        checked
        {
            return new Usage(run.Usage.Elapsed, run.Usage.Tokens + model.InputTokens + model.OutputTokens,
                new Money(run.Usage.Cost.Amount + model.Cost, run.Usage.Cost.Currency), run.Usage.ToolCalls,
                run.Usage.ResearchRequests, run.Usage.Proposals);
        }
    }

    private static (RunOutcome Outcome, string Code)? BudgetFailure(BotRun run, RunBudget budget, DateTimeOffset now)
    {
        var elapsed = run.StartedAt is null ? run.Usage.Elapsed : now - run.StartedAt.Value;
        if (elapsed >= budget.WallClock) return (RunOutcome.TimedOut, ModelLoopCodes.WallClockExceeded);
        if (run.Usage.Tokens >= budget.TokenLimit) return (RunOutcome.BudgetExceeded, ModelLoopCodes.TokenBudgetExceeded);
        if (run.Usage.Cost.Amount >= budget.CostLimit.Amount) return (RunOutcome.BudgetExceeded, ModelLoopCodes.CostBudgetExceeded);
        if (run.Usage.ToolCalls >= budget.ToolCallLimit) return (RunOutcome.BudgetExceeded, ModelLoopCodes.ToolBudgetExceeded);
        if (run.Usage.ResearchRequests > 0 || run.Usage.ResearchRequests >= budget.ResearchRequestLimit && budget.ResearchRequestLimit > 0)
            return (RunOutcome.BudgetExceeded, ModelLoopCodes.ResearchBudgetExceeded);
        if (run.Usage.Proposals > 0 || run.Usage.Proposals >= budget.ProposalLimit && budget.ProposalLimit > 0)
            return (RunOutcome.BudgetExceeded, ModelLoopCodes.ProposalBudgetExceeded);
        return null;
    }

    private async Task<RunResult> Terminate(BotRun run, List<TranscriptEntry> entries, RunOutcome outcome, string code)
    {
        if (run.IsTerminal) return Result(run, outcome);
        var elapsed = run.StartedAt is null ? run.Usage.Elapsed : Max(run.Usage.Elapsed, clock.UtcNow - run.StartedAt.Value);
        var usage = new Usage(elapsed, run.Usage.Tokens, run.Usage.Cost, run.Usage.ToolCalls, run.Usage.ResearchRequests, run.Usage.Proposals);
        run.RecordModelProgress(TranscriptSchemaVersion, Transcript(entries), usage);
        run.RecordTerminalReason(code);
        switch (outcome)
        {
            case RunOutcome.TimedOut: run.TimeOut(usage, clock.UtcNow); break;
            case RunOutcome.BudgetExceeded: run.ExceedBudget(usage, clock.UtcNow); break;
            case RunOutcome.Cancelled: run.Cancel(usage, clock.UtcNow); break;
            default: run.Fault(usage, clock.UtcNow); break;
        }
        if (!await Save(run, CancellationToken.None).ConfigureAwait(false)) return Result(run, RunOutcome.Faulted);
        return Result(run, outcome);
    }

    private static Task<RunResult> TerminateWithoutSave(BotRun run, RunOutcome outcome, string code) =>
        Task.FromResult(new RunResult(run.Id, outcome, run.Usage, code));
    private static RunResult Result(BotRun run, RunOutcome outcome) => new(run.Id, outcome, run.Usage, run.FinishResult?.Summary ?? run.TerminalReason);
    private async Task<BotRun> Load(BotRunId id, CancellationToken token) => await runs.GetAsync(id, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Bot Run not found.");
    private async Task<bool> Save(BotRun run, CancellationToken token) => await runs.SaveAsync(run, run.Version, token).ConfigureAwait(false) is PersistenceWriteResult.Succeeded;
    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;

    private static string Transcript(IEnumerable<TranscriptEntry> entries)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WritePropertyName("entries"); writer.WriteStartArray();
            foreach (var entry in entries) entry.Write(writer);
            writer.WriteEndArray(); writer.WriteNumber("schemaVersion", TranscriptSchemaVersion); writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed record TranscriptEntry(int Iteration, string Kind, AssistantResponse? Assistant, ModelToolResult? ToolResult)
    {
        public static TranscriptEntry FromAssistant(int iteration, AssistantResponse response) => new(iteration, "assistant", response, null);
        public static TranscriptEntry FromTool(int iteration, ModelToolResult result) => new(iteration, "tool", null, result);
        public void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject(); writer.WriteNumber("iteration", Iteration); writer.WriteString("kind", Kind);
            if (Assistant is not null)
            {
                if (Assistant.Content is null) writer.WriteNull("content"); else writer.WriteString("content", Assistant.Content);
                if (Assistant.Failure is null) writer.WriteNull("failure"); else { writer.WritePropertyName("failure"); writer.WriteStartObject(); writer.WriteString("kind", Assistant.Failure.Kind.ToString()); writer.WriteString("message", Assistant.Failure.Message); writer.WriteBoolean("retryable", Assistant.Failure.IsRetryable); writer.WriteEndObject(); }
                writer.WritePropertyName("toolCalls"); writer.WriteStartArray();
                foreach (var call in Assistant.ToolCalls) { writer.WriteStartObject(); writer.WriteString("arguments", call.CanonicalArguments); writer.WriteString("id", call.InvocationId.ToString()); writer.WriteString("name", call.Name); writer.WriteNumber("schemaVersion", call.SchemaVersion); writer.WriteEndObject(); }
                writer.WriteEndArray();
                writer.WritePropertyName("usage"); writer.WriteStartObject(); writer.WriteString("cost", Assistant.Usage.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture)); writer.WriteNumber("inputTokens", Assistant.Usage.InputTokens); writer.WriteNumber("outputTokens", Assistant.Usage.OutputTokens); writer.WriteEndObject();
            }
            else if (ToolResult is not null)
            { writer.WriteString("id", ToolResult.InvocationId.ToString()); writer.WriteString("name", ToolResult.Name); writer.WriteString("outcome", ToolResult.Outcome.ToString()); writer.WriteString("result", ToolResult.CanonicalResult); writer.WriteNumber("schemaVersion", ToolResult.SchemaVersion); }
            writer.WriteEndObject();
        }
    }
}
