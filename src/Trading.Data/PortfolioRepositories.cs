using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Portfolios;

namespace Trading.Data;

public sealed class PortfolioRepository(TradingDbContext dbContext) : IPortfolioRepository
{
    public async Task<Portfolio?> GetAsync(PortfolioId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await dbContext.Portfolios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.ToString(), cancellationToken).ConfigureAwait(false);
        return entity is null ? null : PortfolioMapper.ToDomain(entity);
    }

    public async Task<PersistenceWriteResult> AddAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        dbContext.Portfolios.Add(PortfolioMapper.ToEntity(portfolio));
        return await RepositoryWrites.SaveAsync(dbContext, "active_portfolio_ownership", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistenceWriteResult> UpdateAsync(Portfolio portfolio, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        var entity = await dbContext.Portfolios.SingleOrDefaultAsync(x => x.Id == portfolio.Id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion) return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        PortfolioMapper.Copy(portfolio, entity); entity.Version = expectedVersion + 1;
        return await RepositoryWrites.SaveAsync(dbContext, "active_portfolio_ownership", cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PositionRepository(TradingDbContext dbContext) : IPositionRepository
{
    public Task<Position?> GetAsync(PositionId id, CancellationToken cancellationToken) => LoadAsync(x => x.Id == id.ToString(), cancellationToken);
    public Task<Position?> GetForPortfolioInstrumentAsync(PortfolioId portfolioId, InstrumentId instrumentId, CancellationToken cancellationToken) =>
        LoadAsync(x => x.PortfolioId == portfolioId.ToString() && x.InstrumentId == instrumentId.ToString(), cancellationToken);

    private async Task<Position?> LoadAsync(System.Linq.Expressions.Expression<Func<PositionEntity, bool>> predicate, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Positions.AsNoTracking().SingleOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        var fills = await dbContext.PositionAppliedFills.AsNoTracking().Where(x => x.PositionId == entity.Id).Select(x => x.FillId).ToListAsync(cancellationToken).ConfigureAwait(false);
        return PositionMapper.ToDomain(entity, fills);
    }

    public async Task<PersistenceWriteResult> AddAsync(Position position, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(position);
        dbContext.Positions.Add(PositionMapper.ToEntity(position));
        dbContext.PositionAppliedFills.AddRange(position.AppliedSources.Select(x => new PositionAppliedFillEntity { PositionId = position.Id.ToString(), FillId = x, AppliedAt = UtcUnixMilliseconds.ToProvider(position.UpdatedAt) }));
        return await RepositoryWrites.SaveAsync(dbContext, "portfolio_instrument_or_applied_fill", cancellationToken).ConfigureAwait(false);
    }

    public async Task<PersistenceWriteResult> UpdateAsync(Position position, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(position);
        var entity = await dbContext.Positions.SingleOrDefaultAsync(x => x.Id == position.Id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion) return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        PositionMapper.Copy(position, entity);
        var stored = await dbContext.PositionAppliedFills.Where(x => x.PositionId == entity.Id).Select(x => x.FillId).ToListAsync(cancellationToken).ConfigureAwait(false);
        dbContext.PositionAppliedFills.AddRange(position.AppliedSources.Except(stored, StringComparer.Ordinal).Select(x => new PositionAppliedFillEntity { PositionId = entity.Id, FillId = x, AppliedAt = UtcUnixMilliseconds.ToProvider(position.UpdatedAt) }));
        return await RepositoryWrites.SaveAsync(dbContext, "portfolio_instrument_or_applied_fill", cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PortfolioLedgerRepository(TradingDbContext dbContext) : IPortfolioLedgerRepository
{
    public async Task<PortfolioLedgerEntry?> GetAsync(PortfolioLedgerEntryId id, CancellationToken cancellationToken)
    { var entity = await dbContext.PortfolioLedgerEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.ToString(), cancellationToken).ConfigureAwait(false); return entity is null ? null : PortfolioLedgerMapper.ToDomain(entity); }

    public async Task<PersistenceWriteResult> AppendAsync(PortfolioLedgerEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var exists = await dbContext.PortfolioLedgerEntries.AsNoTracking().AnyAsync(x => x.PortfolioId == entry.PortfolioId.ToString() && x.SourceType == CanonicalEnumeration.Format(entry.SourceType) && x.SourceId == entry.SourceId, cancellationToken).ConfigureAwait(false);
        if (exists) return new PersistenceWriteResult.Succeeded();
        if (entry.ReversesEntryId is not null)
        {
            var original = await GetAsync(entry.ReversesEntryId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The correction target does not exist.");
            if (original.PortfolioId != entry.PortfolioId || original.Amount.Amount + entry.Amount.Amount != 0 || original.Currency != entry.Currency) throw new InvalidOperationException("A correction must compensate its original entry in the same portfolio and currency.");
        }
        dbContext.PortfolioLedgerEntries.Add(PortfolioLedgerMapper.ToEntity(entry));
        try { await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); return new PersistenceWriteResult.Succeeded(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
        { dbContext.ChangeTracker.Clear(); return new PersistenceWriteResult.Succeeded(); }
    }
}

internal static class PortfolioMapper
{
    private sealed record ReservePolicy(decimal Percentage);
    public static PortfolioEntity ToEntity(Portfolio value) { var entity = new PortfolioEntity(); Copy(value, entity); return entity; }
    public static void Copy(Portfolio value, PortfolioEntity entity)
    { entity.Id = value.Id.ToString(); entity.Name = value.Name; entity.BaseCurrency = value.BaseCurrency.Code; entity.BrokerAccountId = value.BrokerAccountId?.ToString(); entity.AssignedTradingBotId = value.AssignedTradingBotId?.ToString(); entity.Status = CanonicalEnumeration.Format(value.Status); entity.CapitalAllocationAmount = ExactDecimalText.ToProvider(value.CapitalAllocation.Amount); entity.CashReservePolicyJson = CanonicalJsonSerializer.Serialize(1, new ReservePolicy(value.CashReservePercentage)); entity.CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt); entity.UpdatedAt = UtcUnixMilliseconds.ToProvider(value.UpdatedAt); entity.Version = value.Version; }
    public static Portfolio ToDomain(PortfolioEntity value) { var currency = new Currency(value.BaseCurrency); return Portfolio.Rehydrate(PortfolioId.Parse(value.Id), value.Name, currency, value.BrokerAccountId is null ? null : BrokerAccountId.Parse(value.BrokerAccountId), value.AssignedTradingBotId is null ? null : TradingBotId.Parse(value.AssignedTradingBotId), CanonicalEnumeration.Parse<PortfolioStatus>(value.Status), new Money(ExactDecimalText.FromProvider(value.CapitalAllocationAmount), currency), CanonicalJsonSerializer.Deserialize<ReservePolicy>(1, value.CashReservePolicyJson).Percentage, UtcUnixMilliseconds.FromProvider(value.CreatedAt), UtcUnixMilliseconds.FromProvider(value.UpdatedAt), value.Version, ExactDecimalText.FromProvider(value.CapitalAllocationAmount) != 0); }
}

internal static class PositionMapper
{
    public static PositionEntity ToEntity(Position value) { var entity = new PositionEntity(); Copy(value, entity); return entity; }
    public static void Copy(Position value, PositionEntity entity) { entity.Id = value.Id.ToString(); entity.PortfolioId = value.PortfolioId.ToString(); entity.InstrumentId = value.InstrumentId.ToString(); entity.QuantityUnit = value.QuantityUnit; entity.Quantity = ExactDecimalText.ToProvider(value.Quantity); entity.AverageCostAmount = ExactDecimalText.ToProvider(value.AverageCost.Amount); entity.AverageCostCurrency = value.AverageCost.Currency.Code; entity.RealizedPnlAmount = ExactDecimalText.ToProvider(value.RealizedProfitLoss.Amount); entity.RealizedPnlCurrency = value.RealizedProfitLoss.Currency.Code; entity.OpenedAt = UtcUnixMilliseconds.ToProvider(value.OpenedAt); entity.UpdatedAt = UtcUnixMilliseconds.ToProvider(value.UpdatedAt); entity.ClosedAt = value.ClosedAt is null ? null : UtcUnixMilliseconds.ToProvider(value.ClosedAt.Value); entity.Version = value.Version; }
    public static Position ToDomain(PositionEntity value, IEnumerable<string> fills) => Position.Rehydrate(PositionId.Parse(value.Id), PortfolioId.Parse(value.PortfolioId), InstrumentId.Parse(value.InstrumentId), value.QuantityUnit, ExactDecimalText.FromProvider(value.Quantity), new Money(ExactDecimalText.FromProvider(value.AverageCostAmount), new Currency(value.AverageCostCurrency)), new Money(ExactDecimalText.FromProvider(value.RealizedPnlAmount), new Currency(value.RealizedPnlCurrency)), value.Version, UtcUnixMilliseconds.FromProvider(value.OpenedAt), UtcUnixMilliseconds.FromProvider(value.UpdatedAt), value.ClosedAt is null ? null : UtcUnixMilliseconds.FromProvider(value.ClosedAt.Value), fills);
}

internal static class PortfolioLedgerMapper
{
    public static PortfolioLedgerEntryEntity ToEntity(PortfolioLedgerEntry value) => new() { Id = value.Id.ToString(), PortfolioId = value.PortfolioId.ToString(), EntryType = CanonicalEnumeration.Format(value.EntryType), Amount = ExactDecimalText.ToProvider(value.Amount.Amount), Currency = value.Currency.Code, InstrumentId = value.InstrumentId?.ToString(), Quantity = value.Quantity is null ? null : ExactDecimalText.ToProvider(value.Quantity.Value), EffectiveAt = UtcUnixMilliseconds.ToProvider(value.EffectiveAt), RecordedAt = UtcUnixMilliseconds.ToProvider(value.RecordedAt), SourceType = CanonicalEnumeration.Format(value.SourceType), SourceId = value.SourceId, ReversesEntryId = value.ReversesEntryId?.ToString(), Description = value.Description, MetadataJson = value.MetadataJson };
    public static PortfolioLedgerEntry ToDomain(PortfolioLedgerEntryEntity value) => new(PortfolioLedgerEntryId.Parse(value.Id), PortfolioId.Parse(value.PortfolioId), CanonicalEnumeration.Parse<PortfolioLedgerEntryType>(value.EntryType), new Money(ExactDecimalText.FromProvider(value.Amount!), new Currency(value.Currency!)), value.InstrumentId is null ? null : InstrumentId.Parse(value.InstrumentId), value.Quantity is null ? null : ExactDecimalText.FromProvider(value.Quantity), UtcUnixMilliseconds.FromProvider(value.EffectiveAt), CanonicalEnumeration.Parse<LedgerSourceType>(value.SourceType), value.SourceId, UtcUnixMilliseconds.FromProvider(value.RecordedAt), value.ReversesEntryId is null ? null : PortfolioLedgerEntryId.Parse(value.ReversesEntryId), value.Description, value.MetadataJson);
}
