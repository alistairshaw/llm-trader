using System.Text;
using System.Text.Json;
using Trading.Core.FinancialValues;
using Trading.Core.Persistence;
using Trading.Core.Research;
using Trading.Research.Contracts;

namespace Trading.Research;

public sealed record ResearchModelExpectation(string Instructions, ResearchVersionPins Versions);

public abstract record ScriptedResearchModelStep(TimeSpan Delay, ResearchModelExpectation? Expectation)
{
    public sealed record Response(ResearchAssistantResponse Value, TimeSpan ResponseDelay = default,
        ResearchModelExpectation? Expected = null) : ScriptedResearchModelStep(ResponseDelay, Expected);
    public sealed record ProviderFault(TimeSpan ResponseDelay = default,
        ResearchModelExpectation? Expected = null) : ScriptedResearchModelStep(ResponseDelay, Expected);
    public sealed record Cancellation(TimeSpan ResponseDelay = default,
        ResearchModelExpectation? Expected = null) : ScriptedResearchModelStep(ResponseDelay, Expected);
}

public sealed class ScriptedResearchModelSession(IEnumerable<ScriptedResearchModelStep> steps, IResearchDelay delay) : IResearchModelSession
{
    private readonly Queue<ScriptedResearchModelStep> script = new(steps ?? throw new ArgumentNullException(nameof(steps)));

    public async Task<ResearchAssistantResponse> CompleteAsync(ResearchModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (script.Count == 0) throw new InvalidDataException(ResearchResultCodes.MissingFinish);
        var step = script.Dequeue();
        if (step.Expectation is { } expected &&
            (expected.Instructions != request.Instructions || expected.Versions != request.Versions))
            throw new InvalidDataException(ResearchResultCodes.MalformedModelResponse);
        await delay.DelayAsync(step.Delay, cancellationToken).ConfigureAwait(false);
        return step switch
        {
            ScriptedResearchModelStep.Response response => response.Value,
            ScriptedResearchModelStep.ProviderFault => throw new InvalidOperationException(ResearchResultCodes.ProviderFailed),
            ScriptedResearchModelStep.Cancellation => throw new OperationCanceledException(cancellationToken),
            _ => throw new InvalidDataException(ResearchResultCodes.MalformedModelResponse)
        };
    }
}

public sealed record ResearchLoopLimits
{
    public ResearchLoopLimits(int iterationLimit = 32, int maximumNarrativeBytes = 65_536, int maximumTranscriptBytes = 262_144)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterationLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumNarrativeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTranscriptBytes);
        IterationLimit = iterationLimit; MaximumNarrativeBytes = maximumNarrativeBytes; MaximumTranscriptBytes = maximumTranscriptBytes;
    }
    public int IterationLimit { get; }
    public int MaximumNarrativeBytes { get; }
    public int MaximumTranscriptBytes { get; }
}

public sealed record ResearchModelAudit(int Sequence, string Kind, string CanonicalContent);
public sealed record ResearchLoopResult(bool HasPublicationCandidate, string ResultCode, ResearchUsage Usage,
    IReadOnlyList<ResearchModelAudit> Transcript);

public sealed class BoundedResearchModelLoop(IResearchToolDispatcher dispatcher, IResearchRunAttemptRepository attempts,
    IResearchClock clock, ResearchLoopLimits? configuredLimits = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ResearchLoopLimits limits = configuredLimits ?? new();

    public async Task<ResearchLoopResult> ExecuteAsync(ResearchRunAttempt attempt, ResearchPrincipal principal,
        string instructions, long expectedVersion, IResearchModelSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt); ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(session);
        if (attempt.Status != ResearchRunAttemptStatus.Running) throw new InvalidOperationException("Research attempt must be running.");
        if (string.IsNullOrWhiteSpace(instructions)) throw new ArgumentException("Instructions are required.", nameof(instructions));
        var started = attempt.StartedAt!.Value;
        var usage = attempt.Usage ?? Zero();
        var transcript = new List<ResearchModelAudit>();
        var hasDraft = false;
        var hasFinish = false;

        for (var iteration = 1; iteration <= limits.IterationLimit; iteration++)
        {
            var boundary = Boundary(attempt.Budget, usage, clock.UtcNow - started);
            if (boundary is not null) return await Terminate(boundary.Value.Status, boundary.Value.Code).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) return await Terminate(ResearchRunAttemptStatus.Cancelled, ResearchResultCodes.Cancelled).ConfigureAwait(false);
            ResearchAssistantResponse response;
            try
            {
                response = await session.CompleteAsync(new(attempt.Id, instructions, attempt.Versions, dispatcher.Definitions), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return await Terminate(ResearchRunAttemptStatus.Cancelled, ResearchResultCodes.Cancelled).ConfigureAwait(false); }
            catch (InvalidDataException exception) when (exception.Message == ResearchResultCodes.MissingFinish)
            { return await Terminate(ResearchRunAttemptStatus.Failed, hasDraft ? ResearchResultCodes.MissingFinish : ResearchResultCodes.MissingDraft).ConfigureAwait(false); }
            catch (InvalidDataException) { return await Terminate(ResearchRunAttemptStatus.Failed, ResearchResultCodes.MalformedModelResponse).ConfigureAwait(false); }
            catch (Exception) { return await Terminate(ResearchRunAttemptStatus.Failed, ResearchResultCodes.ProviderFailed).ConfigureAwait(false); }

            if (!Valid(response)) return await Terminate(ResearchRunAttemptStatus.Failed, ResearchResultCodes.MalformedModelResponse).ConfigureAwait(false);
            usage = Add(usage, response.Tokens, response.Cost, default, clock.UtcNow - started);
            if (!AppendAudit("assistant", new { response.Narrative, response.Tokens, response.Cost, response.ToolCalls }))
                return await Terminate(ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded).ConfigureAwait(false);
            boundary = Boundary(attempt.Budget, usage, clock.UtcNow - started);
            if (boundary is not null) return await Terminate(boundary.Value.Status, boundary.Value.Code).ConfigureAwait(false);
            if (response.ToolCalls.Count == 0)
                return await Terminate(ResearchRunAttemptStatus.Failed, hasDraft ? ResearchResultCodes.MissingFinish : ResearchResultCodes.MissingDraft).ConfigureAwait(false);

            foreach (var call in response.ToolCalls)
            {
                if ((call.Name == StageFourResearchTools.PublishReportDraft && hasDraft) ||
                    (call.Name == StageFourResearchTools.FinishResearch && hasFinish))
                    return await Terminate(ResearchRunAttemptStatus.Failed, ResearchResultCodes.MalformedModelResponse).ConfigureAwait(false);
                boundary = Boundary(attempt.Budget, usage, clock.UtcNow - started);
                if (boundary is not null) return await Terminate(boundary.Value.Status, boundary.Value.Code).ConfigureAwait(false);
                ResearchToolResult result;
                try { result = await dispatcher.DispatchAsync(attempt, principal, call, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return await Terminate(ResearchRunAttemptStatus.Cancelled, ResearchResultCodes.Cancelled).ConfigureAwait(false); }
                catch (Exception) { return await Terminate(ResearchRunAttemptStatus.Failed, ResearchResultCodes.ProviderFailed).ConfigureAwait(false); }
                usage = Add(usage, 0, 0, result.Usage, clock.UtcNow - started);
                if (!AppendAudit("tool", new { call.CallId, call.Name, call.SchemaVersion, result.Succeeded, result.ResultCode, result.CanonicalPayload, result.Usage }))
                    return await Terminate(ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded).ConfigureAwait(false);
                usage = Copy(usage, usage.Elapsed, result.Succeeded ? 0 : usage.ConsecutiveFailures + 1);
                if (result.Succeeded && call.Name == StageFourResearchTools.PublishReportDraft) hasDraft = true;
                if (result.Succeeded && call.Name == StageFourResearchTools.FinishResearch) hasFinish = true;
                if (usage.ConsecutiveFailures >= attempt.Budget.ConsecutiveFailureLimit && !result.Succeeded)
                    return await Terminate(ResearchRunAttemptStatus.Failed, ResearchResultCodes.ConsecutiveFailuresExceeded).ConfigureAwait(false);
                if (hasFinish)
                    return await Terminate(hasDraft ? ResearchRunAttemptStatus.Completed : ResearchRunAttemptStatus.Failed,
                        hasDraft ? ResearchResultCodes.Success : ResearchResultCodes.MissingDraft).ConfigureAwait(false);
            }
        }
        return await Terminate(ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded).ConfigureAwait(false);

        bool AppendAudit(string kind, object value)
        {
            var canonical = Canonical(value);
            var projected = transcript.Sum(x => Encoding.UTF8.GetByteCount(x.CanonicalContent)) + Encoding.UTF8.GetByteCount(canonical);
            if (projected > limits.MaximumTranscriptBytes) return false;
            transcript.Add(new(transcript.Count + 1, kind, canonical)); return true;
        }
        async Task<ResearchLoopResult> Terminate(ResearchRunAttemptStatus status, string code)
        {
            usage = Copy(usage, NonNegative(clock.UtcNow - started), usage.ConsecutiveFailures);
            attempt.Terminate(status, usage, code, clock.UtcNow < started ? started : clock.UtcNow);
            var save = await attempts.SaveAsync(attempt, expectedVersion, CancellationToken.None).ConfigureAwait(false);
            if (save is not PersistenceWriteResult.Succeeded)
                return new(false, ResearchResultCodes.PersistenceConflict, usage, transcript);
            return new(status == ResearchRunAttemptStatus.Completed && hasDraft && hasFinish, code, usage, transcript);
        }
    }

    private bool Valid(ResearchAssistantResponse? response) => response is not null && response.ToolCalls is not null && response.Tokens >= 0 &&
        response.Cost >= 0 && (response.Narrative is null || Encoding.UTF8.GetByteCount(response.Narrative) <= limits.MaximumNarrativeBytes) &&
        response.ToolCalls.All(x => x is not null && !string.IsNullOrWhiteSpace(x.CallId) && !string.IsNullOrWhiteSpace(x.Name) && x.CanonicalArguments is not null);
    private static (ResearchRunAttemptStatus Status, string Code)? Boundary(ResearchBudget budget, ResearchUsage usage, TimeSpan elapsed)
    {
        if (elapsed >= budget.WallClock) return (ResearchRunAttemptStatus.TimedOut, ResearchResultCodes.TimedOut);
        if (usage.Tokens >= budget.TokenLimit || usage.Cost.Amount >= budget.CostLimit.Amount || usage.ToolCalls >= budget.ToolCallLimit ||
            usage.Documents >= budget.DocumentLimit || usage.RetainedBytes >= budget.RetainedByteLimit)
            return (ResearchRunAttemptStatus.BudgetExceeded, ResearchResultCodes.BudgetExceeded);
        return null;
    }
    private static ResearchUsage Add(ResearchUsage current, long tokens, decimal cost, ResearchUsage? tool, TimeSpan elapsed) => new(
        NonNegative(elapsed), checked(current.Tokens + tokens + (tool?.Tokens ?? 0)),
        new Money(current.Cost.Amount + cost + (tool?.Cost.Amount ?? 0), current.Cost.Currency),
        checked(current.ToolCalls + (tool?.ToolCalls ?? 0)), checked(current.Documents + (tool?.Documents ?? 0)),
        checked(current.RetainedBytes + (tool?.RetainedBytes ?? 0)), current.ConsecutiveFailures);
    private static ResearchUsage Zero() => new(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0, 0);
    private static ResearchUsage Copy(ResearchUsage value, TimeSpan elapsed, int consecutiveFailures) => new(elapsed,
        value.Tokens, value.Cost, value.ToolCalls, value.Documents, value.RetainedBytes, consecutiveFailures);
    private static TimeSpan NonNegative(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
    private static string Canonical(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) Write(writer, document.RootElement);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) { writer.WriteStartObject(); foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); Write(writer, property.Value); } writer.WriteEndObject(); }
        else if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
}
