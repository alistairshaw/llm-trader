using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Trading.Host;

public enum ApplicationLifecycleState { Created, Starting, Ready, Failed, Stopping, Stopped }

public sealed class TradingApplicationLifecycle(IHost host, TimeSpan shutdownTimeout) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HostDatabaseIdentity? databaseIdentity = host.Services.GetService<HostDatabaseIdentity>();
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
            try
            {
                if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else host.Dispose();
            }
            finally
            {
                ReleaseOwnedDatabasePool();
                State = ApplicationLifecycleState.Stopped;
            }
        }
    }

    private void ReleaseOwnedDatabasePool()
    {
        if (databaseIdentity is null) return;

        // Host disposal closes every context and connection owned by the root provider. The
        // provider pool is process-wide, so release only the canonical pool for this host.
        using var poolIdentity = new SqliteConnection(databaseIdentity.ConnectionString);
        SqliteConnection.ClearPool(poolIdentity);
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
