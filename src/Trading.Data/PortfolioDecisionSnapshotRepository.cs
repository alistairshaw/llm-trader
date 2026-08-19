using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;

namespace Trading.Data;

public sealed class PortfolioDecisionSnapshotRepository(TradingDbContext dbContext) : IPortfolioDecisionSnapshotRepository
{
    public async Task<PortfolioDecisionSnapshot?> GetAsync(PortfolioDecisionSnapshotId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await dbContext.PortfolioDecisionSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id.ToString(), cancellationToken).ConfigureAwait(false);
        return entity is null ? null : PortfolioDecisionSnapshotMapper.ToDomain(entity);
    }

    public async Task<PersistenceWriteResult> PublishAsync(PortfolioDecisionSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var portfolio = await dbContext.Portfolios.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == snapshot.PortfolioId.ToString(), cancellationToken).ConfigureAwait(false);
        if (portfolio is null || portfolio.AssignedTradingBotId != snapshot.TradingBotId.ToString())
            throw new InvalidOperationException("The snapshot Trading Bot must own the Portfolio.");

        var configurationOwnedByBot = await dbContext.TradingBotConfigurationVersions.AsNoTracking().AnyAsync(
            x => x.Id == snapshot.ConfigurationVersionId.ToString() && x.TradingBotId == snapshot.TradingBotId.ToString(), cancellationToken).ConfigureAwait(false);
        if (!configurationOwnedByBot) throw new InvalidOperationException("The snapshot configuration must belong to its Trading Bot.");

        dbContext.PortfolioDecisionSnapshots.Add(PortfolioDecisionSnapshotMapper.ToEntity(snapshot));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new PersistenceWriteResult.Succeeded();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
        {
            dbContext.ChangeTracker.Clear();
            return new PersistenceWriteResult.UniquenessConflict("portfolio_decision_snapshot_id");
        }
    }
}

internal static class PortfolioDecisionSnapshotMapper
{
    private const int FreshnessSchemaVersion = 1;

    public static PortfolioDecisionSnapshotEntity ToEntity(PortfolioDecisionSnapshot value)
    {
        ValidateFinancialEnvelope(value);
        return new PortfolioDecisionSnapshotEntity
        {
            Id = value.Id.ToString(),
            PortfolioId = value.PortfolioId.ToString(),
            TradingBotId = value.TradingBotId.ToString(),
            ConfigurationVersionId = value.ConfigurationVersionId.ToString(),
            AsOf = UtcUnixMilliseconds.ToProvider(value.AsOf),
            ReconciliationStatus = CanonicalEnumeration.Format(value.ReconciliationStatus),
            DataFreshnessJson = CanonicalJsonSerializer.Serialize(FreshnessSchemaVersion, FreshnessDto.From(value.DataFreshness)),
            SnapshotSchemaVersion = value.SnapshotSchemaVersion,
            SnapshotJson = value.CanonicalContent,
            ContentHash = value.ContentHash,
            CreatedAt = UtcUnixMilliseconds.ToProvider(value.CreatedAt)
        };
    }

    public static PortfolioDecisionSnapshot ToDomain(PortfolioDecisionSnapshotEntity value)
    {
        if (value.SnapshotSchemaVersion != PortfolioDecisionSnapshot.CurrentSchemaVersion) throw new JsonException("The snapshot schema version is unsupported.");
        var content = CanonicalJsonSerializer.Deserialize<SnapshotDto>(value.SnapshotSchemaVersion, value.SnapshotJson);
        var freshness = CanonicalJsonSerializer.Deserialize<FreshnessDto>(FreshnessSchemaVersion, value.DataFreshnessJson).ToDomain();
        var result = new PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId.Parse(value.Id), PortfolioId.Parse(value.PortfolioId),
            TradingBotId.Parse(value.TradingBotId), TradingBotConfigurationVersionId.Parse(value.ConfigurationVersionId),
            UtcUnixMilliseconds.FromProvider(value.AsOf), CanonicalEnumeration.Parse<ReconciliationStatus>(value.ReconciliationStatus),
            content.Money(content.Cash), content.Money(content.BuyingPower), content.Money(content.ReservedCapital),
            content.Positions.Select(x => new PositionSnapshot(InstrumentId.Parse(x.InstrumentId), ExactDecimalText.FromProvider(x.Quantity), content.Money(x.MarketValue))),
            content.OpenOrders.Select(x => new OpenOrderSnapshot(OrderId.Parse(x.OrderId), InstrumentId.Parse(x.InstrumentId), ExactDecimalText.FromProvider(x.Quantity))),
            ExactDecimalText.FromProvider(content.RiskUtilization), content.RelevantCashFlows.Select(x => new CashFlowSnapshot(content.Money(x.Amount), DateTimeOffset.Parse(x.EffectiveAt, System.Globalization.CultureInfo.InvariantCulture), x.SourceId)),
            freshness, UtcUnixMilliseconds.FromProvider(value.CreatedAt));
        if (!string.Equals(result.CanonicalContent, value.SnapshotJson, StringComparison.Ordinal) || !string.Equals(result.ContentHash, value.ContentHash, StringComparison.Ordinal))
            throw new InvalidDataException("Stored snapshot content or hash is invalid.");
        return result;
    }

    private static void ValidateFinancialEnvelope(PortfolioDecisionSnapshot value)
    {
        _ = CanonicalDecimal.Format(value.Cash.Amount); _ = CanonicalDecimal.Format(value.BuyingPower.Amount); _ = CanonicalDecimal.Format(value.ReservedCapital.Amount); _ = CanonicalDecimal.Format(value.RiskUtilization);
        foreach (var item in value.PositionSnapshots) { _ = CanonicalDecimal.Format(item.Quantity); _ = CanonicalDecimal.Format(item.MarketValue.Amount); }
        foreach (var item in value.OpenOrderSnapshots) _ = CanonicalDecimal.Format(item.Quantity);
        foreach (var item in value.RelevantCashFlows) _ = CanonicalDecimal.Format(item.Amount.Amount);
    }

    private sealed record FreshnessDto(long SourceAsOf, long RetrievedAt, long MaximumAgeTicks)
    {
        public static FreshnessDto From(DataFreshness value) => new(UtcUnixMilliseconds.ToProvider(value.SourceAsOf), UtcUnixMilliseconds.ToProvider(value.RetrievedAt), value.MaximumAge.Ticks);
        public DataFreshness ToDomain() => new(UtcUnixMilliseconds.FromProvider(SourceAsOf), UtcUnixMilliseconds.FromProvider(RetrievedAt), TimeSpan.FromTicks(MaximumAgeTicks));
    }

    private sealed record PositionDto(string InstrumentId, string MarketValue, string Quantity);
    private sealed record OpenOrderDto(string InstrumentId, string OrderId, string Quantity);
    private sealed record CashFlowDto(string Amount, string EffectiveAt, string SourceId);
    private sealed record SnapshotDto(string AsOf, string BuyingPower, string Cash, string ConfigurationVersionId, string Currency,
        FreshnessContentDto DataFreshness, OpenOrderDto[] OpenOrders, string PortfolioId, PositionDto[] Positions, string ReconciliationStatus,
        CashFlowDto[] RelevantCashFlows, string ReservedCapital, string RiskUtilization, string TradingBotId)
    {
        public Money Money(string amount) => new(ExactDecimalText.FromProvider(amount), new Currency(Currency));
    }
    private sealed record FreshnessContentDto(long MaximumAgeTicks, string RetrievedAt, string SourceAsOf);
}
