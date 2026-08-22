using NUnit.Framework;
using Trading.UI.Wpf.Services;

namespace Trading.UI.Wpf.Tests;

[TestFixture]
[Category("LiveUpdates")]
public sealed class LiveUpdatesWpfTests
{
    [Test]
    public async Task RefreshesOnlyThroughInjectedDispatcherAndStopsOnDisposal()
    {
        await using var source = new BoundedOperatorUpdateBuffer(4);
        var dispatcher = new RecordingDispatcher();
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        await using var updater = new LiveWorkspaceUpdater(source, dispatcher,
            new HashSet<OperatorUpdateKind> { OperatorUpdateKind.Runs }, _ =>
            {
                Interlocked.Increment(ref count);
                refreshed.TrySetResult();
                return Task.CompletedTask;
            });

        updater.Start();
        await source.PublishAsync(new(OperatorUpdateKind.Runs, "run-1", 1));
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(dispatcher.Invocations, Is.EqualTo(1));
        });

        await updater.DisposeAsync();
        await source.PublishAsync(new(OperatorUpdateKind.Runs, "run-1", 2, true));
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task IgnoresUpdatesForInactiveWorkspace()
    {
        await using var source = new BoundedOperatorUpdateBuffer();
        var dispatcher = new RecordingDispatcher();
        await using var updater = new LiveWorkspaceUpdater(source, dispatcher,
            new HashSet<OperatorUpdateKind> { OperatorUpdateKind.Research }, _ => Task.CompletedTask);
        updater.Start();
        await source.PublishAsync(new(OperatorUpdateKind.Fills, "fill-1", 1, true));
        await Task.Delay(25);
        Assert.That(dispatcher.Invocations, Is.Zero);
    }

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        public int Invocations { get; private set; }
        public async Task InvokeAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations++;
            await action();
        }
    }
}
