namespace Trading.UI.Wpf.Navigation;

public sealed class PlaceholderNavigationPageFactory : INavigationPageFactory
{
    public INavigationPage Create(ShellRoute route) => new PlaceholderNavigationPage(route.Title);

    private sealed class PlaceholderNavigationPage(string title) : INavigationPage
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
