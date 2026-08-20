using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Core.Research;

public enum ResearchRunAttemptStatus { Created, Running, WaitingForTool, Completed, Failed, TimedOut, BudgetExceeded, Cancelled, Recovered }

public sealed record ResearchVersionPins
{
    public ResearchVersionPins(string modelProvider, string modelId, string modelVersion, string promptVersion, string toolSetVersion, string reportSchemaVersion)
    {
        ModelProvider = ResearchValidation.Required(modelProvider, nameof(modelProvider), 200);
        ModelId = ResearchValidation.Required(modelId, nameof(modelId), 200);
        ModelVersion = ResearchValidation.Required(modelVersion, nameof(modelVersion), 200);
        PromptVersion = ResearchValidation.Required(promptVersion, nameof(promptVersion), 200);
        ToolSetVersion = ResearchValidation.Required(toolSetVersion, nameof(toolSetVersion), 200);
        ReportSchemaVersion = ResearchValidation.Required(reportSchemaVersion, nameof(reportSchemaVersion), 200);
    }
    public string ModelProvider { get; }
    public string ModelId { get; }
    public string ModelVersion { get; }
    public string PromptVersion { get; }
    public string ToolSetVersion { get; }
    public string ReportSchemaVersion { get; }
}

public sealed record ResearchBudget
{
    public ResearchBudget(TimeSpan wallClock, long tokenLimit, Money costLimit, int toolCallLimit, int documentLimit, long retainedByteLimit, int consecutiveFailureLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(wallClock, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(costLimit);
        if (tokenLimit < 0 || costLimit.Amount < 0 || toolCallLimit < 0 || documentLimit < 0 || retainedByteLimit < 0 || consecutiveFailureLimit < 0)
            throw new ArgumentOutOfRangeException(nameof(tokenLimit), "Research budget limits cannot be negative.");
        WallClock = wallClock; TokenLimit = tokenLimit; CostLimit = costLimit; ToolCallLimit = toolCallLimit;
        DocumentLimit = documentLimit; RetainedByteLimit = retainedByteLimit; ConsecutiveFailureLimit = consecutiveFailureLimit;
    }
    public TimeSpan WallClock { get; }
    public long TokenLimit { get; }
    public Money CostLimit { get; }
    public int ToolCallLimit { get; }
    public int DocumentLimit { get; }
    public long RetainedByteLimit { get; }
    public int ConsecutiveFailureLimit { get; }
}

public sealed record ResearchUsage
{
    public ResearchUsage(TimeSpan elapsed, long tokens, Money cost, int toolCalls, int documents, long retainedBytes, int consecutiveFailures)
    {
        ArgumentNullException.ThrowIfNull(cost);
        if (elapsed < TimeSpan.Zero || tokens < 0 || cost.Amount < 0 || toolCalls < 0 || documents < 0 || retainedBytes < 0 || consecutiveFailures < 0)
            throw new ArgumentOutOfRangeException(nameof(tokens), "Research usage cannot be negative.");
        Elapsed = elapsed; Tokens = tokens; Cost = cost; ToolCalls = toolCalls; Documents = documents;
        RetainedBytes = retainedBytes; ConsecutiveFailures = consecutiveFailures;
    }
    public TimeSpan Elapsed { get; }
    public long Tokens { get; }
    public Money Cost { get; }
    public int ToolCalls { get; }
    public int Documents { get; }
    public long RetainedBytes { get; }
    public int ConsecutiveFailures { get; }
}

public sealed class ResearchRunAttempt
{
    public ResearchRunAttempt(ResearchRunAttemptId id, ResearchRequestId requestId, ResearchVersionPins versions, ResearchBudget budget, DateTimeOffset createdAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)); RequestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
        Versions = versions ?? throw new ArgumentNullException(nameof(versions)); Budget = budget ?? throw new ArgumentNullException(nameof(budget));
        CreatedAt = ResearchValidation.Utc(createdAt, nameof(createdAt));
    }
    public ResearchRunAttemptId Id { get; }
    public ResearchRequestId RequestId { get; }
    public ResearchVersionPins Versions { get; }
    public ResearchBudget Budget { get; }
    public ResearchRunAttemptStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public ResearchUsage? Usage { get; private set; }
    public string? ResultCode { get; private set; }
    public void Start(DateTimeOffset at) { Require(ResearchRunAttemptStatus.Created); StartedAt = Ordered(at, CreatedAt); Status = ResearchRunAttemptStatus.Running; }
    public void WaitForTool() { Require(ResearchRunAttemptStatus.Running); Status = ResearchRunAttemptStatus.WaitingForTool; }
    public void Resume() { Require(ResearchRunAttemptStatus.WaitingForTool); Status = ResearchRunAttemptStatus.Running; }
    public void Terminate(ResearchRunAttemptStatus status, ResearchUsage usage, string resultCode, DateTimeOffset at)
    {
        if (Status is not ResearchRunAttemptStatus.Running and not ResearchRunAttemptStatus.WaitingForTool) throw new InvalidOperationException("Only an active attempt can terminate.");
        if (status is < ResearchRunAttemptStatus.Completed or > ResearchRunAttemptStatus.Recovered) throw new ArgumentException("A terminal status is required.", nameof(status));
        Usage = usage ?? throw new ArgumentNullException(nameof(usage)); ResultCode = ResearchValidation.Required(resultCode, nameof(resultCode), 200);
        CompletedAt = Ordered(at, StartedAt!.Value); Status = status;
    }
    private void Require(ResearchRunAttemptStatus status) { if (Status != status) throw new InvalidOperationException($"Attempt must be {status}."); }
    private static DateTimeOffset Ordered(DateTimeOffset at, DateTimeOffset lower) { ResearchValidation.Utc(at, nameof(at)); if (at < lower) throw new ArgumentException("Timestamp is out of order.", nameof(at)); return at; }
}
