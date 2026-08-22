using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Services;

public sealed class AuthorizedPortfolioBrokerViewSource(IOperatorPortfolioBrokerQueries queries,
    TradingBotId botId, BrokerAccountId accountId) : IPortfolioBrokerViewSource
{
    public async Task<PortfolioBrokerLoadResult> LoadAsync(string? search, string? status, int offset, int size,
        CancellationToken cancellationToken)
    {
        var rows = await queries.GetAuthorizedAsync(new(botId, accountId, search, status),
            new(offset, size), cancellationToken);
        return new(PortfolioBrokerLoadStatus.Succeeded, rows);
    }
}
