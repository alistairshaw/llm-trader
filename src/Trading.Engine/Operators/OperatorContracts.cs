using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Core.Research;

namespace Trading.Engine.Operators;

public enum OperatorAuthority
{
    ReadOperations,
    ManageBots,
    TriggerRuns,
    RequestResearch,
    DecideProposals,
    ManageKillSwitches,
}

public enum OperatorResourceKind { Platform, TradingBot, Portfolio, BrokerAccount, ResearchReport, TradeProposal, Order }
public enum OperatorPageKind { Overview, Bots, Portfolios, Runs, Research, Proposals, Execution, RiskAndAudit }
public enum OperatorResultStatus { Succeeded, Unavailable, Conflict, Invalid, Cancelled }
public enum OperatorWarningSeverity { Information, Warning, Critical }
public enum OperatorCommandKind
{
    CreateBot, ConfigureBot, AssignPortfolio, PauseBot, ResumeBot, RetireBot,
    TriggerManualRun, RequestResearch, ApproveProposal, RejectProposal,
    ActivateKillSwitch, ClearKillSwitch,
}

public sealed record OperatorResource(OperatorResourceKind Kind, string Id)
{
    public static OperatorResource Platform { get; } = new(OperatorResourceKind.Platform, "platform");
}

public sealed record OperatorPrincipal
{
    public OperatorPrincipal(string actorId, IEnumerable<OperatorAuthority> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(permissions);
        ActorId = actorId.Trim();
        Permissions = new ReadOnlyCollection<OperatorAuthority>(permissions.Distinct().Order().ToArray());
    }

    public string ActorId { get; }
    public IReadOnlyList<OperatorAuthority> Permissions { get; }
}

public readonly record struct OperatorPageRequest
{
    public const int MaximumSize = 200;
    public OperatorPageRequest(int offset, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, MaximumSize);
        Offset = offset;
        Size = size;
    }
    public int Offset { get; }
    public int Size { get; }
}

public sealed record OperatorFilter(string? Search = null, string? Status = null, DateTimeOffset? From = null,
    DateTimeOffset? To = null);
public sealed record OperatorWarning(string Code, string Message, OperatorWarningSeverity Severity);
public sealed record OperatorProgress(string OperationId, string Stage, int? Percent, string Message,
    DateTimeOffset UpdatedAt);
public sealed record OperatorPage<T>
{
    public OperatorPage(IEnumerable<T> items, int offset, int? nextOffset)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = new ReadOnlyCollection<T>(items.ToArray());
        Offset = offset;
        NextOffset = nextOffset;
    }
    public IReadOnlyList<T> Items { get; }
    public int Offset { get; }
    public int? NextOffset { get; }
}

public sealed record OperatorQueryResult<T>(OperatorResultStatus Status, T? Value);

public sealed record OperatorCommandResult(OperatorResultStatus Status, string Code, string? ResourceId = null,
    long? Version = null, OperatorProgress? Progress = null)
{
    public static OperatorCommandResult Unavailable() => new(OperatorResultStatus.Unavailable, "operator.unavailable");
}

public sealed record OperatorCommand(OperatorCommandKind Kind, OperatorResource Resource, long? ExpectedVersion,
    IReadOnlyDictionary<string, string> Arguments)
{
    public static OperatorCommand Create(OperatorCommandKind kind, OperatorResource resource, long? expectedVersion,
        IEnumerable<KeyValuePair<string, string>>? arguments = null) => new(kind, resource, expectedVersion,
        new ReadOnlyDictionary<string, string>((arguments ?? []).ToDictionary(x => x.Key, x => x.Value,
            StringComparer.Ordinal)));
}

public sealed record OperatorOverview(int ActiveBots, int ActiveRuns, int PendingProposals, int OpenOrders,
    ImmutableArray<OperatorWarning> Warnings);
public sealed record BotSummary(TradingBotId Id, string Name, TradingBotStatus Status, PortfolioId? PortfolioId,
    TradingBotConfigurationVersionId? ConfigurationId, long Version);
public sealed record BotDetail(BotSummary Summary, ExecutionMode ExecutionMode, DateTimeOffset? NextRunAt,
    ImmutableArray<OperatorWarning> Warnings);
public sealed record PortfolioSummary(PortfolioId Id, TradingBotId TradingBotId, BrokerAccountId AccountId,
    string Currency, decimal Cash, decimal ReservedCapital, int PositionCount);
public sealed record RunSummary(BotRunId Id, TradingBotId TradingBotId, BotRunStatus Status, DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt, int ToolCalls, decimal Cost);
public sealed record QueuedRunTriggerSummary(BotRunTriggerId Id, TradingBotId TradingBotId,
    BotRunTriggerType TriggerType, string Reason, DateTimeOffset OccurredAt, string? SourceId);
public sealed record RunTriggerSummary(BotRunTriggerId Id, BotRunTriggerType TriggerType, string Reason,
    DateTimeOffset OccurredAt, string? SourceId);
public sealed record RunBudgetSummary(string Name, string Limit, string Used);
public sealed record RunDetail(RunSummary Summary, TradingBotConfigurationVersionId ConfigurationVersionId,
    PortfolioDecisionSnapshotId PortfolioSnapshotId, ImmutableArray<RunTriggerSummary> Triggers,
    ImmutableArray<RunBudgetSummary> Budgets, Usage Usage, FinishResult? FinishResult,
    DateTimeOffset? RequestedNextRunAt, DateTimeOffset? AcceptedNextRunAt, string? FailureCode,
    bool WasRecovered);
public sealed record ResearchSummary(ResearchReportId Id, string SeriesId, int Version, string Subject,
    ResearchReportStatus Status, DateTimeOffset PublishedAt);
public sealed record ResearchDetail(ResearchSummary Summary, string ContentHash, string Content,
    ImmutableArray<string> Citations);
public sealed record ProposalSummary(TradeProposalId Id, TradingBotId TradingBotId, PortfolioId PortfolioId,
    ProposalStatus Status, DateTimeOffset ValidUntil, long Version);
public sealed record ProposalDetail(ProposalSummary Summary, string Rationale, string ContentHash,
    ImmutableArray<string> Evidence, ImmutableArray<OperatorWarning> Warnings);
public sealed record ExecutionSummary(OrderId Id, PortfolioId PortfolioId, OrderStatus Status, string Instrument,
    decimal Quantity, decimal FilledQuantity, string Currency, DateTimeOffset UpdatedAt);
public sealed record AuditSummary(string Id, string Kind, string Code, DateTimeOffset At, string CorrelationId);
public sealed record KillSwitchSummary(OperatorResource Scope, bool IsActive, string Reason, string ActorId,
    DateTimeOffset ChangedAt, long Version);

public sealed record BotConfigurationInput(string Mandate, string RiskPolicyVersion, string ToolPolicyVersion,
    string SchedulingPolicyVersion, ExecutionMode ExecutionMode, string Model, string PromptVersion);

public interface IOperatorAuthorization
{
    Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission,
        OperatorResource resource, CancellationToken cancellationToken);
}

public interface IOperatorWorkflowPort
{
    Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
        OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
        CancellationToken cancellationToken);
    Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command,
        CancellationToken cancellationToken);
}

public interface IOperatorQueries
{
    Task<OperatorQueryResult<OperatorOverview>> GetOverviewAsync(OperatorPrincipal principal, CancellationToken cancellationToken);
    Task<OperatorQueryResult<OperatorPage<T>>> GetPageAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
        OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest, CancellationToken cancellationToken);
}

public interface IBotOperatorService
{
    Task<OperatorCommandResult> CreateAsync(OperatorPrincipal principal, string name, CancellationToken cancellationToken);
    Task<OperatorCommandResult> ConfigureAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion,
        BotConfigurationInput configuration, CancellationToken cancellationToken);
    Task<OperatorCommandResult> AssignAsync(OperatorPrincipal principal, TradingBotId id, PortfolioId portfolioId,
        long expectedVersion, CancellationToken cancellationToken);
    Task<OperatorCommandResult> PauseAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion, CancellationToken cancellationToken);
    Task<OperatorCommandResult> ResumeAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion, CancellationToken cancellationToken);
    Task<OperatorCommandResult> RetireAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion, CancellationToken cancellationToken);
}

public interface IRunOperatorService
{
    Task<OperatorCommandResult> TriggerAsync(OperatorPrincipal principal, TradingBotId id, string reason, CancellationToken cancellationToken);
}

public interface IResearchOperatorService
{
    Task<OperatorCommandResult> RequestAsync(OperatorPrincipal principal, TradingBotId id, string subject, CancellationToken cancellationToken);
}

public interface IProposalOperatorService
{
    Task<OperatorCommandResult> ApproveAsync(OperatorPrincipal principal, TradeProposalId id, long expectedVersion,
        string? reason, CancellationToken cancellationToken);
    Task<OperatorCommandResult> RejectAsync(OperatorPrincipal principal, TradeProposalId id, long expectedVersion,
        string reason, CancellationToken cancellationToken);
}

public interface IKillSwitchOperatorService
{
    Task<OperatorCommandResult> ActivateAsync(OperatorPrincipal principal, OperatorResource scope,
        long expectedVersion, string reason, CancellationToken cancellationToken);
    Task<OperatorCommandResult> ClearAsync(OperatorPrincipal principal, OperatorResource scope,
        long expectedVersion, string reason, CancellationToken cancellationToken);
}
