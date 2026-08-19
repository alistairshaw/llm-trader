using System.Threading.Channels;
using Trading.Core.Identifiers;

namespace Trading.Engine.Runtime;

public sealed class MultiBotSupervisorOptions
{
    public int GlobalRunConcurrency { get; init; } = 1;
    public int QueueCapacity { get; init; } = 1;

    public void Validate()
    {
        if (GlobalRunConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(GlobalRunConcurrency), "Global run concurrency must be positive.");
        if (QueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity), "Queue capacity must be positive.");
    }
}

public interface IBotRunExecutor
{
    Task<BotRunExecutionResult> ExecuteAsync(BotRunExecutionRequest request, CancellationToken cancellationToken);
}

public sealed record BotRunSupervisorWork(TradingBotId TradingBotId, string LeaseOwner,
    TimeSpan LeaseDuration, IModelSession ModelSession);

public enum BotRunQueueOutcome { Accepted, Saturated, Stopping }

public sealed record BotRunQueueResult(BotRunQueueOutcome Outcome, Task<BotRunExecutionResult>? Completion)
{
    public static BotRunQueueResult Rejected(BotRunQueueOutcome outcome) => new(outcome, null);
}

/// <summary>Coordinates bounded multi-Bot execution. Work is not durably claimed until a worker invokes
/// the one-run service, so rejected queue admission cannot consume or lose a durable trigger.</summary>
public sealed class MultiBotSupervisor : IAsyncDisposable
{
    private readonly Channel<QueuedWork> work;
    private readonly IBotRunExecutor executor;
    private readonly Channel<TradingBotId> completions = Channel.CreateUnbounded<TradingBotId>();
    private readonly SemaphoreSlim admission;
    private readonly int globalRunConcurrency;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task coordinator;
    private int stopping;
    private int cancelledRuns;

    public MultiBotSupervisor(MultiBotSupervisorOptions options, IBotRunExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(executor);
        options.Validate();
        this.executor = executor;
        globalRunConcurrency = options.GlobalRunConcurrency;
        admission = new SemaphoreSlim(options.GlobalRunConcurrency + options.QueueCapacity,
            options.GlobalRunConcurrency + options.QueueCapacity);
        work = Channel.CreateBounded<QueuedWork>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = options.GlobalRunConcurrency == 1,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        coordinator = Task.Run(RunCoordinatorAsync);
    }

    public BotRunQueueResult TryQueue(BotRunSupervisorWork request)
    {
        Validate(request);
        if (Volatile.Read(ref stopping) != 0)
            return BotRunQueueResult.Rejected(BotRunQueueOutcome.Stopping);
        if (!admission.Wait(0)) return BotRunQueueResult.Rejected(BotRunQueueOutcome.Saturated);
        var queued = new QueuedWork(request);
        if (work.Writer.TryWrite(queued)) return new(BotRunQueueOutcome.Accepted, queued.Completion.Task);
        admission.Release();
        return BotRunQueueResult.Rejected(Volatile.Read(ref stopping) != 0
            ? BotRunQueueOutcome.Stopping
            : BotRunQueueOutcome.Saturated);
    }

    public async Task<BotRunQueueResult> QueueAsync(BotRunSupervisorWork request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        if (Volatile.Read(ref stopping) != 0)
            return BotRunQueueResult.Rejected(BotRunQueueOutcome.Stopping);
        await admission.WaitAsync(cancellationToken).ConfigureAwait(false);
        var queued = new QueuedWork(request);
        try
        {
            await work.Writer.WriteAsync(queued, cancellationToken).ConfigureAwait(false);
            return new(BotRunQueueOutcome.Accepted, queued.Completion.Task);
        }
        catch (ChannelClosedException)
        {
            admission.Release();
            return BotRunQueueResult.Rejected(BotRunQueueOutcome.Stopping);
        }
        catch
        {
            admission.Release();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref stopping, 1) == 0)
            work.Writer.TryComplete();
        await coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops admission immediately, drains within the deadline, then cancels active and queued work.</summary>
    public async Task<ShutdownResult> ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (Interlocked.Exchange(ref stopping, 1) == 0) work.Writer.TryComplete();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await coordinator.WaitAsync(deadline.Token).ConfigureAwait(false);
            return new(0, true);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            lifetime.Cancel();
            try { await coordinator.ConfigureAwait(false); } catch (OperationCanceledException) { }
            return new(Volatile.Read(ref cancelledRuns), false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref stopping, 1) == 0)
            work.Writer.TryComplete();
        lifetime.Cancel();
        try { await coordinator.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        lifetime.Dispose();
        admission.Dispose();
    }

    private async Task RunCoordinatorAsync()
    {
        var pending = new LinkedList<QueuedWork>();
        var activeBots = new HashSet<TradingBotId>();
        var activeCount = 0;
        try
        {
            while (true)
            {
                while (work.Reader.TryRead(out var queued)) pending.AddLast(queued);
                while (completions.Reader.TryRead(out var completedBot))
                {
                    activeBots.Remove(completedBot);
                    activeCount--;
                }
                while (activeCount < globalRunConcurrency)
                {
                    var eligible = pending.First;
                    while (eligible is not null && activeBots.Contains(eligible.Value.Request.TradingBotId))
                        eligible = eligible.Next;
                    if (eligible is null) break;
                    pending.Remove(eligible);
                    activeBots.Add(eligible.Value.Request.TradingBotId);
                    activeCount++;
                    _ = ExecuteIsolatedAsync(eligible.Value);
                }
                if (work.Reader.Completion.IsCompleted && pending.Count == 0 && activeCount == 0) return;

                var inputReady = work.Reader.WaitToReadAsync(lifetime.Token).AsTask();
                var completionReady = completions.Reader.WaitToReadAsync(lifetime.Token).AsTask();
                await await Task.WhenAny(inputReady, completionReady).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            foreach (var queued in pending) CompleteCancelled(queued);
            while (work.Reader.TryRead(out var queued)) CompleteCancelled(queued);
        }
    }

    private async Task ExecuteIsolatedAsync(QueuedWork queued)
    {
        try
        {
            var request = new BotRunExecutionRequest(queued.Request.TradingBotId,
                queued.Request.LeaseOwner.Trim(), queued.Request.LeaseDuration, queued.Request.ModelSession);
            var result = await executor.ExecuteAsync(request, lifetime.Token).ConfigureAwait(false);
            queued.Completion.TrySetResult(result);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            Interlocked.Increment(ref cancelledRuns);
            queued.Completion.TrySetResult(new(BotRunExecutionOutcome.Cancelled, null, null, "supervisor_stopped"));
        }
        catch (Exception)
        {
            queued.Completion.TrySetResult(new(BotRunExecutionOutcome.Faulted, null, null, "bot_execution_faulted"));
        }
        finally
        {
            admission.Release();
            completions.Writer.TryWrite(queued.Request.TradingBotId);
        }
    }

    private void CompleteCancelled(QueuedWork queued)
    {
        Interlocked.Increment(ref cancelledRuns);
        queued.Completion.TrySetResult(new(BotRunExecutionOutcome.Cancelled, null, null, "supervisor_stopped"));
        admission.Release();
    }

    private static void Validate(BotRunSupervisorWork request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ModelSession);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseOwner);
        if (request.LeaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request));
    }

    private sealed class QueuedWork(BotRunSupervisorWork request)
    {
        public BotRunSupervisorWork Request { get; } = request;
        public TaskCompletionSource<BotRunExecutionResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
