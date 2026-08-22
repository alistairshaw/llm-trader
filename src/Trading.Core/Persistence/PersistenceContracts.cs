using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
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

public sealed record AtomicCapitalReservationRequest(
    CapitalReservation Reservation,
    TradingBotId TradingBotId,
    ProposalContentVersion ApprovedContentVersion,
    FreshStateReference ValidatedState,
    Money GrossAvailableCapital,
    DateTimeOffset At);

public abstract record AtomicCapitalReservationWriteResult
{
    private AtomicCapitalReservationWriteResult() { }
    public sealed record Reserved(CapitalReservation Reservation) : AtomicCapitalReservationWriteResult;
    public sealed record AlreadyReserved(CapitalReservation Reservation) : AtomicCapitalReservationWriteResult;
    public sealed record Rejected(string Code) : AtomicCapitalReservationWriteResult;
    public sealed record Contention : AtomicCapitalReservationWriteResult;
}

public interface IAtomicCapitalReservationRepository
{
    Task<AtomicCapitalReservationWriteResult> TryReserveAsync(
        AtomicCapitalReservationRequest request, CancellationToken token);
}

public sealed record AtomicOrderConversionRequest(
    TradeProposalId ProposalId,
    CapitalReservationId ReservationId,
    OrderId OrderId,
    OrderWorkItemId WorkItemId,
    CorrelationIdentity CorrelationId,
    ClientOrderIdentity ClientOrderId,
    DateTimeOffset At);

public abstract record AtomicOrderConversionWriteResult
{
    private AtomicOrderConversionWriteResult() { }
    public sealed record Created(Order Order) : AtomicOrderConversionWriteResult;
    public sealed record AlreadyCreated(Order Order) : AtomicOrderConversionWriteResult;
    public sealed record Rejected(string Code) : AtomicOrderConversionWriteResult;
    public sealed record NotFound : AtomicOrderConversionWriteResult;
    public sealed record Contention : AtomicOrderConversionWriteResult;
}

public interface IAtomicOrderConversionRepository
{
    Task<AtomicOrderConversionWriteResult> TryConvertAsync(
        AtomicOrderConversionRequest request, CancellationToken token);
}

public static class AtomicOrderConversionCodes
{
    public const string ProposalNotApproved = "order_conversion.proposal_not_approved";
    public const string ProposalExpired = "order_conversion.proposal_expired";
    public const string ResearchOnly = "order_conversion.research_only";
    public const string EnvironmentMismatch = "order_conversion.environment_mismatch";
    public const string ApprovalMismatch = "order_conversion.approval_mismatch";
    public const string EvaluationMismatch = "order_conversion.evaluation_mismatch";
    public const string SnapshotMismatch = "order_conversion.snapshot_mismatch";
    public const string ReservationMismatch = "order_conversion.reservation_mismatch";
    public const string PortfolioMismatch = "order_conversion.portfolio_mismatch";
    public const string AccountRestricted = "order_conversion.account_restricted";
    public const string AccountUnreconciled = "order_conversion.account_unreconciled";
    public const string InstrumentUnavailable = "order_conversion.instrument_unavailable";
    public const string InstrumentMappingUnavailable = "order_conversion.instrument_mapping_unavailable";
    public const string CurrencyMismatch = "order_conversion.currency_mismatch";
    public const string UnsupportedAction = "order_conversion.unsupported_action";
}

public sealed record OrderPersistenceEnvelope(Order Order, CapitalReservationId? ReservationId, CorrelationIdentity CorrelationId);
public interface IOrderRepository
{
    Task<PersistenceWriteResult> AddAsync(OrderPersistenceEnvelope value, CancellationToken token);
    Task<PersistenceWriteResult> SaveAsync(OrderPersistenceEnvelope value, long expectedVersion, CancellationToken token);
    Task<Order?> GetAsync(OrderId id, BrokerAccountId account, PortfolioId portfolio, CancellationToken token);
    Task<Order?> FindByProposalAsync(TradeProposalId proposal, BrokerAccountId account, PortfolioId portfolio, CancellationToken token);
    Task<Order?> FindByClientOrderIdAsync(ClientOrderIdentity clientOrderId, BrokerAccountId account, CancellationToken token);
    Task<Order?> FindByBrokerOrderIdAsync(string brokerOrderId, BrokerAccountId account, CancellationToken token);
    Task<Fill?> FindFillAsync(string executionId, BrokerAccountId account, OrderId order, CancellationToken token);
}
public sealed record BrokerReconciliationRecord(string Id, BrokerAccountId AccountId, string Status, DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt, string SnapshotJson, string DifferencesJson, string ResolutionJson, CorrelationIdentity CorrelationId, string ContentHash);
public interface IBrokerReconciliationRepository
{
    Task<PersistenceWriteResult> AppendAsync(BrokerReconciliationRecord value, CancellationToken token);
    Task<IReadOnlyList<BrokerReconciliationRecord>> ListAsync(BrokerAccountId account, CancellationToken token);
}
public sealed record DurableWorkLease(string Owner, DateTimeOffset ExpiresAt);
public interface IOrderWorkRepository
{
    Task<PersistenceWriteResult> EnqueueAsync(OrderWorkEnvelope value, CancellationToken token);
    Task<IReadOnlyList<OrderWorkEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease, CancellationToken token);
    Task<PersistenceWriteResult> CompleteAsync(OrderWorkItemId id, string owner, string result, DateTimeOffset at, CancellationToken token);
    Task<PersistenceWriteResult> RetryAsync(OrderWorkItemId id, string owner, string errorCode, DateTimeOffset availableAt, CancellationToken token);
    Task<PersistenceWriteResult> RenewAsync(OrderWorkItemId id, string owner, DateTimeOffset expiresAt, CancellationToken token);
    Task<PersistenceWriteResult> FailAsync(OrderWorkItemId id, string owner, string errorCode, DateTimeOffset failedAt, CancellationToken token);
}

public static class PaperExecutionRecoveryCodes
{
    public const string ExpiredLease = "paper_execution.recovery.expired_lease";
    public const string SubmissionOutcomeUnknown = "paper_execution.recovery.submission_unknown";
}

public sealed record PaperExecutionRecoveryRequest(
    DateTimeOffset RecoveredAt,
    IReadOnlyList<OrderTransitionId> TransitionIds,
    IReadOnlyList<OrderWorkItemId> ReconciliationWorkItemIds);

public sealed record PaperExecutionRecoveryScope(
    BrokerAccountId BrokerAccountId,
    PortfolioId PortfolioId,
    OrderId OrderId);

public sealed record PaperExecutionRecoveryResult(
    int SubmissionClaimsConverted,
    int OutboxClaimsReleased,
    int InboxClaimsReleased,
    int FailedOutboxItems,
    int FailedInboxItems,
    IReadOnlyList<PaperExecutionRecoveryScope> Scopes);

public interface IPaperExecutionRecoveryRepository
{
    Task<PaperExecutionRecoveryResult> RecoverAsync(
        PaperExecutionRecoveryRequest request, CancellationToken token);
}

public sealed record SubmitOrderAuthorization(
    string OrderId, string ClientOrderId, string ProposalId, int ProposalContentVersion,
    string ProposalContentHash, string ConfigurationVersionId, string EvaluationId, string EvaluationHash,
    string SnapshotId, string SnapshotHash, string ApprovalId, string ReservationId,
    string BrokerAccountId, string BrokerConnectionId, string InstrumentMappingId, string InstrumentId,
    string Environment, string Side, string Quantity, string QuantityUnit, string Currency,
    string OrderType, string? LimitPrice, string TimeInForce, string CorrelationId);

public static class OrderSubmissionCodes
{
    public const string Ready = "order_submission.ready";
    public const string AlreadyCompleted = "order_submission.already_completed";
    public const string InvalidWork = "order_submission.invalid_work";
    public const string OrderState = "order_submission.order_state";
    public const string AccountRestricted = "order_submission.account_restricted";
    public const string AccountUnreconciled = "order_submission.account_unreconciled";
    public const string ConnectionDisabled = "order_submission.connection_disabled";
    public const string EnvironmentMismatch = "order_submission.environment_mismatch";
    public const string InstrumentMappingUnavailable = "order_submission.instrument_mapping_unavailable";
    public const string CapabilityUnavailable = "order_submission.capability_unavailable";
    public const string AuthorizationMismatch = "order_submission.authorization_mismatch";
    public const string Persisted = "order_submission.persisted";
    public const string Contention = "order_submission.contention";
}

public sealed record PreparedOrderSubmission(
    OrderWorkItemId WorkItemId, OrderId OrderId, BrokerAccountId BrokerAccountId,
    BrokerConnectionId BrokerConnectionId, string EnvironmentName, CorrelationIdentity CorrelationId,
    BrokerOrderRequest Request, string CommandHash, string AdapterIdentity, string LeaseOwner,
    long ExpectedOrderVersion);

public abstract record PrepareOrderSubmissionResult
{
    private PrepareOrderSubmissionResult() { }
    public sealed record Ready(PreparedOrderSubmission Value) : PrepareOrderSubmissionResult;
    public sealed record AlreadyCompleted(string Code) : PrepareOrderSubmissionResult;
    public sealed record Rejected(string Code) : PrepareOrderSubmissionResult;
    public sealed record Contention : PrepareOrderSubmissionResult;
}

public sealed record CompleteOrderSubmissionCommand(
    PreparedOrderSubmission Submission, BrokerSubmissionResult Result, DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt, string DiagnosticCode, IReadOnlyList<OrderTransitionId> TransitionIds);

public interface IOrderSubmissionRepository
{
    Task<PrepareOrderSubmissionResult> PrepareAsync(OrderWorkEnvelope work, DateTimeOffset at,
        BrokerCapabilities gatewayCapabilities, CancellationToken token);
    Task<PersistenceWriteResult> CompleteAsync(CompleteOrderSubmissionCommand command, CancellationToken token);
}

public static class OrderReconciliationCodes
{
    public const string Found = "order_reconciliation.found";
    public const string AbsentPending = "order_reconciliation.absent_pending";
    public const string AbsenceConfirmed = "order_reconciliation.absence_confirmed";
    public const string Uncertain = "order_reconciliation.uncertain";
    public const string Unavailable = "order_reconciliation.unavailable";
    public const string IdentityMismatch = "order_reconciliation.identity_mismatch";
    public const string AttemptsExhausted = "order_reconciliation.attempts_exhausted";
    public const string InvalidWork = "order_reconciliation.invalid_work";
    public const string Contention = "order_reconciliation.contention";
}

public sealed record PreparedOrderReconciliation(OrderWorkItemId WorkItemId, OrderId OrderId,
    BrokerAccountId BrokerAccountId, BrokerConnectionId BrokerConnectionId, string EnvironmentName,
    ClientOrderIdentity ClientOrderId, CorrelationIdentity CorrelationId, string LeaseOwner,
    long ExpectedOrderVersion, int Attempt, DateTimeOffset UnknownSince);

public abstract record PrepareOrderReconciliationResult
{
    private PrepareOrderReconciliationResult() { }
    public sealed record Ready(PreparedOrderReconciliation Value) : PrepareOrderReconciliationResult;
    public sealed record AlreadyCompleted(string Code) : PrepareOrderReconciliationResult;
    public sealed record Rejected(string Code) : PrepareOrderReconciliationResult;
    public sealed record Contention : PrepareOrderReconciliationResult;
}

public sealed record CompleteOrderReconciliationCommand(PreparedOrderReconciliation Reconciliation,
    BrokerReconciliationResult Result, string ResolutionCode, DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt, OrderTransitionId TransitionId);

public interface IOrderReconciliationRepository
{
    Task<PrepareOrderReconciliationResult> PrepareAsync(OrderWorkEnvelope work,
        BrokerCapabilities gatewayCapabilities, CancellationToken token);
    Task<PersistenceWriteResult> CompleteAsync(CompleteOrderReconciliationCommand command,
        CancellationToken token);
}
public interface IBrokerInboxRepository
{
    Task<PersistenceWriteResult> ReceiveAsync(BrokerInboxEnvelope value, CancellationToken token);
    Task<IReadOnlyList<BrokerInboxEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease, CancellationToken token);
    Task<PersistenceWriteResult> CompleteAsync(BrokerMessageId id, string owner, string result, DateTimeOffset at, CancellationToken token);
    Task<PersistenceWriteResult> RetryAsync(BrokerMessageId id, string owner, string errorCode, DateTimeOffset availableAt, CancellationToken token);
    Task<PersistenceWriteResult> RenewAsync(BrokerMessageId id, string owner, DateTimeOffset expiresAt, CancellationToken token);
    Task<PersistenceWriteResult> FailAsync(BrokerMessageId id, string owner, string errorCode, DateTimeOffset failedAt, CancellationToken token);
}

public sealed record ApplyBrokerOrderEventCommand(
    BrokerInboxEnvelope Message,
    string LeaseOwner,
    BrokerAccountId BrokerAccountId,
    string Environment,
    ClientOrderIdentity ClientOrderId,
    string? BrokerOrderId,
    BrokerOrderEventKind Kind,
    string Code,
    DateTimeOffset OccurredAt,
    DateTimeOffset ProcessedAt);

public enum BrokerOrderEventWriteDisposition { Applied, Duplicate, Deferred, Reconcile, Rejected, Contention }
public sealed record BrokerOrderEventWriteResult(BrokerOrderEventWriteDisposition Disposition, string Code);

public interface IBrokerOrderEventRepository
{
    Task<BrokerOrderEventWriteResult> ApplyAsync(ApplyBrokerOrderEventCommand command, CancellationToken token);
}

public sealed record ApplyFillAccountingCommand(
    BrokerInboxEnvelope Message,
    string LeaseOwner,
    BrokerAccountId BrokerAccountId,
    string Environment,
    ClientOrderIdentity ClientOrderId,
    string BrokerOrderId,
    BrokerExecution Execution,
    DateTimeOffset ProcessedAt);

public enum FillAccountingWriteDisposition { Applied, Duplicate, Rejected, Deferred, Contention }
public sealed record FillAccountingWriteResult(FillAccountingWriteDisposition Disposition, string Code);

public interface IFillAccountingRepository
{
    Task<FillAccountingWriteResult> ApplyAsync(ApplyFillAccountingCommand command, CancellationToken token);
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
