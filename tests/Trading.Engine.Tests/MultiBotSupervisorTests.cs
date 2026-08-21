using System.Collections.Concurrent;
using NUnit.Framework;
using Trading.Core.Identifiers;
using Trading.Engine.Runtime;

namespace Trading.Engine.Tests;

[Category("MultiBotSupervisor")]
public sealed class MultiBotSupervisorTests
{
    [Test]
    public void OptionsRequirePositiveBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MultiBotSupervisor(new() { GlobalRunConcurrency = 0, QueueCapacity = 1 }, new RecordingExecutor()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MultiBotSupervisor(new() { GlobalRunConcurrency = 1, QueueCapacity = 0 }, new RecordingExecutor()));
    }

    [Test]
    public async Task DifferentBotsRunConcurrentlyWithinGlobalCapacity()
    {
        var executor = new RecordingExecutor();
        await using var supervisor = Create(executor, concurrency: 2, capacity: 4);
        var first = supervisor.TryQueue(Work(Bot(1), "session-a"));
        var second = supervisor.TryQueue(Work(Bot(2), "session-b"));

        await executor.TwoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(executor.MaximumActive, Is.EqualTo(2));
            Assert.That(executor.StartedBots, Is.EquivalentTo(new[] { Bot(1), Bot(2) }));
        });
        executor.ReleaseAll();
        await Task.WhenAll(first.Completion!, second.Completion!);
        await supervisor.StopAsync(default);
    }

    [Test]
    public async Task SameBotIsSerializedAndCannotExceedGlobalCapacity()
    {
        var executor = new RecordingExecutor();
        await using var supervisor = Create(executor, concurrency: 2, capacity: 6);
        var a1 = supervisor.TryQueue(Work(Bot(1), "one"));
        var a2 = supervisor.TryQueue(Work(Bot(1), "two"));
        var b = supervisor.TryQueue(Work(Bot(2), "three"));

        await executor.TwoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(executor.ActiveByBot.GetValueOrDefault(Bot(1)), Is.EqualTo(1));
            Assert.That(executor.MaximumByBot.GetValueOrDefault(Bot(1)), Is.EqualTo(1));
            Assert.That(executor.MaximumActive, Is.LessThanOrEqualTo(2));
        });
        executor.ReleaseAll();
        await Task.WhenAll(a1.Completion!, a2.Completion!, b.Completion!);
        Assert.That(executor.MaximumByBot[Bot(1)], Is.EqualTo(1));
    }

    [Test]
    public async Task BotFailureIsContainedAndIdentityScopedSessionIsPreserved()
    {
        var executor = new RecordingExecutor(faultBot: Bot(1));
        await using var supervisor = Create(executor, 2, 4);
        var failed = supervisor.TryQueue(Work(Bot(1), "private-a"));
        var healthy = supervisor.TryQueue(Work(Bot(2), "private-b"));
        executor.ReleaseAll();

        var results = await Task.WhenAll(failed.Completion!, healthy.Completion!);
        Assert.Multiple(() =>
        {
            Assert.That(results[0].Outcome, Is.EqualTo(BotRunExecutionOutcome.Faulted));
            Assert.That(results[0].Reason, Is.EqualTo("bot_execution_faulted"));
            Assert.That(results[1].Outcome, Is.EqualTo(BotRunExecutionOutcome.Completed));
            Assert.That(((NamedSession)executor.SessionByBot[Bot(1)]).Name, Is.EqualTo("private-a"));
            Assert.That(((NamedSession)executor.SessionByBot[Bot(2)]).Name, Is.EqualTo("private-b"));
        });
    }

    [Test]
    public async Task SaturationRejectsAdmissionBeforeExecutionAndStopRejectsNewWork()
    {
        var executor = new RecordingExecutor();
        await using var supervisor = Create(executor, 1, 1);
        var running = supervisor.TryQueue(Work(Bot(1), "running"));
        await executor.OneStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = supervisor.TryQueue(Work(Bot(2), "queued"));
        var saturated = supervisor.TryQueue(Work(Bot(3), "durable"));
        Assert.Multiple(() =>
        {
            Assert.That(running.Outcome, Is.EqualTo(BotRunQueueOutcome.Accepted));
            Assert.That(queued.Outcome, Is.EqualTo(BotRunQueueOutcome.Accepted));
            Assert.That(saturated.Outcome, Is.EqualTo(BotRunQueueOutcome.Saturated));
            Assert.That(executor.StartedBots, Does.Not.Contain(Bot(3)));
        });
        executor.ReleaseAll();
        await Task.WhenAll(running.Completion!, queued.Completion!);
        await supervisor.StopAsync(default);
        Assert.That(supervisor.TryQueue(Work(Bot(3), "late")).Outcome, Is.EqualTo(BotRunQueueOutcome.Stopping));
    }

    [Test]
    public async Task ShutdownRejectsAdmissionAndCancelsWorkAfterDeadline()
    {
        var executor = new RecordingExecutor();
        await using var supervisor = Create(executor, 1, 2);
        var running = supervisor.TryQueue(Work(Bot(1), "running"));
        var queued = supervisor.TryQueue(Work(Bot(2), "queued"));
        await executor.OneStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var result = await supervisor.ShutdownAsync(TimeSpan.FromMilliseconds(25));

        Assert.Multiple(() =>
        {
            Assert.That(result.CompletedWithinDeadline, Is.False);
            Assert.That(supervisor.TryQueue(Work(Bot(3), "late")).Outcome, Is.EqualTo(BotRunQueueOutcome.Stopping));
            Assert.That(running.Completion!.Result.Outcome, Is.EqualTo(BotRunExecutionOutcome.Cancelled));
            Assert.That(queued.Completion!.Result.Outcome, Is.EqualTo(BotRunExecutionOutcome.Cancelled));
        });
    }

    [Test]
    public async Task ShutdownDrainsCompletedWorkWithinDeadline()
    {
        var executor = new RecordingExecutor();
        await using var supervisor = Create(executor, 1, 1);
        var queued = supervisor.TryQueue(Work(Bot(1), "running"));
        executor.ReleaseAll();
        var result = await supervisor.ShutdownAsync(TimeSpan.FromSeconds(5));
        Assert.Multiple(() =>
        {
            Assert.That(result.CompletedWithinDeadline, Is.True);
            Assert.That(queued.Completion!.Result.Outcome, Is.EqualTo(BotRunExecutionOutcome.Completed));
        });
    }

    [Test]
    public async Task RepeatedAsyncDisposalIsSafe()
    {
        var supervisor = Create(new RecordingExecutor(), 1, 1);

        await supervisor.DisposeAsync();
        Assert.DoesNotThrowAsync(async () => await supervisor.DisposeAsync());
    }

    private static MultiBotSupervisor Create(IBotRunExecutor executor, int concurrency, int capacity) =>
        new(new() { GlobalRunConcurrency = concurrency, QueueCapacity = capacity }, executor);
    private static BotRunSupervisorWork Work(TradingBotId bot, string session) =>
        new(bot, $"host-{bot}", TimeSpan.FromMinutes(5), new NamedSession(session));
    private static TradingBotId Bot(int number) => TradingBotId.Parse($"01J5QH8M00000000000000{number:D4}");

    private sealed class NamedSession(string name) : IModelSession
    {
        public string Name { get; } = name;
        public Task<AssistantResponse> GetNextResponseAsync(ModelRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task SubmitToolResultAsync(ModelToolResult result, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class RecordingExecutor(TradingBotId? faultBot = null) : IBotRunExecutor
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int active;
        public TaskCompletionSource OneStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource TwoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentDictionary<TradingBotId, int> ActiveByBot { get; } = new();
        public ConcurrentDictionary<TradingBotId, int> MaximumByBot { get; } = new();
        public ConcurrentDictionary<TradingBotId, IModelSession> SessionByBot { get; } = new();
        public ConcurrentBag<TradingBotId> StartedBots { get; } = [];
        public int MaximumActive { get; private set; }

        public async Task<BotRunExecutionResult> ExecuteAsync(BotRunExecutionRequest request, CancellationToken token)
        {
            StartedBots.Add(request.TradingBotId); SessionByBot[request.TradingBotId] = request.ModelSession;
            var botActive = ActiveByBot.AddOrUpdate(request.TradingBotId, 1, (_, value) => value + 1);
            MaximumByBot.AddOrUpdate(request.TradingBotId, botActive, (_, value) => Math.Max(value, botActive));
            var current = Interlocked.Increment(ref active);
            MaximumActive = Math.Max(MaximumActive, current);
            OneStarted.TrySetResult(); if (current >= 2) TwoStarted.TrySetResult();
            try
            {
                await release.Task.WaitAsync(token);
                if (request.TradingBotId == faultBot) throw new InvalidOperationException("bot-specific failure");
                return new(BotRunExecutionOutcome.Completed, null, null, "completed");
            }
            finally
            {
                ActiveByBot.AddOrUpdate(request.TradingBotId, 0, (_, value) => value - 1);
                Interlocked.Decrement(ref active);
            }
        }
        public void ReleaseAll() => release.TrySetResult();
    }
}
