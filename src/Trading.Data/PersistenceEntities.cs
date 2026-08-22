using Trading.Core.Orders;

namespace Trading.Data;

internal abstract class PersistenceEntity { public string Id { get; set; } = string.Empty; }
internal sealed class KillSwitchEntity
{
    public string ScopeKind { get; set; } = string.Empty;
    public string ScopeId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string Confirmation { get; set; } = string.Empty;
    public long ChangedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class KillSwitchHistoryEntity : PersistenceEntity
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ScopeKind { get; set; } = string.Empty;
    public string ScopeId { get; set; } = string.Empty;
    public string PriorState { get; set; } = string.Empty;
    public string ResultingState { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string Confirmation { get; set; } = string.Empty;
    public long ChangedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class BrokerConnectionEntity : PersistenceEntity
{
    public string BrokerType { get; set; } = string.Empty; public string DisplayName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty; public string CredentialReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; public string CapabilitiesJson { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class BrokerAccountEntity : PersistenceEntity
{
    public string BrokerConnectionId { get; set; } = string.Empty; public string ExternalAccountId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty; public string AccountType { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = string.Empty; public string Status { get; set; } = string.Empty;
    public long? LastReconciledAt { get; set; }
    public string CapabilitiesJson { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class InstrumentEntity : PersistenceEntity
{
    public string InstrumentType { get; set; } = string.Empty; public string PrimarySymbol { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty; public string Currency { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty; public int PricePrecision { get; set; }
    public int QuantityPrecision { get; set; }
    public string Status { get; set; } = string.Empty; public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class InstrumentBrokerMappingEntity : PersistenceEntity
{
    public string InstrumentId { get; set; } = string.Empty; public string BrokerConnectionId { get; set; } = string.Empty;
    public string ExternalInstrumentId { get; set; } = string.Empty; public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty; public long EffectiveFrom { get; set; }
    public long? EffectiveTo { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
internal sealed class TradingBotEntity : PersistenceEntity
{
    public string Name { get; set; } = string.Empty; public string Status { get; set; } = string.Empty;
    public string? ActiveConfigurationVersionId { get; set; }
    public long? RequestedNextRunAt { get; set; }
    public long? AcceptedNextRunAt { get; set; }
    public string? LastCompletedRunId { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class TradingBotConfigurationVersionEntity : PersistenceEntity
{
    public string TradingBotId { get; set; } = string.Empty; public int VersionNumber { get; set; }
    public string InvestmentMandateJson { get; set; } = string.Empty; public string RiskPolicyJson { get; set; } = string.Empty;
    public string ToolPolicyJson { get; set; } = string.Empty; public string RunBudgetJson { get; set; } = string.Empty;
    public string SchedulingPolicyJson { get; set; } = string.Empty; public string ExecutionMode { get; set; } = string.Empty;
    public string ModelConfigurationJson { get; set; } = string.Empty; public string PromptVersion { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty; public long CreatedAt { get; set; }
    public long? ActivatedAt { get; set; }
    public long? SupersededAt { get; set; }
}
internal sealed class BotRunTriggerEntity : PersistenceEntity
{
    public string TradingBotId { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public long OccurredAt { get; set; }
    public string? ConsumedByRunId { get; set; }
    public long CreatedAt { get; set; }
}
internal sealed class BotRunEntity : PersistenceEntity
{
    public string TradingBotId { get; set; } = string.Empty;
    public string ConfigurationVersionId { get; set; } = string.Empty;
    public string? PortfolioSnapshotId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LeaseOwner { get; set; }
    public long? LeaseExpiresAt { get; set; }
    public long StartedAt { get; set; }
    public long? CompletedAt { get; set; }
    public string? FinishStatus { get; set; }
    public string? FinishSummary { get; set; }
    public long? RequestedNextRunAt { get; set; }
    public string? RequestedWakeReason { get; set; }
    public long? AcceptedNextRunAt { get; set; }
    public string? TerminalReason { get; set; }
    public string UsageJson { get; set; } = string.Empty;
    public int ModelTranscriptSchemaVersion { get; set; }
    public string ModelTranscriptJson { get; set; } = string.Empty;
    public string InputRenderingVersion { get; set; } = string.Empty;
    public string? InputRenderingHash { get; set; }
    public long Version { get; set; }
}
internal sealed class BotToolInvocationEntity : PersistenceEntity
{
    public string BotRunId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public int ToolSchemaVersion { get; set; }
    public string ArgumentsJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long StartedAt { get; set; }
    public long? CompletedAt { get; set; }
    public string? ResultJson { get; set; }
    public string? ResultArtifactId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDetail { get; set; }
    public string? UsageJson { get; set; }
}
internal sealed class PortfolioEntity : PersistenceEntity
{
    public string Name { get; set; } = string.Empty; public string BaseCurrency { get; set; } = string.Empty;
    public string? BrokerAccountId { get; set; }
    public string? AssignedTradingBotId { get; set; }
    public string Status { get; set; } = string.Empty; public string CapitalAllocationAmount { get; set; } = string.Empty;
    public string CashReservePolicyJson { get; set; } = string.Empty; public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class PositionEntity : PersistenceEntity
{
    public string PortfolioId { get; set; } = string.Empty; public string InstrumentId { get; set; } = string.Empty;
    public string QuantityUnit { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty; public string AverageCostAmount { get; set; } = string.Empty;
    public string AverageCostCurrency { get; set; } = string.Empty; public string RealizedPnlAmount { get; set; } = string.Empty;
    public string RealizedPnlCurrency { get; set; } = string.Empty; public long OpenedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long? ClosedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class PositionAppliedFillEntity
{
    public string PositionId { get; set; } = string.Empty; public string FillId { get; set; } = string.Empty; public long AppliedAt { get; set; }
}
internal sealed class PortfolioLedgerEntryEntity : PersistenceEntity
{
    public string PortfolioId { get; set; } = string.Empty; public string EntryType { get; set; } = string.Empty;
    public string? Amount { get; set; }
    public string? Currency { get; set; }
    public string? InstrumentId { get; set; }
    public string? Quantity { get; set; }
    public long EffectiveAt { get; set; }
    public long RecordedAt { get; set; }
    public string SourceType { get; set; } = string.Empty; public string SourceId { get; set; } = string.Empty;
    public string? ReversesEntryId { get; set; }
    public string? Description { get; set; }
    public string? MetadataJson { get; set; }
}
internal sealed class PortfolioDecisionSnapshotEntity : PersistenceEntity
{
    public string PortfolioId { get; set; } = string.Empty; public string TradingBotId { get; set; } = string.Empty;
    public string ConfigurationVersionId { get; set; } = string.Empty; public long AsOf { get; set; }
    public string ReconciliationStatus { get; set; } = string.Empty; public string DataFreshnessJson { get; set; } = string.Empty;
    public int SnapshotSchemaVersion { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty; public long CreatedAt { get; set; }
}
internal sealed class ResearchRequestEntity : PersistenceEntity
{
    public string SubjectType { get; set; } = string.Empty; public string? SubjectId { get; set; }
    public string Question { get; set; } = string.Empty; public string NormalizedResearchKey { get; set; } = string.Empty;
    public long AsOf { get; set; }
    public string Status { get; set; } = string.Empty; public string Visibility { get; set; } = string.Empty;
    public string? RequestingBotId { get; set; }
    public string FreshnessRequirementJson { get; set; } = string.Empty;
    public string RequestJson { get; set; } = string.Empty; public long? StartedAt { get; set; }
    public long? CompletedAt { get; set; }
    public string? ResultReportId { get; set; }
    public long CreatedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class ResearchSubscriptionEntity : PersistenceEntity
{
    public string ResearchRequestId { get; set; } = string.Empty; public string TradingBotId { get; set; } = string.Empty;
    public long SubscribedAt { get; set; }
    public string NotificationStatus { get; set; } = string.Empty; public long? NotifiedAt { get; set; }
}
internal sealed class ResearchRunEntity : PersistenceEntity
{
    public string ResearchRequestId { get; set; } = string.Empty; public int AttemptNumber { get; set; }
    public string Status { get; set; } = string.Empty; public string ModelConfigurationJson { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty; public string ToolSetVersion { get; set; } = string.Empty;
    public string ReportSchemaVersion { get; set; } = string.Empty; public long StartedAt { get; set; }
    public long? CompletedAt { get; set; }
    public string? TerminalReason { get; set; }
    public string UsageJson { get; set; } = string.Empty; public long Version { get; set; }
}
internal sealed class ResearchToolInvocationEntity : PersistenceEntity
{
    public string ResearchRunId { get; set; } = string.Empty; public int SequenceNumber { get; set; }
    public string ToolName { get; set; } = string.Empty; public int ToolSchemaVersion { get; set; }
    public string ArgumentsJson { get; set; } = string.Empty; public string Status { get; set; } = string.Empty;
    public long StartedAt { get; set; }
    public long? CompletedAt { get; set; }
    public string? ResultJson { get; set; }
    public string? ResultArtifactId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDetail { get; set; }
    public string? UsageJson { get; set; }
}
internal sealed class ResearchReportEntity : PersistenceEntity
{
    public string ReportSeriesId { get; set; } = string.Empty; public int VersionNumber { get; set; }
    public string ResearchRequestId { get; set; } = string.Empty; public string ResearchRunId { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty; public string? SubjectId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty; public long DataCutoff { get; set; }
    public long GeneratedAt { get; set; }
    public long? ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty; public string? SupersedesReportId { get; set; }
    public string ReportSchemaVersion { get; set; } = string.Empty; public string ContentJson { get; set; } = string.Empty;
    public string? ContentMarkdown { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string GeneratorMetadataJson { get; set; } = string.Empty;
}
internal sealed class ResearchReportSourceEntity : PersistenceEntity
{
    public string ResearchReportId { get; set; } = string.Empty; public int SourceSequence { get; set; }
    public string SourceType { get; set; } = string.Empty; public string? SourceUri { get; set; }
    public string? StableSourceId { get; set; }
    public string Title { get; set; } = string.Empty; public string? Publisher { get; set; }
    public long? PublishedAt { get; set; }
    public long RetrievedAt { get; set; }
    public string ContentHash { get; set; } = string.Empty; public string MetadataJson { get; set; } = string.Empty;
}
internal sealed class HypothesisEntity : PersistenceEntity { public string Name { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public string? CurrentVersionId { get; set; } public long CreatedAt { get; set; } public long UpdatedAt { get; set; } public long Version { get; set; } }
internal sealed class HypothesisVersionEntity : PersistenceEntity { public string HypothesisId { get; set; } = string.Empty; public int VersionNumber { get; set; } public int SpecificationSchemaVersion { get; set; } public string SpecificationJson { get; set; } = string.Empty; public string ContentHash { get; set; } = string.Empty; public long CreatedAt { get; set; } public long? FrozenAt { get; set; } }
internal sealed class HypothesisEvidenceReportEntity { public string HypothesisVersionId { get; set; } = string.Empty; public string ResearchReportId { get; set; } = string.Empty; public string RelationshipType { get; set; } = string.Empty; }
internal sealed class HypothesisTestResultEntity : PersistenceEntity { public string HypothesisVersionId { get; set; } = string.Empty; public string DatasetVersion { get; set; } = string.Empty; public string CodeVersion { get; set; } = string.Empty; public string ParametersHash { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public long StartedAt { get; set; } public long? CompletedAt { get; set; } public string MetricsJson { get; set; } = string.Empty; public string ArtifactsJson { get; set; } = string.Empty; public string ResultHash { get; set; } = string.Empty; }
internal sealed class TradeProposalEntity : PersistenceEntity { public string TradingBotId { get; set; } = string.Empty; public string BotRunId { get; set; } = string.Empty; public string PortfolioId { get; set; } = string.Empty; public string PortfolioSnapshotId { get; set; } = string.Empty; public string ConfigurationVersionId { get; set; } = string.Empty; public string InstrumentId { get; set; } = string.Empty; public string ProposalType { get; set; } = string.Empty; public string RequestedActionJson { get; set; } = string.Empty; public string Rationale { get; set; } = string.Empty; public string? HypothesisVersionId { get; set; } public string Status { get; set; } = string.Empty; public long CreatedAt { get; set; } public long ValidUntil { get; set; } public string IdempotencyKey { get; set; } = string.Empty; public long Version { get; set; } }
internal sealed class TradeProposalEvidenceReportEntity { public string TradeProposalId { get; set; } = string.Empty; public string ResearchReportId { get; set; } = string.Empty; }
internal sealed class GuardrailEvaluationEntity : PersistenceEntity { public string TradeProposalId { get; set; } = string.Empty; public int EvaluationSequence { get; set; } public string EvaluationStage { get; set; } = string.Empty; public string PolicyVersion { get; set; } = string.Empty; public string Outcome { get; set; } = string.Empty; public string StateSnapshotId { get; set; } = string.Empty; public string RuleResultsJson { get; set; } = string.Empty; public string ContentHash { get; set; } = string.Empty; public long EvaluatedAt { get; set; } }
internal sealed class ProposalApprovalEntity : PersistenceEntity { public string TradeProposalId { get; set; } = string.Empty; public string Decision { get; set; } = string.Empty; public string ActorType { get; set; } = string.Empty; public string ActorId { get; set; } = string.Empty; public string? Reason { get; set; } public long DecidedAt { get; set; } public long ProposalVersion { get; set; } public string StateSnapshotId { get; set; } = string.Empty; }
internal sealed class CapitalReservationEntity : PersistenceEntity { public string PortfolioId { get; set; } = string.Empty; public string TradeProposalId { get; set; } = string.Empty; public string? OrderId { get; set; } public string Amount { get; set; } = string.Empty; public string Currency { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public long CreatedAt { get; set; } public long ExpiresAt { get; set; } public long? ConsumedAt { get; set; } public long? ReleasedAt { get; set; } public long Version { get; set; } }
internal sealed class OrderEntity : PersistenceEntity
{
    public string ClientOrderId { get; set; } = string.Empty; public string PortfolioId { get; set; } = string.Empty;
    public string BrokerAccountId { get; set; } = string.Empty; public string TradeProposalId { get; set; } = string.Empty;
    public string? CapitalReservationId { get; set; }
    public string InstrumentId { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty; public string Quantity { get; set; } = string.Empty;
    public string QuantityUnit { get; set; } = string.Empty; public string Currency { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty; public string? LimitPrice { get; set; }
    public TimeInForce TimeInForce { get; set; }
    public OrderStatus Status { get; set; }
    public string? BrokerOrderId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public long? SubmittedAt { get; set; }
    public long? CompletedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class OrderTransitionEntity : PersistenceEntity
{
    public string OrderId { get; set; } = string.Empty; public int SequenceNumber { get; set; }
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public string ReasonCode { get; set; } = string.Empty; public string? ReasonDetail { get; set; }
    public string Source { get; set; } = string.Empty; public long OccurredAt { get; set; }
    public long ReceivedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
internal sealed class FillEntity : PersistenceEntity
{
    public string OrderId { get; set; } = string.Empty; public string BrokerAccountId { get; set; } = string.Empty;
    public string BrokerExecutionId { get; set; } = string.Empty; public string Quantity { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty; public string Currency { get; set; } = string.Empty;
    public string FeeAmount { get; set; } = string.Empty; public string FeeCurrency { get; set; } = string.Empty;
    public long ExecutedAt { get; set; }
    public long ReceivedAt { get; set; }
    public string? RawPayloadReference { get; set; }
}
internal sealed class BrokerSubmissionAttemptEntity : PersistenceEntity
{
    public string OrderId { get; set; } = string.Empty; public string WorkItemId { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public string ClientOrderId { get; set; } = string.Empty;
    public string CommandHash { get; set; } = string.Empty; public string AdapterIdentity { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty; public long StartedAt { get; set; }
    public long CompletedAt { get; set; }
    public string Outcome { get; set; } = string.Empty; public string ResultCode { get; set; } = string.Empty;
    public string? BrokerOrderId { get; set; }
    public string DiagnosticCode { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}
internal sealed class BrokerReconciliationEntity : PersistenceEntity
{
    public string BrokerAccountId { get; set; } = string.Empty; public string Status { get; set; } = string.Empty;
    public long StartedAt { get; set; }
    public long? CompletedAt { get; set; }
    public string BrokerSnapshotJson { get; set; } = string.Empty; public string DifferencesJson { get; set; } = string.Empty;
    public string ResolutionJson { get; set; } = string.Empty; public string CorrelationId { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}
internal sealed class OutboxMessageEntity : PersistenceEntity
{
    public string OrderId { get; set; } = string.Empty; public string WorkKind { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty; public string PayloadJson { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty; public string CorrelationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long AvailableAt { get; set; }
    public long CreatedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LeaseOwner { get; set; }
    public long? LeaseExpiresAt { get; set; }
    public string? LastError { get; set; }
    public long? CompletedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class InboxMessageEntity : PersistenceEntity
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty; public long ReceivedAt { get; set; }
    public long AvailableAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty; public string PayloadHash { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string? LeaseOwner { get; set; }
    public long? LeaseExpiresAt { get; set; }
    public string? LastError { get; set; }
    public long? CompletedAt { get; set; }
    public long Version { get; set; }
}
internal sealed class SchemaMetadataEntity
{
    public string Key { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; public long UpdatedAt { get; set; }
}
