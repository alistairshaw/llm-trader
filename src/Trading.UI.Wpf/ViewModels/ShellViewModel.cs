using System.Windows.Input;
using Trading.UI.Wpf.Commands;
using Trading.UI.Wpf.Navigation;

namespace Trading.UI.Wpf.ViewModels;

public sealed class ShellViewModel : ObservableViewModel, IAsyncDisposable
{
    private readonly INavigationPageFactory pageFactory;
    private readonly IReadOnlyList<ShellRoute> routes = ShellRoute.All;
    private readonly SemaphoreSlim navigationLock = new(1, 1);
    private CancellationTokenSource? navigationCancellation;
    private INavigationPage? activePage;
    private ShellRoute? activeRoute;
    private object? content;
    private string? errorMessage;
    private bool isBusy;
    private string lifetimeStatus = "Ready";
    private bool disposed;

    public ShellViewModel(INavigationPageFactory pageFactory)
    {
        this.pageFactory = pageFactory;
        NavigateCommand = new AsyncCommand<ShellRoute>(
            (route, cancellationToken) => NavigateAsync(route, cancellationToken),
            allowConcurrentExecutions: true);
    }

    public IReadOnlyList<ShellRoute> Routes => routes;
    public ICommand NavigateCommand { get; }
    public ShellRoute? ActiveRoute { get => activeRoute; private set => SetProperty(ref activeRoute, value); }
    public object? Content { get => content; private set => SetProperty(ref content, value); }
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }
    public string? ErrorMessage
    {
        get => errorMessage;
        private set { if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => ErrorMessage is not null;
    public string LifetimeStatus { get => lifetimeStatus; private set => SetProperty(ref lifetimeStatus, value); }

    public async Task NavigateAsync(ShellRoute route, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(route);
        var nextCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var obsoleteCancellation = Interlocked.Exchange(ref navigationCancellation, nextCancellation);
        if (obsoleteCancellation is not null)
        {
            await obsoleteCancellation.CancelAsync();
            obsoleteCancellation.Dispose();
        }

        try { await navigationLock.WaitAsync(nextCancellation.Token); }
        catch (OperationCanceledException) when (nextCancellation.IsCancellationRequested) { return; }

        try
        {
            if (nextCancellation.IsCancellationRequested) return;
            IsBusy = true;
            LifetimeStatus = $"Loading {route.Title}";
            ErrorMessage = null;
            var nextPage = pageFactory.Create(route);
            try
            {
                await nextPage.LoadAsync(nextCancellation.Token);
                nextCancellation.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (nextCancellation.IsCancellationRequested)
            {
                await nextPage.DisposeAsync();
                return;
            }
            catch (Exception exception)
            {
                await nextPage.DisposeAsync();
                ErrorMessage = $"{route.Title} could not be loaded. {exception.Message}";
                LifetimeStatus = "Load failed";
                return;
            }

            var previousPage = activePage;
            activePage = nextPage;
            ActiveRoute = route;
            Content = nextPage.Content;
            LifetimeStatus = $"Showing {route.Title}";
            if (previousPage is not null) await previousPage.DisposeAsync();
        }
        finally
        {
            IsBusy = false;
            navigationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        var cancellation = Interlocked.Exchange(ref navigationCancellation, null);
        if (cancellation is not null)
        {
            await cancellation.CancelAsync();
            cancellation.Dispose();
        }
        await navigationLock.WaitAsync();
        try
        {
            if (activePage is not null) await activePage.DisposeAsync();
            activePage = null;
            LifetimeStatus = "Stopped";
        }
        finally
        {
            navigationLock.Release();
            navigationLock.Dispose();
        }
    }
}
