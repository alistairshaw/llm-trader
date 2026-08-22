namespace Trading.UI.Wpf.Services;

public interface IUiDispatcher
{
    Task InvokeAsync(Func<Task> action, CancellationToken cancellationToken = default);
}

public sealed class LiveWorkspaceUpdater : IAsyncDisposable
{
    private readonly IOperatorUpdateSource source;
    private readonly IUiDispatcher dispatcher;
    private readonly IReadOnlySet<OperatorUpdateKind> kinds;
    private readonly Func<CancellationToken, Task> refresh;
    private readonly CancellationTokenSource lifetime = new();
    private Task? subscription;

    public LiveWorkspaceUpdater(IOperatorUpdateSource source, IUiDispatcher dispatcher,
        IReadOnlySet<OperatorUpdateKind> kinds, Func<CancellationToken, Task> refresh)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
        this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(lifetime.IsCancellationRequested, this);
        subscription ??= ConsumeAsync();
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var _ in source.SubscribeAsync(kinds, lifetime.Token).ConfigureAwait(false))
                await dispatcher.InvokeAsync(() => refresh(lifetime.Token), lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (lifetime.IsCancellationRequested) return;
        await lifetime.CancelAsync().ConfigureAwait(false);
        if (subscription is not null) await subscription.ConfigureAwait(false);
        lifetime.Dispose();
    }
}
