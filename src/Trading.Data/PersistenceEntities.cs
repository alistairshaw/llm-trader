namespace Trading.Data;

internal abstract class PersistenceEntity { public string Id { get; set; } = string.Empty; }
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
internal sealed class SchemaMetadataEntity
{
    public string Key { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; public long UpdatedAt { get; set; }
}
