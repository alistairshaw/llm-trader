namespace Trading.Data;

// Mapping is introduced by the aggregate persistence tasks. These types establish the complete
// Stage 2 EF model surface without allowing persistence details to cross the data boundary.
internal abstract class PersistenceEntity
{
    public string Id { get; set; } = string.Empty;
}

internal sealed class BrokerConnectionEntity : PersistenceEntity;
internal sealed class BrokerAccountEntity : PersistenceEntity;
internal sealed class InstrumentEntity : PersistenceEntity;
internal sealed class InstrumentBrokerMappingEntity : PersistenceEntity;
internal sealed class TradingBotEntity : PersistenceEntity;
internal sealed class TradingBotConfigurationVersionEntity : PersistenceEntity;
internal sealed class PortfolioEntity : PersistenceEntity;
internal sealed class PositionEntity : PersistenceEntity;
internal sealed class PositionAppliedFillEntity : PersistenceEntity;
internal sealed class PortfolioLedgerEntryEntity : PersistenceEntity;
internal sealed class PortfolioDecisionSnapshotEntity : PersistenceEntity;
internal sealed class SchemaMetadataEntity : PersistenceEntity;
