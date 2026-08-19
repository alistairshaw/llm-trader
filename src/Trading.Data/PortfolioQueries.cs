using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Portfolios;

namespace Trading.Data;

public sealed class PortfolioQueries(TradingDbContext dbContext) : IPortfolioQueries
{
    public async Task<PortfolioSummary?> GetSummaryAsync(PortfolioId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var row = await dbContext.Portfolios.AsNoTracking()
            .Where(x => x.Id == id.ToString())
            .Select(x => new PortfolioRow(x.Id, x.Name, x.BaseCurrency, x.Status, x.CapitalAllocationAmount,
                x.BrokerAccountId, x.AssignedTradingBotId, x.CreatedAt, x.UpdatedAt, x.Version))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : ToSummary(row);
    }

    public async Task<IReadOnlyList<PortfolioSummary>> GetPortfoliosAsync(PortfolioQueryFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        ValidatePage(page);
        var query = dbContext.Portfolios.AsNoTracking();
        if (filter.BrokerAccountId is not null) query = query.Where(x => x.BrokerAccountId == filter.BrokerAccountId.ToString());
        if (filter.TradingBotId is not null) query = query.Where(x => x.AssignedTradingBotId == filter.TradingBotId.ToString());
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.Size)
            .Select(x => new PortfolioRow(x.Id, x.Name, x.BaseCurrency, x.Status, x.CapitalAllocationAmount,
                x.BrokerAccountId, x.AssignedTradingBotId, x.CreatedAt, x.UpdatedAt, x.Version))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(ToSummary).ToArray();
    }

    public async Task<IReadOnlyList<PositionView>> GetPositionsAsync(PositionQueryFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        ValidatePage(page);
        ValidateRange(filter.UpdatedFrom, filter.UpdatedTo, nameof(filter.UpdatedFrom), nameof(filter.UpdatedTo));
        var query = dbContext.Positions.AsNoTracking();
        if (filter.PortfolioId is not null) query = query.Where(x => x.PortfolioId == filter.PortfolioId.ToString());
        if (filter.InstrumentId is not null) query = query.Where(x => x.InstrumentId == filter.InstrumentId.ToString());
        if (filter.UpdatedFrom is not null) query = query.Where(x => x.UpdatedAt >= Milliseconds(filter.UpdatedFrom.Value, nameof(filter.UpdatedFrom)));
        if (filter.UpdatedTo is not null) query = query.Where(x => x.UpdatedAt <= Milliseconds(filter.UpdatedTo.Value, nameof(filter.UpdatedTo)));
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.Size).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(x => new PositionView(PositionId.Parse(x.Id), PortfolioId.Parse(x.PortfolioId), InstrumentId.Parse(x.InstrumentId),
            ExactDecimalText.FromProvider(x.Quantity), x.QuantityUnit,
            new Money(ExactDecimalText.FromProvider(x.AverageCostAmount), new Currency(x.AverageCostCurrency)),
            new Money(ExactDecimalText.FromProvider(x.RealizedPnlAmount), new Currency(x.RealizedPnlCurrency)), x.Version,
            UtcUnixMilliseconds.FromProvider(x.UpdatedAt), x.ClosedAt is null ? null : UtcUnixMilliseconds.FromProvider(x.ClosedAt.Value))).ToArray();
    }

    public async Task<IReadOnlyList<PortfolioLedgerEntryView>> GetLedgerAsync(PortfolioLedgerQueryFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        ValidatePage(page);
        ValidateRange(filter.EffectiveFrom, filter.EffectiveTo, nameof(filter.EffectiveFrom), nameof(filter.EffectiveTo));
        var query = dbContext.PortfolioLedgerEntries.AsNoTracking();
        if (filter.PortfolioId is not null) query = query.Where(x => x.PortfolioId == filter.PortfolioId.ToString());
        if (filter.InstrumentId is not null) query = query.Where(x => x.InstrumentId == filter.InstrumentId.ToString());
        if (filter.EffectiveFrom is not null) query = query.Where(x => x.EffectiveAt >= Milliseconds(filter.EffectiveFrom.Value, nameof(filter.EffectiveFrom)));
        if (filter.EffectiveTo is not null) query = query.Where(x => x.EffectiveAt <= Milliseconds(filter.EffectiveTo.Value, nameof(filter.EffectiveTo)));
        if (filter.BrokerAccountId is not null)
            query = query.Where(x => dbContext.Portfolios.Any(p => p.Id == x.PortfolioId && p.BrokerAccountId == filter.BrokerAccountId.ToString()));
        if (filter.TradingBotId is not null)
            query = query.Where(x => dbContext.Portfolios.Any(p => p.Id == x.PortfolioId && p.AssignedTradingBotId == filter.TradingBotId.ToString()));
        var rows = await query.OrderByDescending(x => x.EffectiveAt).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.Size).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(x => new PortfolioLedgerEntryView(PortfolioLedgerEntryId.Parse(x.Id), PortfolioId.Parse(x.PortfolioId),
            CanonicalEnumeration.Parse<PortfolioLedgerEntryType>(x.EntryType),
            new Money(ExactDecimalText.FromProvider(x.Amount!), new Currency(x.Currency!)),
            x.InstrumentId is null ? null : InstrumentId.Parse(x.InstrumentId),
            x.Quantity is null ? null : ExactDecimalText.FromProvider(x.Quantity), UtcUnixMilliseconds.FromProvider(x.EffectiveAt),
            CanonicalEnumeration.Parse<LedgerSourceType>(x.SourceType), x.SourceId, UtcUnixMilliseconds.FromProvider(x.RecordedAt),
            x.ReversesEntryId is null ? null : PortfolioLedgerEntryId.Parse(x.ReversesEntryId), x.Description)).ToArray();
    }

    public async Task<BrokerAccountAssociationView?> GetBrokerAccountAssociationAsync(PortfolioId portfolioId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(portfolioId);
        var row = await (from portfolio in dbContext.Portfolios.AsNoTracking()
                         join account in dbContext.BrokerAccounts.AsNoTracking() on portfolio.BrokerAccountId equals account.Id
                         where portfolio.Id == portfolioId.ToString()
                         select new AssociationRow(portfolio.Id, account.Id, account.BrokerConnectionId, account.ExternalAccountId,
                             account.DisplayName, account.Status, account.LastReconciledAt, account.Version))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : new BrokerAccountAssociationView(PortfolioId.Parse(row.PortfolioId), BrokerAccountId.Parse(row.BrokerAccountId),
            BrokerConnectionId.Parse(row.BrokerConnectionId), row.ExternalAccountId, row.DisplayName,
            CanonicalEnumeration.Parse<BrokerAccountStatus>(row.Status),
            row.LastReconciledAt is null ? null : UtcUnixMilliseconds.FromProvider(row.LastReconciledAt.Value), row.Version);
    }

    public async Task<IReadOnlyList<PortfolioDecisionSnapshotSummary>> GetDecisionSnapshotsAsync(PortfolioDecisionSnapshotQueryFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        ValidatePage(page);
        ValidateRange(filter.AsOfFrom, filter.AsOfTo, nameof(filter.AsOfFrom), nameof(filter.AsOfTo));
        var query = dbContext.PortfolioDecisionSnapshots.AsNoTracking();
        if (filter.PortfolioId is not null) query = query.Where(x => x.PortfolioId == filter.PortfolioId.ToString());
        if (filter.TradingBotId is not null) query = query.Where(x => x.TradingBotId == filter.TradingBotId.ToString());
        if (filter.AsOfFrom is not null) query = query.Where(x => x.AsOf >= Milliseconds(filter.AsOfFrom.Value, nameof(filter.AsOfFrom)));
        if (filter.AsOfTo is not null) query = query.Where(x => x.AsOf <= Milliseconds(filter.AsOfTo.Value, nameof(filter.AsOfTo)));
        var rows = await query.OrderByDescending(x => x.AsOf).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.Size).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(x => new PortfolioDecisionSnapshotSummary(PortfolioDecisionSnapshotId.Parse(x.Id), PortfolioId.Parse(x.PortfolioId),
            TradingBotId.Parse(x.TradingBotId), TradingBotConfigurationVersionId.Parse(x.ConfigurationVersionId),
            UtcUnixMilliseconds.FromProvider(x.AsOf), CanonicalEnumeration.Parse<ReconciliationStatus>(x.ReconciliationStatus),
            x.ContentHash, UtcUnixMilliseconds.FromProvider(x.CreatedAt))).ToArray();
    }

    private static PortfolioSummary ToSummary(PortfolioRow x) => new(PortfolioId.Parse(x.Id), x.Name, new Currency(x.BaseCurrency),
        CanonicalEnumeration.Parse<PortfolioStatus>(x.Status), new Money(ExactDecimalText.FromProvider(x.CapitalAllocationAmount), new Currency(x.BaseCurrency)),
        x.BrokerAccountId is null ? null : BrokerAccountId.Parse(x.BrokerAccountId),
        x.AssignedTradingBotId is null ? null : TradingBotId.Parse(x.AssignedTradingBotId),
        UtcUnixMilliseconds.FromProvider(x.CreatedAt), UtcUnixMilliseconds.FromProvider(x.UpdatedAt), x.Version);

    private static void ValidateRange(DateTimeOffset? from, DateTimeOffset? to, string fromName, string toName)
    {
        if (from is not null) _ = Milliseconds(from.Value, fromName);
        if (to is not null) _ = Milliseconds(to.Value, toName);
        if (from > to) throw new ArgumentException("The start of a time range must not follow its end.", fromName);
    }

    private static long Milliseconds(DateTimeOffset value, string parameterName) =>
        value.Offset == TimeSpan.Zero ? value.ToUnixTimeMilliseconds() : throw new ArgumentException("Timestamp filters must be expressed in UTC.", parameterName);

    private static void ValidatePage(PageRequest page)
    {
        if (page.Offset < 0 || page.Size is < 1 or > PageRequest.MaximumSize)
            throw new ArgumentOutOfRangeException(nameof(page), "Pagination must have a non-negative offset and a bounded positive size.");
    }

    private sealed record PortfolioRow(string Id, string Name, string BaseCurrency, string Status, string CapitalAllocationAmount,
        string? BrokerAccountId, string? AssignedTradingBotId, long CreatedAt, long UpdatedAt, long Version);
    private sealed record AssociationRow(string PortfolioId, string BrokerAccountId, string BrokerConnectionId, string ExternalAccountId,
        string DisplayName, string Status, long? LastReconciledAt, long Version);
}
