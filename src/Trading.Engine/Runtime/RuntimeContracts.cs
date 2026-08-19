using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Engine.Runtime;

public interface IUtcClock { DateTimeOffset UtcNow { get; } }
public interface IAsyncDelay { Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken); }
public sealed record HostInstanceIdentity(string Value);
public interface IHostInstanceIdentityProvider { HostInstanceIdentity Identity { get; } }
public interface IRuntimeIdentifierGenerator { BotRunId NewBotRunId(); BotRunTriggerId NewTriggerId(); ToolInvocationId NewToolInvocationId(); }

public enum ModelFailureKind { Timeout, MalformedResponse, ProviderFailure, Cancellation }
public sealed record ModelFailure(ModelFailureKind Kind, string Message, bool IsRetryable);
public sealed record ModelUsage(long InputTokens, long OutputTokens, decimal Cost);
public sealed record ModelRequest(BotRunId RunId, string Instructions, IReadOnlyList<ToolDefinition> Tools);
public sealed record AssistantResponse(string? Content, IReadOnlyList<ModelToolCall> ToolCalls, ModelUsage Usage, ModelFailure? Failure);
public sealed record ModelToolCall(ToolInvocationId InvocationId, string Name, int SchemaVersion, string CanonicalArguments);
public sealed record ModelToolResult(ToolInvocationId InvocationId, string Name, int SchemaVersion, ToolExecutionOutcome Outcome, string CanonicalResult);

public interface IModelSession
{
    Task<AssistantResponse> GetNextResponseAsync(ModelRequest request, CancellationToken cancellationToken);
    Task SubmitToolResultAsync(ModelToolResult result, CancellationToken cancellationToken);
}

public static class StageThreeTools
{
    public const string GetPortfolioSnapshot = nameof(GetPortfolioSnapshot);
    public const string Finish = nameof(Finish);
}

public sealed record ToolDefinition(string Name, int SchemaVersion, string CanonicalSchema);
public sealed record GetPortfolioSnapshotArguments(PortfolioDecisionSnapshotId SnapshotId);
public sealed record FinishArguments(string Summary, DateTimeOffset? RequestedNextRunAt);
public enum ToolAuthorizationOutcome { Authorized, UnknownTool, Disallowed, InvalidArguments, UnsupportedSchemaVersion }
public enum ToolExecutionOutcome { Succeeded, Rejected, Failed }
public sealed record ToolAuthorizationResult(ToolAuthorizationOutcome Outcome, string Reason);
public sealed record ToolDispatchContext(BotRunId RunId, TradingBotId TradingBotId, PortfolioDecisionSnapshotId SnapshotId);
public sealed record ToolDispatchResult(ModelToolResult Result, ToolAuthorizationResult Authorization);
public interface IToolDispatcher
{
    Task<ToolDispatchResult> DispatchAsync(ToolDispatchContext context, ModelToolCall toolCall, CancellationToken cancellationToken);
}

public enum RunOutcome { Completed, TimedOut, BudgetExceeded, Cancelled, Faulted }
public sealed record RunResult(BotRunId RunId, RunOutcome Outcome, Usage Usage, string? Summary);
public sealed record ScheduleDecision(bool Accepted, DateTimeOffset? NextRunAt, string Reason);
public sealed record BudgetDecision(bool Allowed, string Reason, Usage CurrentUsage);
public sealed record LeaseResult(bool Acquired, string? Owner, DateTimeOffset? ExpiresAt, string Reason);
public sealed record TriggerClaim(BotRunTriggerId TriggerId, BotRunId RunId, bool Claimed);
public sealed record ShutdownResult(int CancelledRuns, bool CompletedWithinDeadline);
public sealed record RecoveryResult(int RecoveredRuns, int FaultedRuns);
