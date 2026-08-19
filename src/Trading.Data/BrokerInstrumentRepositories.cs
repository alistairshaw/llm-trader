using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Data;

public sealed class BrokerConnectionRepository(TradingDbContext dbContext) : IBrokerConnectionRepository
{
    public async Task<BrokerConnection?> GetAsync(BrokerConnectionId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await dbContext.BrokerConnections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id.ToString(), cancellationToken).ConfigureAwait(false);
        return entity is null ? null : BrokerInstrumentMapper.ToDomain(entity);
    }

    public Task<PersistenceWriteResult> AddAsync(BrokerConnection connection, CancellationToken cancellationToken) =>
        RepositoryWrites.AddAsync(dbContext, BrokerInstrumentMapper.ToEntity(connection), "broker_connection_id", cancellationToken);

    public async Task<PersistenceWriteResult> UpdateAsync(BrokerConnection connection, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var entity = await dbContext.BrokerConnections.SingleOrDefaultAsync(x => x.Id == connection.Id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion)
            return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        BrokerInstrumentMapper.Copy(connection, entity); entity.Version++;
        return await RepositoryWrites.SaveAsync(dbContext, "broker_connection_id", cancellationToken).ConfigureAwait(false);
    }
}

public sealed class BrokerAccountRepository(TradingDbContext dbContext) : IBrokerAccountRepository
{
    public async Task<BrokerAccount?> GetAsync(BrokerAccountId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await dbContext.BrokerAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id.ToString(), cancellationToken).ConfigureAwait(false);
        return entity is null ? null : BrokerInstrumentMapper.ToDomain(entity);
    }

    public Task<PersistenceWriteResult> AddAsync(BrokerAccount account, CancellationToken cancellationToken) =>
        RepositoryWrites.AddAsync(dbContext, BrokerInstrumentMapper.ToEntity(account), "broker_account_external_identity", cancellationToken);

    public async Task<PersistenceWriteResult> UpdateAsync(BrokerAccount account, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        var entity = await dbContext.BrokerAccounts.SingleOrDefaultAsync(x => x.Id == account.Id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion)
            return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        BrokerInstrumentMapper.Copy(account, entity); entity.Version++;
        return await RepositoryWrites.SaveAsync(dbContext, "broker_account_external_identity", cancellationToken).ConfigureAwait(false);
    }
}

public sealed class InstrumentRepository(TradingDbContext dbContext) : IInstrumentRepository
{
    public async Task<Instrument?> GetAsync(InstrumentId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await dbContext.Instruments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        var mappings = await dbContext.InstrumentBrokerMappings.AsNoTracking().Where(x => x.InstrumentId == entity.Id)
            .OrderBy(x => x.EffectiveFrom).ToListAsync(cancellationToken).ConfigureAwait(false);
        return BrokerInstrumentMapper.ToDomain(entity, mappings);
    }

    public async Task<PersistenceWriteResult> AddAsync(Instrument instrument, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var overlap = await HasExternalMappingOverlapAsync(instrument, null, cancellationToken).ConfigureAwait(false);
        if (overlap) return new PersistenceWriteResult.UniquenessConflict("instrument_mapping_effective_interval");
        dbContext.Instruments.Add(BrokerInstrumentMapper.ToEntity(instrument));
        dbContext.InstrumentBrokerMappings.AddRange(instrument.BrokerMappings.Select(x => BrokerInstrumentMapper.ToEntity(instrument.Id, x)));
        var result = await RepositoryWrites.SaveAsync(dbContext, "instrument_external_identity", cancellationToken).ConfigureAwait(false);
        if (result is PersistenceWriteResult.Succeeded) await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<PersistenceWriteResult> UpdateAsync(Instrument instrument, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var entity = await dbContext.Instruments.SingleOrDefaultAsync(x => x.Id == instrument.Id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion)
            return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        if (await HasExternalMappingOverlapAsync(instrument, instrument.Id.ToString(), cancellationToken).ConfigureAwait(false))
            return new PersistenceWriteResult.UniquenessConflict("instrument_mapping_effective_interval");
        BrokerInstrumentMapper.Copy(instrument, entity); entity.Version++;
        var existing = await dbContext.InstrumentBrokerMappings.Where(x => x.InstrumentId == entity.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        dbContext.InstrumentBrokerMappings.RemoveRange(existing);
        dbContext.InstrumentBrokerMappings.AddRange(instrument.BrokerMappings.Select(x => BrokerInstrumentMapper.ToEntity(instrument.Id, x)));
        var result = await RepositoryWrites.SaveAsync(dbContext, "instrument_external_identity", cancellationToken).ConfigureAwait(false);
        if (result is PersistenceWriteResult.Succeeded) await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<bool> HasExternalMappingOverlapAsync(Instrument instrument, string? excludedInstrumentId, CancellationToken cancellationToken)
    {
        foreach (var mapping in instrument.BrokerMappings)
        {
            var from = UtcUnixMilliseconds.ToProvider(mapping.EffectiveFrom);
            var to = mapping.EffectiveTo is null ? (long?)null : UtcUnixMilliseconds.ToProvider(mapping.EffectiveTo.Value);
            var connectionId = mapping.BrokerConnectionId.ToString();
            if (await dbContext.InstrumentBrokerMappings.AsNoTracking().AnyAsync(existing =>
                    existing.InstrumentId != excludedInstrumentId && existing.BrokerConnectionId == connectionId &&
                    existing.ExternalInstrumentId == mapping.ExternalInstrumentId &&
                    existing.EffectiveFrom < (to ?? long.MaxValue) && from < (existing.EffectiveTo ?? long.MaxValue), cancellationToken)
                .ConfigureAwait(false)) return true;
        }
        return false;
    }
}

internal static class RepositoryWrites
{
    public static async Task<PersistenceWriteResult> AddAsync<TEntity>(TradingDbContext context, TEntity entity,
        string constraint, CancellationToken cancellationToken) where TEntity : class
    {
        context.Add(entity);
        return await SaveAsync(context, constraint, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<PersistenceWriteResult> SaveAsync(TradingDbContext context, string constraint, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new PersistenceWriteResult.Succeeded();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException
               { SqliteExtendedErrorCode: 1555 or 2067 })
        {
            context.ChangeTracker.Clear();
            return new PersistenceWriteResult.UniquenessConflict(constraint);
        }
    }
}

internal static class BrokerInstrumentMapper
{
    private const int JsonSchemaVersion = 1;
    public static BrokerConnectionEntity ToEntity(BrokerConnection value) { var entity = new BrokerConnectionEntity(); Copy(value, entity); return entity; }
    public static void Copy(BrokerConnection value, BrokerConnectionEntity entity)
    { entity.Id = value.Id.ToString(); entity.BrokerType = value.BrokerType; entity.DisplayName = value.DisplayName; entity.Environment = CanonicalEnumeration.Format(value.Environment); entity.CredentialReference = value.CredentialReference; entity.Status = CanonicalEnumeration.Format(value.Status); entity.CapabilitiesJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, value.Capabilities); entity.CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt); entity.UpdatedAt = UtcUnixMilliseconds.ToProvider(value.UpdatedAt); entity.Version = value.Version; }
    public static BrokerConnection ToDomain(BrokerConnectionEntity value) => BrokerConnection.Rehydrate(BrokerConnectionId.Parse(value.Id), value.BrokerType, value.DisplayName, CanonicalEnumeration.Parse<BrokerEnvironment>(value.Environment), value.CredentialReference, CanonicalJsonSerializer.Deserialize<string[]>(JsonSchemaVersion, value.CapabilitiesJson), CanonicalEnumeration.Parse<BrokerConnectionStatus>(value.Status), UtcUnixMilliseconds.FromProvider(value.CreatedAt), UtcUnixMilliseconds.FromProvider(value.UpdatedAt), value.Version);

    public static BrokerAccountEntity ToEntity(BrokerAccount value) { var entity = new BrokerAccountEntity(); Copy(value, entity); return entity; }
    public static void Copy(BrokerAccount value, BrokerAccountEntity entity)
    { entity.Id = value.Id.ToString(); entity.BrokerConnectionId = value.BrokerConnectionId.ToString(); entity.ExternalAccountId = value.ExternalAccountId; entity.DisplayName = value.DisplayName; entity.AccountType = value.AccountType; entity.BaseCurrency = value.BaseCurrency.Code; entity.Status = CanonicalEnumeration.Format(value.Status); entity.LastReconciledAt = value.LastReconciledAt is null ? null : UtcUnixMilliseconds.ToProvider(value.LastReconciledAt.Value); entity.CapabilitiesJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, value.Capabilities); entity.CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt); entity.UpdatedAt = UtcUnixMilliseconds.ToProvider(value.UpdatedAt); entity.Version = value.Version; }
    public static BrokerAccount ToDomain(BrokerAccountEntity value) => BrokerAccount.Rehydrate(BrokerAccountId.Parse(value.Id), BrokerConnectionId.Parse(value.BrokerConnectionId), value.ExternalAccountId, value.DisplayName, value.AccountType, new Currency(value.BaseCurrency), CanonicalEnumeration.Parse<BrokerAccountStatus>(value.Status), value.LastReconciledAt is null ? null : UtcUnixMilliseconds.FromProvider(value.LastReconciledAt.Value), CanonicalJsonSerializer.Deserialize<string[]>(JsonSchemaVersion, value.CapabilitiesJson), UtcUnixMilliseconds.FromProvider(value.CreatedAt), UtcUnixMilliseconds.FromProvider(value.UpdatedAt), value.Version);

    public static InstrumentEntity ToEntity(Instrument value) { var entity = new InstrumentEntity(); Copy(value, entity); return entity; }
    public static void Copy(Instrument value, InstrumentEntity entity)
    { entity.Id = value.Id.ToString(); entity.InstrumentType = CanonicalEnumeration.Format(value.InstrumentType); entity.PrimarySymbol = value.PrimarySymbol; entity.DisplayName = value.DisplayName; entity.Currency = value.Currency.Code; entity.Exchange = value.Exchange; entity.PricePrecision = value.PricePrecision; entity.QuantityPrecision = value.QuantityPrecision; entity.Status = CanonicalEnumeration.Format(value.Status); entity.CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt); entity.UpdatedAt = UtcUnixMilliseconds.ToProvider(value.UpdatedAt); entity.Version = value.Version; }
    public static InstrumentBrokerMappingEntity ToEntity(InstrumentId instrumentId, InstrumentBrokerMapping value) => new() { Id = value.Id.ToString(), InstrumentId = instrumentId.ToString(), BrokerConnectionId = value.BrokerConnectionId.ToString(), ExternalInstrumentId = value.ExternalInstrumentId, Symbol = value.Symbol, Exchange = value.Exchange, EffectiveFrom = UtcUnixMilliseconds.ToProvider(value.EffectiveFrom), EffectiveTo = value.EffectiveTo is null ? null : UtcUnixMilliseconds.ToProvider(value.EffectiveTo.Value), MetadataJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, new Dictionary<string, string>()) };
    public static Instrument ToDomain(InstrumentEntity value, IEnumerable<InstrumentBrokerMappingEntity> mappings) => Instrument.Rehydrate(InstrumentId.Parse(value.Id), CanonicalEnumeration.Parse<InstrumentType>(value.InstrumentType), value.PrimarySymbol, value.DisplayName, new Currency(value.Currency), value.Exchange, value.PricePrecision, value.QuantityPrecision, CanonicalEnumeration.Parse<InstrumentStatus>(value.Status), UtcUnixMilliseconds.FromProvider(value.CreatedAt), UtcUnixMilliseconds.FromProvider(value.UpdatedAt), value.Version, mappings.Select(x => new InstrumentBrokerMappingState(InstrumentBrokerMappingId.Parse(x.Id), BrokerConnectionId.Parse(x.BrokerConnectionId), x.ExternalInstrumentId, x.Symbol, x.Exchange, UtcUnixMilliseconds.FromProvider(x.EffectiveFrom), x.EffectiveTo is null ? null : UtcUnixMilliseconds.FromProvider(x.EffectiveTo.Value))));
}
