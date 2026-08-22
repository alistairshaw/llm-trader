using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Data;

public sealed class OperatorPortfolioBrokerQueries(TradingDbContext dbContext) : IOperatorPortfolioBrokerQueries
{
    public async Task<IReadOnlyList<OperatorPortfolioBrokerView>> GetAuthorizedAsync(
        OperatorPortfolioBrokerFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter.TradingBotId);
        ArgumentNullException.ThrowIfNull(filter.BrokerAccountId);
        var botId = filter.TradingBotId.ToString();
        var accountId = filter.BrokerAccountId.ToString();
        var search = filter.Search?.Trim();
        var status = filter.Status?.Trim();

        var query =
            from portfolio in dbContext.Portfolios.AsNoTracking()
            join account in dbContext.BrokerAccounts.AsNoTracking() on portfolio.BrokerAccountId equals account.Id
            join connection in dbContext.BrokerConnections.AsNoTracking() on account.BrokerConnectionId equals connection.Id
            where portfolio.AssignedTradingBotId == botId && portfolio.BrokerAccountId == accountId
                && (search == null || portfolio.Name.Contains(search) || account.DisplayName.Contains(search))
                && (status == null || portfolio.Status == status || account.Status == status || connection.Status == status)
            orderby portfolio.Name, portfolio.Id
            select new Row(portfolio.Id, portfolio.Name, portfolio.AssignedTradingBotId!, account.Id,
                connection.Id, portfolio.BaseCurrency, portfolio.CapitalAllocationAmount, portfolio.Status,
                account.DisplayName, account.Status, connection.DisplayName, connection.Status,
                connection.Environment, account.CapabilitiesJson, account.LastReconciledAt, portfolio.UpdatedAt);

        var rows = await query.Skip(page.Offset).Take(page.Size).ToListAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<OperatorPortfolioBrokerView>(rows.Count);
        foreach (var row in rows)
        {
            var positionValues = await dbContext.Positions.AsNoTracking()
                .Where(x => x.PortfolioId == row.PortfolioId)
                .Select(x => x.Quantity).ToListAsync(cancellationToken).ConfigureAwait(false);
            var ledgerValues = await dbContext.PortfolioLedgerEntries.AsNoTracking()
                .Where(x => x.PortfolioId == row.PortfolioId && x.Currency == row.Currency && x.Amount != null)
                .Select(x => x.Amount!).ToListAsync(cancellationToken).ConfigureAwait(false);
            var mappingCount = await dbContext.InstrumentBrokerMappings.AsNoTracking()
                .CountAsync(x => x.BrokerConnectionId == row.ConnectionId && x.EffectiveTo == null, cancellationToken)
                .ConfigureAwait(false);
            var reconciliation = await dbContext.BrokerReconciliations.AsNoTracking()
                .Where(x => x.BrokerAccountId == row.AccountId)
                .OrderByDescending(x => x.StartedAt).ThenBy(x => x.Id)
                .Select(x => x.Status).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            results.Add(new(PortfolioId.Parse(row.PortfolioId), row.PortfolioName, TradingBotId.Parse(row.TradingBotId),
                BrokerAccountId.Parse(row.AccountId), BrokerConnectionId.Parse(row.ConnectionId), row.Currency,
                ExactDecimalText.FromProvider(row.CapitalAllocation), positionValues.Count,
                positionValues.Sum(ExactDecimalText.FromProvider), ledgerValues.Sum(ExactDecimalText.FromProvider),
                row.PortfolioStatus, row.AccountName, row.AccountStatus, row.ConnectionName, row.ConnectionStatus,
                row.Environment, ParseCapabilities(row.CapabilitiesJson), mappingCount,
                reconciliation ?? (row.LastReconciledAt is null ? "Uncertain" : "Reconciled"),
                row.LastReconciledAt is null ? null : UtcUnixMilliseconds.FromProvider(row.LastReconciledAt.Value),
                UtcUnixMilliseconds.FromProvider(row.UpdatedAt)));
        }
        return results;
    }

    private static string[] ParseCapabilities(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return [];
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.EnumerateArray().Select(x => x.GetString()!)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Order(StringComparer.Ordinal).ToArray(),
            JsonValueKind.Object => document.RootElement.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.True)
                .Select(x => x.Name).Order(StringComparer.Ordinal).ToArray(),
            _ => [],
        };
    }

    private sealed record Row(string PortfolioId, string PortfolioName, string TradingBotId, string AccountId,
        string ConnectionId, string Currency, string CapitalAllocation, string PortfolioStatus, string AccountName,
        string AccountStatus, string ConnectionName, string ConnectionStatus, string Environment,
        string CapabilitiesJson, long? LastReconciledAt, long UpdatedAt);
}
