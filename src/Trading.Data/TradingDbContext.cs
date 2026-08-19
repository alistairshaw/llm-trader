using Microsoft.EntityFrameworkCore;

namespace Trading.Data;

public sealed class TradingDbContext(DbContextOptions<TradingDbContext> options) : DbContext(options)
{
    internal DbSet<BrokerConnectionEntity> BrokerConnections => Set<BrokerConnectionEntity>();
    internal DbSet<BrokerAccountEntity> BrokerAccounts => Set<BrokerAccountEntity>();
    internal DbSet<InstrumentEntity> Instruments => Set<InstrumentEntity>();
    internal DbSet<InstrumentBrokerMappingEntity> InstrumentBrokerMappings => Set<InstrumentBrokerMappingEntity>();
    internal DbSet<TradingBotEntity> TradingBots => Set<TradingBotEntity>();
    internal DbSet<TradingBotConfigurationVersionEntity> TradingBotConfigurationVersions => Set<TradingBotConfigurationVersionEntity>();
    internal DbSet<BotRunTriggerEntity> BotRunTriggers => Set<BotRunTriggerEntity>();
    internal DbSet<BotRunEntity> BotRuns => Set<BotRunEntity>();
    internal DbSet<BotToolInvocationEntity> BotToolInvocations => Set<BotToolInvocationEntity>();
    internal DbSet<PortfolioEntity> Portfolios => Set<PortfolioEntity>();
    internal DbSet<PositionEntity> Positions => Set<PositionEntity>();
    internal DbSet<PositionAppliedFillEntity> PositionAppliedFills => Set<PositionAppliedFillEntity>();
    internal DbSet<PortfolioLedgerEntryEntity> PortfolioLedgerEntries => Set<PortfolioLedgerEntryEntity>();
    internal DbSet<PortfolioDecisionSnapshotEntity> PortfolioDecisionSnapshots => Set<PortfolioDecisionSnapshotEntity>();
    internal DbSet<SchemaMetadataEntity> SchemaMetadata => Set<SchemaMetadataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradingDbContext).Assembly);
}
