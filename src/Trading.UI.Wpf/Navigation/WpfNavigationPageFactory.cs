using Trading.UI.Wpf.ViewModels;
using Trading.UI.Wpf.Views;

namespace Trading.UI.Wpf.Navigation;

public sealed class WpfNavigationPageFactory(Func<BotManagementViewModel> createBots,
    Func<BotRunsViewModel> createRuns,
    Func<ResearchCatalogViewModel>? createResearch = null) : INavigationPageFactory
{
    public INavigationPage Create(ShellRoute route)
    {
        if (route.Key == "bots")
        {
            var bots = createBots();
            return new Page(new BotManagementView { DataContext = bots }, token => bots.RefreshAsync(token), bots);
        }
        if (route.Key == "runs")
        {
            var runs = createRuns();
            return new Page(new BotRunsView { DataContext = runs }, token => runs.RefreshAsync(token), runs);
        }
        if (route.Key == "research" && createResearch is not null)
        {
            var research = createResearch();
            return new Page(new ResearchCatalogView { DataContext = research }, token => research.RefreshAsync(token), research);
        }
        return new Placeholder(route.Title);
    }

    private sealed class Page(object content, Func<CancellationToken, Task> load, IAsyncDisposable lifetime) : INavigationPage
    {
        public object Content { get; } = content;
        public async ValueTask LoadAsync(CancellationToken cancellationToken) => await load(cancellationToken);
        public ValueTask DisposeAsync() => lifetime.DisposeAsync();
    }

    private sealed class Placeholder(string title) : INavigationPage
    {
        public object Content { get; } = $"{title} workspace";
        public ValueTask LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
