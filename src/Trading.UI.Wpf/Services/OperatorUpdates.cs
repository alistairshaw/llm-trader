using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Trading.UI.Wpf.Services;

public enum OperatorUpdateKind
{
    Bots, Runs, Research, Proposals, Orders, Fills, Positions, Reconciliation, Warnings, Switches,
}

public sealed record OperatorUpdate(OperatorUpdateKind Kind, string Identity, long Sequence, bool IsTerminal = false)
{
    public const int MaximumIdentityLength = 128;

    public OperatorUpdate Validate()
    {
        if (string.IsNullOrWhiteSpace(Identity) || Identity.Length > MaximumIdentityLength)
            throw new ArgumentException("Update identity is required and bounded.", nameof(Identity));
        if (Sequence < 0) throw new ArgumentOutOfRangeException(nameof(Sequence));
        return this;
    }
}

public interface IOperatorUpdateSource
{
    IAsyncEnumerable<OperatorUpdate> SubscribeAsync(IReadOnlySet<OperatorUpdateKind> kinds,
        CancellationToken cancellationToken = default);
}

public interface IOperatorUpdatePublisher
{
    ValueTask PublishAsync(OperatorUpdate update, CancellationToken cancellationToken = default);
}

public sealed class BoundedOperatorUpdateBuffer(int capacity = 256) : IOperatorUpdateSource, IOperatorUpdatePublisher, IAsyncDisposable
{
    private readonly ConcurrentDictionary<long, Subscription> subscriptions = new();
    private long nextSubscription;
    private bool disposed;

    public IAsyncEnumerable<OperatorUpdate> SubscribeAsync(IReadOnlySet<OperatorUpdateKind> kinds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(kinds);
        var id = Interlocked.Increment(ref nextSubscription);
        var subscription = new Subscription(capacity, kinds);
        subscriptions[id] = subscription;
        return ReadAsync(id, subscription, cancellationToken);
    }

    public async ValueTask PublishAsync(OperatorUpdate update, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        update.Validate();
        foreach (var subscription in subscriptions.Values)
            if (subscription.Kinds.Contains(update.Kind))
                await subscription.EnqueueAsync(update, cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<OperatorUpdate> ReadAsync(long id, Subscription subscription,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in subscription.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return item;
        }
        finally
        {
            subscriptions.TryRemove(id, out _);
            subscription.Complete();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (disposed) return ValueTask.CompletedTask;
        disposed = true;
        foreach (var subscription in subscriptions.Values) subscription.Complete();
        subscriptions.Clear();
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription
    {
        private readonly Channel<OperatorUpdate> channel;
        private readonly Dictionary<(OperatorUpdateKind Kind, string Identity), OperatorUpdate> pending = [];
        private readonly object gate = new();

        public Subscription(int capacity, IReadOnlySet<OperatorUpdateKind> kinds)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            Kinds = kinds;
            channel = Channel.CreateBounded<OperatorUpdate>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public IReadOnlySet<OperatorUpdateKind> Kinds { get; }

        public async ValueTask EnqueueAsync(OperatorUpdate update, CancellationToken token)
        {
            var accepted = false;
            lock (gate)
            {
                var key = (update.Kind, update.Identity);
                if (pending.TryGetValue(key, out var earlier))
                {
                    if (update.Sequence <= earlier.Sequence) return;
                    if (earlier.IsTerminal && !update.IsTerminal) return;
                }
                pending[key] = update;
                accepted = true;
            }
            if (accepted) await channel.Writer.WriteAsync(update, token).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<OperatorUpdate> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var update in channel.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                OperatorUpdate? selected = null;
                lock (gate)
                {
                    var key = (update.Kind, update.Identity);
                    if (!pending.TryGetValue(key, out var newest)) continue;
                    pending.Remove(key);
                    selected = newest;
                }
                yield return selected!;
            }
        }

        public void Complete()
        {
            channel.Writer.TryComplete();
        }
    }
}

public sealed class PollingOperatorUpdateSource : IOperatorUpdateSource
{
    private readonly TimeSpan interval;

    public PollingOperatorUpdateSource(TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        this.interval = interval;
    }

    public async IAsyncEnumerable<OperatorUpdate> SubscribeAsync(IReadOnlySet<OperatorUpdateKind> kinds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        using var timer = new PeriodicTimer(interval);
        long sequence = 0;
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            foreach (var kind in kinds) yield return new(kind, "workspace", ++sequence);
    }
}
