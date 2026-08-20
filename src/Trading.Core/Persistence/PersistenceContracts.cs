using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Core.Proposals;
using Trading.Core.Research;

namespace Trading.Core.Persistence;

public abstract record PersistenceWriteResult
{
    private PersistenceWriteResult() { }

    public sealed record Succeeded : PersistenceWriteResult;

    public sealed record UniquenessConflict : PersistenceWriteResult
    {
        public UniquenessConflict(string constraint) => Constraint = RequireValue(constraint, nameof(constraint));
        public string Constraint { get; }
    }

    public sealed record ConcurrencyConflict : PersistenceWriteResult
    {
        public ConcurrencyConflict(long expectedVersion, long? actualVersion)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
            if (actualVersion is not null) ArgumentOutOfRangeException.ThrowIfNegative(actualVersion.Value);
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        public long ExpectedVersion { get; }
        public long? ActualVersion { get; }
    }

    private static string RequireValue(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IBrokerConnectionRepository
{
    Task<BrokerConnection?> GetAsync(BrokerConnectionId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(BrokerConnection connection, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(BrokerConnection connection, long expectedVersion, CancellationToken cancellationToken);
}

public interface IBrokerAccountRepository
{
    Task<BrokerAccount?> GetAsync(BrokerAccountId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(BrokerAccount account, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(BrokerAccount account, long expectedVersion, CancellationToken cancellationToken);
}

public interface IInstrumentRepository
{
    Task<Instrument?> GetAsync(InstrumentId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(Instrument instrument, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(Instrument instrument, long expectedVersion, CancellationToken cancellationToken);
}

public interface ITradingBotRepository
{
    Task<TradingBot?> GetAsync(TradingBotId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(TradingBot bot, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(TradingBot bot, long expectedVersion, CancellationToken cancellationToken);
}

public interface IPortfolioRepository
{
    Task<Portfolio?> GetAsync(PortfolioId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(Portfolio portfolio, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(Portfolio portfolio, long expectedVersion, CancellationToken cancellationToken);
}

public interface IPositionRepository
{
    Task<Position?> GetAsync(PositionId id, CancellationToken cancellationToken);
    Task<Position?> GetForPortfolioInstrumentAsync(PortfolioId portfolioId, InstrumentId instrumentId, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AddAsync(Position position, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> UpdateAsync(Position position, long expectedVersion, CancellationToken cancellationToken);
}

public interface IPortfolioLedgerRepository
{
    Task<PortfolioLedgerEntry?> GetAsync(PortfolioLedgerEntryId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> AppendAsync(PortfolioLedgerEntry entry, CancellationToken cancellationToken);
}

public interface IPortfolioDecisionSnapshotRepository
{
    Task<PortfolioDecisionSnapshot?> GetAsync(PortfolioDecisionSnapshotId id, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> PublishAsync(PortfolioDecisionSnapshot snapshot, CancellationToken cancellationToken);
}

public interface IBotRunInputAuditWriter
{
    Task<PersistenceWriteResult> StoreInputRenderingAsync(BotRunId runId, long expectedVersion,
        string renderingVersion, string renderingHash, CancellationToken cancellationToken);
}

public sealed record PendingBotRunTrigger(BotRunTriggerId Id, TradingBotId TradingBotId,
    BotRunTriggerType Type, string Reason, DateTimeOffset OccurredAt, DateTimeOffset CreatedAt,
    string? SourceType = null, string? SourceId = null);

public interface IBotRunTriggerRepository
{
    Task<PersistenceWriteResult> AppendAsync(PendingBotRunTrigger trigger, CancellationToken cancellationToken);
    Task<IReadOnlyList<PendingBotRunTrigger>> GetPendingAsync(TradingBotId botId, CancellationToken cancellationToken);
}

public sealed record BotRunClaim(BotRunId RunId, TradingBotId TradingBotId,
    TradingBotConfigurationVersionId ConfigurationVersionId, PortfolioDecisionSnapshotId PortfolioSnapshotId,
    string LeaseOwner, DateTimeOffset StartedAt, DateTimeOffset LeaseExpiresAt, Usage InitialUsage,
    int ModelTranscriptSchemaVersion, string ModelTranscriptJson, string InputRenderingVersion);

public abstract record BotRunLeaseResult
{
    private BotRunLeaseResult() { }
    public sealed record Acquired(BotRun Run) : BotRunLeaseResult;
    public sealed record ActiveLeaseConflict(BotRunId? ActiveRunId) : BotRunLeaseResult;
    public sealed record ConcurrencyConflict : BotRunLeaseResult;
}

public interface IBotRunRepository
{
    Task<BotRun?> GetAsync(BotRunId id, CancellationToken cancellationToken);
    Task<BotRunLeaseResult> TryClaimAsync(BotRunClaim claim, CancellationToken cancellationToken);
    Task<bool> RenewLeaseAsync(BotRunId runId, string leaseOwner, DateTimeOffset newExpiry,
        long expectedVersion, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> SaveAsync(BotRun run, long expectedVersion, CancellationToken cancellationToken);
    Task<IReadOnlyList<BotRunId>> GetExpiredLeaseRunIdsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<PersistenceWriteResult> RecoverExpiredAsync(BotRun run, long expectedVersion,
        PendingBotRunTrigger? followUpTrigger, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This repository does not support atomic runtime recovery.");
}

public sealed record ResearchAttemptClaim(ResearchRunAttempt Attempt, int AttemptNumber);
public abstract record ResearchClaimResult
{
    private ResearchClaimResult() { }
    public sealed record Acquired(ResearchRunAttempt Attempt) : ResearchClaimResult;
    public sealed record ActiveAttemptConflict(ResearchRunAttemptId? ActiveAttemptId) : ResearchClaimResult;
    public sealed record ConcurrencyConflict : ResearchClaimResult;
}

public sealed record ResearchToolAudit(string Id, ResearchRunAttemptId AttemptId, int SequenceNumber,
    string ToolName, int SchemaVersion, string ArgumentsJson, string Status, DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt, string? ResultJson, string? ErrorCode, string? ErrorDetail, string? UsageJson);

public interface IResearchRequestRepository
{
    Task<ResearchRequest?> GetAsync(ResearchRequestId id, CancellationToken token);
    Task<PersistenceWriteResult> AddAsync(ResearchRequest request, CancellationToken token);
    Task<PersistenceWriteResult> SaveAsync(ResearchRequest request, long expectedVersion, CancellationToken token);
    Task<ResearchClaimResult> TryClaimQueuedAsync(ResearchRequestId requestId, ResearchAttemptClaim claim, CancellationToken token);
}

public sealed record AuthorizedResearchRequest(ResearchRequest Request, ResearchSubscriptionId SubscriptionId,
    string CanonicalSpecification, ResearchReportId? RefreshReportId);
public abstract record ResearchRequestPersistenceDecision
{
    private ResearchRequestPersistenceDecision() { }
    public sealed record Reused(ResearchReportId ReportId) : ResearchRequestPersistenceDecision;
    public sealed record Subscribed(ResearchRequestId RequestId, ResearchSubscriptionId SubscriptionId) : ResearchRequestPersistenceDecision;
    public sealed record Queued(ResearchRequestId RequestId, ResearchSubscriptionId SubscriptionId) : ResearchRequestPersistenceDecision;
    public sealed record RefreshUnauthorized : ResearchRequestPersistenceDecision;
}
public interface IResearchRequestDecisionRepository
{
    Task<ResearchRequestPersistenceDecision> DecideAsync(AuthorizedResearchRequest candidate,
        ResearchPrincipal principal, DateTimeOffset now, CancellationToken token);
}

public interface IResearchRunAttemptRepository
{
    Task<ResearchRunAttempt?> GetAsync(ResearchRunAttemptId id, CancellationToken token);
    Task<PersistenceWriteResult> SaveAsync(ResearchRunAttempt attempt, long expectedVersion, CancellationToken token);
    Task<PersistenceWriteResult> AppendToolAuditAsync(ResearchToolAudit audit, CancellationToken token);
    Task<IReadOnlyList<ResearchToolAudit>> GetToolAuditAsync(ResearchRunAttemptId id, CancellationToken token);
}

public interface IResearchReportRepository
{
    Task<ResearchReport?> GetAsync(ResearchReportId id, CancellationToken token);
    Task<PersistenceWriteResult> PublishAsync(ResearchReport report, ResearchRunAttemptId attemptId, CancellationToken token);
    Task<ResearchReport> PublishCompletedAsync(ResearchPublication publication, CancellationToken token);
}

public sealed record ResearchPublication(ResearchReportId ReportId, ResearchRequest Request,
    ResearchRunAttempt Attempt, string CanonicalContent, string ContentHash, ReportProvenance Provenance,
    DateTimeOffset DataCutoff, DateTimeOffset GeneratedAt, DateTimeOffset ExpiresAt, ResearchReportId? RefreshReportId,
    GeneratorMetadata GeneratorMetadata);

public sealed record ResearchReportSearch(ResearchPrincipal Principal, DateTimeOffset At, string? Subject = null,
    string? NormalizedResearchKey = null, bool FreshOnly = false, int Offset = 0, int Size = 50);
public sealed record ResearchReportSummary(ResearchReportId Id, string SeriesId, int Version, string Subject,
    ResearchReportStatus Status, DateTimeOffset DataCutoff, DateTimeOffset GeneratedAt,
    DateTimeOffset ExpiresAt, bool IsFresh);
public interface IResearchReportCatalogQueries
{
    Task<IReadOnlyList<ResearchReportSummary>> SearchAsync(ResearchReportSearch search, CancellationToken token);
    Task<ResearchReport?> GetAuthorizedAsync(ResearchPrincipal principal, ResearchReportId id, CancellationToken token);
    Task<ResearchReport?> GetAuthorizedVersionAsync(ResearchPrincipal principal, string seriesId, int version, CancellationToken token);
}

public sealed record ResearchSubscriptionNotification(ResearchSubscriptionId SubscriptionId,
    ResearchRequestId RequestId, TradingBotId TradingBotId, ResearchTerminalOutcome Outcome,
    ResearchReportId? ReportId, int? ReportVersion, string CorrelationId, DateTimeOffset DeliveredAt);
public abstract record ResearchNotificationDeliveryResult
{
    private ResearchNotificationDeliveryResult() { }
    public sealed record Delivered(ResearchSubscriptionNotification Notification, BotRunTriggerId TriggerId) : ResearchNotificationDeliveryResult;
    public sealed record AlreadyDelivered(ResearchSubscriptionNotification Notification, BotRunTriggerId TriggerId) : ResearchNotificationDeliveryResult;
    public sealed record NotTerminal : ResearchNotificationDeliveryResult;
    public sealed record ConcurrencyConflict : ResearchNotificationDeliveryResult;
}
public interface IResearchNotificationRepository
{
    Task<IReadOnlyList<ResearchSubscriptionId>> GetPendingAsync(ResearchRequestId requestId, int limit, CancellationToken token);
    Task<ResearchNotificationDeliveryResult> DeliverAsync(ResearchSubscriptionId subscriptionId,
        BotRunTriggerId triggerId, DateTimeOffset deliveredAt, CancellationToken token);
}

public sealed record ResearchOrchestrationWork(ResearchRequest Request, ResearchRunAttempt Attempt,
    long RequestVersion, long AttemptVersion, int AttemptNumber, ResearchReportId? RefreshReportId);

public interface IResearchOrchestrationRepository
{
    Task<IReadOnlyList<ResearchRequestId>> GetQueuedAsync(int limit, CancellationToken token);
    Task<ResearchOrchestrationWork?> TryClaimAsync(ResearchRequestId requestId, ResearchRunAttempt attempt,
        CancellationToken token);
    Task<PersistenceWriteResult> TerminalizeAsync(ResearchRequestId requestId, ResearchRunAttempt attempt,
        ResearchRequestStatus requestStatus, long expectedAttemptVersion, CancellationToken token);
    Task<IReadOnlyList<ResearchRunAttemptId>> GetOrphanedAsync(DateTimeOffset recoveryBefore, int limit,
        CancellationToken token);
    Task<PersistenceWriteResult> RecoverAndRequeueAsync(ResearchRunAttemptId attemptId, DateTimeOffset recoveredAt,
        string resultCode, CancellationToken token);
}

public interface IHypothesisRepository
{
    Task<Hypothesis?> GetAsync(HypothesisId id, CancellationToken token);
    Task<HypothesisVersion?> GetVersionAsync(HypothesisVersionId id, CancellationToken token);
    Task<PersistenceWriteResult> AddAsync(Hypothesis hypothesis, CancellationToken token);
    Task<PersistenceWriteResult> SaveAsync(Hypothesis hypothesis, long expectedVersion, CancellationToken token);
}

public abstract record ProposalRecordResult
{
    private ProposalRecordResult() { }
    public sealed record Recorded(TradeProposal Proposal) : ProposalRecordResult;
    public sealed record AlreadyRecorded(TradeProposal Proposal) : ProposalRecordResult;
    public sealed record IdempotencyConflict(TradeProposalId ExistingProposalId) : ProposalRecordResult;
}

public interface ITradeProposalRepository
{
    Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken token);
    Task<ProposalRecordResult> RecordAsync(TradeProposal proposal, string idempotencyKey, CancellationToken token);
    Task<PersistenceWriteResult> SaveAsync(TradeProposal proposal, long expectedVersion, CancellationToken token);
}

public interface ICapitalReservationRepository
{
    Task<CapitalReservation?> GetAsync(CapitalReservationId id, CancellationToken token);
    Task<CapitalReservation?> GetActiveAsync(TradeProposalId proposalId, CancellationToken token);
    Task<IReadOnlyList<CapitalReservation>> GetActiveForPortfolioAsync(PortfolioId portfolioId,
        DateTimeOffset at, CancellationToken token);
    Task<PersistenceWriteResult> AddAsync(CapitalReservation reservation, CancellationToken token);
    Task<PersistenceWriteResult> SaveAsync(CapitalReservation reservation, long expectedVersion, CancellationToken token);
    Task<int> ExpireAsync(PortfolioId portfolioId, DateTimeOffset at, CancellationToken token);
}

public interface IProposalGovernanceTransactionRepository
{
    Task<PersistenceWriteResult> SaveDecisionAndReservationAsync(TradeProposal proposal, long expectedProposalVersion,
        CapitalReservation? reservation, CancellationToken token);
}

public sealed record PortfolioSummary(
    PortfolioId Id,
    string Name,
    Currency BaseCurrency,
    PortfolioStatus Status,
    Money CapitalAllocation,
    BrokerAccountId? BrokerAccountId,
    TradingBotId? AssignedTradingBotId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Version);

public sealed record PositionView(
    PositionId Id,
    PortfolioId PortfolioId,
    InstrumentId InstrumentId,
    decimal Quantity,
    string QuantityUnit,
    Money AverageCost,
    Money RealizedProfitLoss,
    long Version,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt);

public sealed record PortfolioLedgerEntryView(
    PortfolioLedgerEntryId Id,
    PortfolioId PortfolioId,
    PortfolioLedgerEntryType EntryType,
    Money Amount,
    InstrumentId? InstrumentId,
    decimal? Quantity,
    DateTimeOffset EffectiveAt,
    LedgerSourceType SourceType,
    string SourceId,
    DateTimeOffset RecordedAt,
    PortfolioLedgerEntryId? ReversesEntryId,
    string? Description);

public sealed record PortfolioDecisionSnapshotSummary(
    PortfolioDecisionSnapshotId Id,
    PortfolioId PortfolioId,
    TradingBotId TradingBotId,
    TradingBotConfigurationVersionId ConfigurationVersionId,
    DateTimeOffset AsOf,
    ReconciliationStatus ReconciliationStatus,
    string ContentHash,
    DateTimeOffset CreatedAt);

public sealed record BrokerAccountAssociationView(
    PortfolioId PortfolioId,
    BrokerAccountId BrokerAccountId,
    BrokerConnectionId BrokerConnectionId,
    string ExternalAccountId,
    string DisplayName,
    BrokerAccountStatus Status,
    DateTimeOffset? LastReconciledAt,
    long Version);

public readonly record struct PageRequest
{
    public const int MaximumSize = 100;

    public PageRequest(int offset, int size)
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

public readonly record struct PortfolioQueryFilter(BrokerAccountId? BrokerAccountId = null, TradingBotId? TradingBotId = null);
public readonly record struct PositionQueryFilter(PortfolioId? PortfolioId = null, InstrumentId? InstrumentId = null,
    DateTimeOffset? UpdatedFrom = null, DateTimeOffset? UpdatedTo = null);
public readonly record struct PortfolioLedgerQueryFilter(PortfolioId? PortfolioId = null, BrokerAccountId? BrokerAccountId = null,
    TradingBotId? TradingBotId = null, InstrumentId? InstrumentId = null, DateTimeOffset? EffectiveFrom = null, DateTimeOffset? EffectiveTo = null);
public readonly record struct PortfolioDecisionSnapshotQueryFilter(PortfolioId? PortfolioId = null, TradingBotId? TradingBotId = null,
    DateTimeOffset? AsOfFrom = null, DateTimeOffset? AsOfTo = null);

public interface IPortfolioQueries
{
    Task<PortfolioSummary?> GetSummaryAsync(PortfolioId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PortfolioSummary>> GetPortfoliosAsync(PortfolioQueryFilter filter, PageRequest page, CancellationToken cancellationToken);
    Task<IReadOnlyList<PositionView>> GetPositionsAsync(PositionQueryFilter filter, PageRequest page, CancellationToken cancellationToken);
    Task<IReadOnlyList<PortfolioLedgerEntryView>> GetLedgerAsync(PortfolioLedgerQueryFilter filter, PageRequest page, CancellationToken cancellationToken);
    Task<BrokerAccountAssociationView?> GetBrokerAccountAssociationAsync(PortfolioId portfolioId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PortfolioDecisionSnapshotSummary>> GetDecisionSnapshotsAsync(PortfolioDecisionSnapshotQueryFilter filter, PageRequest page, CancellationToken cancellationToken);
}
