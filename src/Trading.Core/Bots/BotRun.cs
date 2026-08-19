using Trading.Core.Identifiers;
using Trading.Core.Policies;

namespace Trading.Core.Bots;

public enum BotRunStatus { Pending, AcquiringLease, PreparingSnapshot, Reasoning, WaitingForTool, Completed, TimedOut, BudgetExceeded, Cancelled, Faulted }
public enum BotRunTriggerType { Manual, BaselineSchedule, AcceptedNextRun, ResearchCompleted, ResearchFailed, PortfolioEvent, RiskOrReconciliation }
public enum ToolInvocationStatus { Running, Completed, Failed }

public interface IBotRunScheduler
{
    void AcceptNextRun(DateTimeOffset acceptedAt);
}

public sealed class BotRun : IBotRunScheduler
{
    private readonly List<BotRunTrigger> _triggers = [];
    private readonly List<ToolInvocation> _toolInvocations = [];

    public BotRun(BotRunId id, TradingBotId tradingBotId, TradingBotConfigurationVersionId configurationVersionId,
        PortfolioDecisionSnapshotId portfolioSnapshotId, Usage usage)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        TradingBotId = tradingBotId ?? throw new ArgumentNullException(nameof(tradingBotId));
        ConfigurationVersionId = configurationVersionId ?? throw new ArgumentNullException(nameof(configurationVersionId));
        PortfolioSnapshotId = portfolioSnapshotId ?? throw new ArgumentNullException(nameof(portfolioSnapshotId));
        Usage = usage ?? throw new ArgumentNullException(nameof(usage));
    }

    public BotRunId Id { get; }
    public TradingBotId TradingBotId { get; }
    public TradingBotConfigurationVersionId ConfigurationVersionId { get; }
    public PortfolioDecisionSnapshotId PortfolioSnapshotId { get; }
    public BotRunStatus Status { get; private set; } = BotRunStatus.Pending;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public FinishResult? FinishResult { get; private set; }
    public DateTimeOffset? RequestedNextRunAt => FinishResult?.RequestedNextRunAt;
    public DateTimeOffset? AcceptedNextRunAt { get; private set; }
    public Usage Usage { get; private set; }
    public IReadOnlyList<BotRunTrigger> Triggers => _triggers.AsReadOnly();
    public IReadOnlyList<ToolInvocation> ToolInvocations => _toolInvocations.AsReadOnly();
    public int ModelTranscriptSchemaVersion { get; private set; } = 1;
    public string ModelTranscriptJson { get; private set; } = "{}";
    public string InputRenderingVersion { get; private set; } = "1";
    public string? InputRenderingHash { get; private set; }
    public string? TerminalReason { get; private set; }
    public long Version { get; private set; }
    public static IReadOnlySet<BotRunStatus> ActiveStatuses { get; } = new HashSet<BotRunStatus>
    {
        BotRunStatus.Pending, BotRunStatus.AcquiringLease, BotRunStatus.PreparingSnapshot,
        BotRunStatus.Reasoning, BotRunStatus.WaitingForTool,
    };
    public bool IsTerminal => !ActiveStatuses.Contains(Status);

    public static BotRun Rehydrate(BotRunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var run = new BotRun(state.Id, state.TradingBotId, state.ConfigurationVersionId,
            state.PortfolioSnapshotId, state.Usage)
        {
            Status = state.Status,
            StartedAt = state.StartedAt,
            CompletedAt = state.CompletedAt,
            LeaseOwner = state.LeaseOwner,
            LeaseExpiresAt = state.LeaseExpiresAt,
            FinishResult = state.FinishResult,
            AcceptedNextRunAt = state.AcceptedNextRunAt,
            ModelTranscriptSchemaVersion = state.ModelTranscriptSchemaVersion,
            ModelTranscriptJson = BotValidation.Required(state.ModelTranscriptJson, nameof(state.ModelTranscriptJson)),
            InputRenderingVersion = BotValidation.Required(state.InputRenderingVersion, nameof(state.InputRenderingVersion)),
            InputRenderingHash = state.InputRenderingHash,
            TerminalReason = state.TerminalReason,
            Version = state.Version,
        };
        run._triggers.AddRange(state.Triggers.OrderBy(x => x.SequenceNumber).Select(x =>
            new BotRunTrigger(x.Id, x.Type, x.Reason, x.OccurredAt, x.SourceId)));
        run._toolInvocations.AddRange(state.ToolInvocations.OrderBy(x => x.SequenceNumber).Select(ToolInvocation.Rehydrate));
        return run;
    }

    public void AddTrigger(BotRunTriggerId id, BotRunTriggerType type, string reason, DateTimeOffset occurredAt, string? sourceId = null)
    {
        EnsureNotTerminal();
        if (_triggers.Any(trigger => trigger.Id == id)) return;
        _triggers.Add(new BotRunTrigger(id, type, reason, occurredAt, sourceId));
    }

    public void BeginLeaseAcquisition(DateTimeOffset startedAt)
    {
        RequireStatus(BotRunStatus.Pending);
        StartedAt = BotValidation.Utc(startedAt, nameof(startedAt));
        Status = BotRunStatus.AcquiringLease;
    }

    public void LeaseAcquired(string leaseOwner, DateTimeOffset leaseExpiresAt)
    {
        RequireStatus(BotRunStatus.AcquiringLease);
        LeaseOwner = BotValidation.Required(leaseOwner, nameof(leaseOwner));
        if (StartedAt is null) throw new InvalidOperationException("Run start time is required.");
        LeaseExpiresAt = BotValidation.Utc(leaseExpiresAt, nameof(leaseExpiresAt));
        var startedAt = StartedAt.Value;
        LeaseExpiresAt = BotValidation.Utc(leaseExpiresAt, nameof(leaseExpiresAt));
        if (leaseExpiresAt <= startedAt) throw new ArgumentException("Lease expiry must follow start time.", nameof(leaseExpiresAt));
        Status = BotRunStatus.PreparingSnapshot;
    }

    public void BeginReasoning() { RequireStatus(BotRunStatus.PreparingSnapshot); Status = BotRunStatus.Reasoning; }

    public void RecordInputRendering(string version, string sha256Hash)
    {
        RequireStatus(BotRunStatus.PreparingSnapshot);
        InputRenderingVersion = BotValidation.Required(version, nameof(version));
        var normalizedHash = BotValidation.Required(sha256Hash, nameof(sha256Hash));
        if (normalizedHash.Length != 64 || normalizedHash.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Input rendering hash must be a SHA-256 hexadecimal value.", nameof(sha256Hash));
        InputRenderingHash = normalizedHash.ToLowerInvariant();
    }
    public void WaitForTool() { RequireStatus(BotRunStatus.Reasoning); Status = BotRunStatus.WaitingForTool; }
    public void ResumeReasoning() { RequireStatus(BotRunStatus.WaitingForTool); Status = BotRunStatus.Reasoning; }

    public void RecordModelProgress(int transcriptSchemaVersion, string canonicalTranscript, Usage usage)
    {
        if (Status is not BotRunStatus.Reasoning and not BotRunStatus.WaitingForTool && !IsTerminal)
            throw new InvalidOperationException("Model progress can be recorded only during reasoning or tool dispatch.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(transcriptSchemaVersion);
        ModelTranscriptSchemaVersion = transcriptSchemaVersion;
        ModelTranscriptJson = BotValidation.Required(canonicalTranscript, nameof(canonicalTranscript));
        Usage = usage ?? throw new ArgumentNullException(nameof(usage));
    }

    public void RecordTerminalReason(string reason)
    {
        EnsureNotTerminal();
        TerminalReason = BotValidation.Required(reason, nameof(reason));
    }

    public void RenewLease(string leaseOwner, DateTimeOffset leaseExpiresAt)
    {
        if (Status is not BotRunStatus.PreparingSnapshot and not BotRunStatus.Reasoning and not BotRunStatus.WaitingForTool)
            throw new InvalidOperationException("A lease can be renewed only while an acquired run is active.");
        if (!string.Equals(LeaseOwner, leaseOwner, StringComparison.Ordinal)) throw new InvalidOperationException("Only the lease owner can renew the lease.");
        BotValidation.Utc(leaseExpiresAt, nameof(leaseExpiresAt));
        if (leaseExpiresAt <= LeaseExpiresAt) throw new ArgumentException("A renewed lease must extend the current lease.", nameof(leaseExpiresAt));
        LeaseExpiresAt = leaseExpiresAt;
    }

    public ToolInvocation StartToolInvocation(ToolInvocationId id, string toolName, string arguments, DateTimeOffset startedAt)
    {
        RequireStatus(BotRunStatus.WaitingForTool);
        if (_toolInvocations.Any(invocation => invocation.Id == id)) throw new InvalidOperationException("Tool invocation identity already exists.");
        var invocation = new ToolInvocation(id, toolName, arguments, startedAt);
        _toolInvocations.Add(invocation);
        return invocation;
    }

    public void Complete(FinishResult result, Usage usage, DateTimeOffset completedAt) => Finish(BotRunStatus.Completed, result, usage, completedAt);
    public void TimeOut(Usage usage, DateTimeOffset completedAt) => Finish(BotRunStatus.TimedOut, null, usage, completedAt);
    public void ExceedBudget(Usage usage, DateTimeOffset completedAt) => Finish(BotRunStatus.BudgetExceeded, null, usage, completedAt);
    public void Cancel(Usage usage, DateTimeOffset completedAt) => Finish(BotRunStatus.Cancelled, null, usage, completedAt);
    public void Fault(Usage usage, DateTimeOffset completedAt) => Finish(BotRunStatus.Faulted, null, usage, completedAt);

    void IBotRunScheduler.AcceptNextRun(DateTimeOffset acceptedAt)
    {
        if (!IsTerminal) throw new InvalidOperationException("A schedule can be accepted only after the run is terminal.");
        if (RequestedNextRunAt is null) throw new InvalidOperationException("The run did not request a next-run time.");
        AcceptedNextRunAt = BotValidation.Utc(acceptedAt, nameof(acceptedAt));
    }

    private void Finish(BotRunStatus terminalStatus, FinishResult? result, Usage usage, DateTimeOffset completedAt)
    {
        if (!ActiveStatuses.Contains(Status)) throw new InvalidOperationException("A terminal run cannot transition again.");
        var terminalAllowed = Status switch
        {
            BotRunStatus.Pending => terminalStatus is BotRunStatus.Cancelled or BotRunStatus.Faulted,
            BotRunStatus.AcquiringLease or BotRunStatus.PreparingSnapshot =>
                terminalStatus is BotRunStatus.TimedOut or BotRunStatus.Cancelled or BotRunStatus.Faulted,
            BotRunStatus.Reasoning or BotRunStatus.WaitingForTool => true,
            _ => false,
        };
        if (!terminalAllowed) throw new InvalidOperationException($"Transition from {Status} to {terminalStatus} is forbidden.");
        if (_toolInvocations.Any(invocation => invocation.Status == ToolInvocationStatus.Running))
            throw new InvalidOperationException("A run cannot finish while a tool invocation is active.");
        BotValidation.Utc(completedAt, nameof(completedAt));
        if (StartedAt is not null && completedAt < StartedAt) throw new ArgumentException("Completion cannot precede start.", nameof(completedAt));
        FinishResult = result;
        Usage = usage ?? throw new ArgumentNullException(nameof(usage));
        CompletedAt = completedAt;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        Status = terminalStatus;
    }

    private void RequireStatus(BotRunStatus required)
    {
        if (Status != required) throw new InvalidOperationException($"Run must be {required} but is {Status}.");
    }
    private void EnsureNotTerminal()
    {
        if (IsTerminal) throw new InvalidOperationException("Terminal runs cannot resume or accept new facts.");
    }
}

public sealed record BotRunState(BotRunId Id, TradingBotId TradingBotId,
    TradingBotConfigurationVersionId ConfigurationVersionId, PortfolioDecisionSnapshotId PortfolioSnapshotId,
    BotRunStatus Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? LeaseOwner,
    DateTimeOffset? LeaseExpiresAt, FinishResult? FinishResult, DateTimeOffset? AcceptedNextRunAt,
    Usage Usage, IReadOnlyList<BotRunTriggerState> Triggers, IReadOnlyList<ToolInvocationState> ToolInvocations,
    int ModelTranscriptSchemaVersion, string ModelTranscriptJson, string InputRenderingVersion, string? InputRenderingHash,
    string? TerminalReason, long Version);
public sealed record BotRunTriggerState(int SequenceNumber, BotRunTriggerId Id, BotRunTriggerType Type,
    string Reason, DateTimeOffset OccurredAt, string? SourceId);
public sealed record ToolInvocationState(int SequenceNumber, ToolInvocationId Id, string ToolName,
    string Arguments, ToolInvocationStatus Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt,
    string? ResultReference, string? Error, Usage? Usage);

public sealed record BotRunTrigger
{
    internal BotRunTrigger(BotRunTriggerId id, BotRunTriggerType type, string reason, DateTimeOffset occurredAt, string? sourceId)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)); Type = type;
        Reason = BotValidation.Required(reason, nameof(reason)); OccurredAt = BotValidation.Utc(occurredAt, nameof(occurredAt));
        SourceId = sourceId is null ? null : BotValidation.Required(sourceId, nameof(sourceId));
    }
    public BotRunTriggerId Id { get; }
    public BotRunTriggerType Type { get; }
    public string Reason { get; }
    public DateTimeOffset OccurredAt { get; }
    public string? SourceId { get; }
}

public sealed class ToolInvocation
{
    internal ToolInvocation(ToolInvocationId id, string toolName, string arguments, DateTimeOffset startedAt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id)); ToolName = BotValidation.Required(toolName, nameof(toolName));
        Arguments = BotValidation.Required(arguments, nameof(arguments)); StartedAt = BotValidation.Utc(startedAt, nameof(startedAt));
    }
    public ToolInvocationId Id { get; }
    public string ToolName { get; }
    public string Arguments { get; }
    public ToolInvocationStatus Status { get; private set; } = ToolInvocationStatus.Running;
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ResultReference { get; private set; }
    public string? Error { get; private set; }
    public Usage? Usage { get; private set; }

    internal static ToolInvocation Rehydrate(ToolInvocationState state)
    {
        var invocation = new ToolInvocation(state.Id, state.ToolName, state.Arguments, state.StartedAt)
        {
            Status = state.Status,
            CompletedAt = state.CompletedAt,
            ResultReference = state.ResultReference,
            Error = state.Error,
            Usage = state.Usage,
        };
        return invocation;
    }

    public void Complete(string resultReference, Usage usage, DateTimeOffset completedAt) =>
        Finish(ToolInvocationStatus.Completed, resultReference, null, usage, completedAt);
    public void Fail(string error, Usage usage, DateTimeOffset completedAt) =>
        Finish(ToolInvocationStatus.Failed, null, BotValidation.Required(error, nameof(error)), usage, completedAt);

    private void Finish(ToolInvocationStatus status, string? resultReference, string? error, Usage usage, DateTimeOffset completedAt)
    {
        if (Status != ToolInvocationStatus.Running) throw new InvalidOperationException("Completed tool invocations are append-only facts.");
        BotValidation.Utc(completedAt, nameof(completedAt));
        if (completedAt < StartedAt) throw new ArgumentException("Completion cannot precede start.", nameof(completedAt));
        Status = status; ResultReference = resultReference is null ? null : BotValidation.Required(resultReference, nameof(resultReference));
        Error = error; Usage = usage ?? throw new ArgumentNullException(nameof(usage)); CompletedAt = completedAt;
    }
}
