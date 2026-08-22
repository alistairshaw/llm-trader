using Trading.UI.Wpf.ViewModels;
using Trading.UI.Wpf.Views;

namespace Trading.UI.Wpf.Navigation;

public sealed class WpfNavigationPageFactory(Func<BotManagementViewModel> createBots) : INavigationPageFactory
{
    public INavigationPage Create(ShellRoute route)
    {
        if (route.Key != "bots") return new Placeholder(route.Title);
        var bots = createBots();
        return new Page(new BotManagementView { DataContext = bots }, token => bots.RefreshAsync(token), bots);
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
