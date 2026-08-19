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
    internal DbSet<PortfolioEntity> Portfolios => Set<PortfolioEntity>();
    internal DbSet<PositionEntity> Positions => Set<PositionEntity>();
    internal DbSet<PositionAppliedFillEntity> PositionAppliedFills => Set<PositionAppliedFillEntity>();
    internal DbSet<PortfolioLedgerEntryEntity> PortfolioLedgerEntries => Set<PortfolioLedgerEntryEntity>();
    internal DbSet<PortfolioDecisionSnapshotEntity> PortfolioDecisionSnapshots => Set<PortfolioDecisionSnapshotEntity>();
    internal DbSet<SchemaMetadataEntity> SchemaMetadata => Set<SchemaMetadataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            entityType.SetTableName(ToSnakeCase(entityType.ClrType.Name.Replace("Entity", string.Empty, StringComparison.Ordinal)));
            modelBuilder.Entity(entityType.ClrType).Property(nameof(PersistenceEntity.Id)).HasColumnName("id");
        }
    }

    private static string ToSnakeCase(string value) => string.Concat(value.Select((character, index) =>
        index > 0 && char.IsUpper(character) ? $"_{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));
}
