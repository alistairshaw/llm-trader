namespace Trading.UI.Wpf.Navigation;

public interface INavigationPage : IAsyncDisposable
{
    object Content { get; }
    ValueTask LoadAsync(CancellationToken cancellationToken);
}

public interface INavigationPageFactory
{
    INavigationPage Create(ShellRoute route);
}
