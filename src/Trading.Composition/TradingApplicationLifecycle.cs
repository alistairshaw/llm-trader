using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Trading.Host;

public enum ApplicationLifecycleState { Created, Starting, Ready, Failed, Stopping, Stopped }

public sealed class TradingApplicationLifecycle(IHost host, TimeSpan shutdownTimeout) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private int disposed;

    public ApplicationLifecycleState State { get; private set; } = ApplicationLifecycleState.Created;
    public Exception? StartupFailure { get; private set; }
    public IServiceProvider Services => host.Services;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == ApplicationLifecycleState.Ready) return;
            if (State != ApplicationLifecycleState.Created) throw new InvalidOperationException($"Application cannot start from state '{State}'.");
            State = ApplicationLifecycleState.Starting;
            try
            {
                await host.StartAsync(cancellationToken).ConfigureAwait(false);
                await host.Services.GetRequiredService<RuntimeReadiness>().WaitForReadyAsync(cancellationToken).ConfigureAwait(false);
                State = ApplicationLifecycleState.Ready;
            }
            catch (Exception exception)
            {
                StartupFailure = exception;
                State = ApplicationLifecycleState.Failed;
                await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally { gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await StopCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        State = ApplicationLifecycleState.Stopping;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(shutdownTimeout);
        try { await host.StopAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        finally
        {
            if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else host.Dispose();
            State = ApplicationLifecycleState.Stopped;
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
