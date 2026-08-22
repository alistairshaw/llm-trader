using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.IntegrationTests;

[TestFixture, Category("BrokerInboxOutbox")]
public sealed class DurableBrokerProcessorIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 17, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ConcurrentDrainsDispatchOneClaimAndContainIndependentFailure()
    {
        var first = Work("one"); var second = Work("two"); var repository = new ConcurrentStore(first, second);
        var calls = 0; var dispatcher = new Dispatcher(_ => Interlocked.Increment(ref calls) == 1
            ? throw new InvalidOperationException("provider detail must be redacted")
            : new(DurableBrokerDispatchDisposition.Completed, "broker.accepted"));
        var options = new DurableBrokerProcessorOptions(2, 3, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4));
        OrderOutboxProcessor[] processors = [new(repository, dispatcher, new Clock(), "host-a", options), new(repository, dispatcher, new Clock(), "host-b", options)];
        var results = await Task.WhenAll(processors.Select(x => x.DrainOnceAsync(default)));
        Assert.Multiple(() => { Assert.That(results.Sum(x => x.Claimed), Is.EqualTo(2)); Assert.That(results.Sum(x => x.Completed), Is.EqualTo(1)); Assert.That(results.Sum(x => x.Failed), Is.EqualTo(1)); Assert.That(calls, Is.EqualTo(2)); Assert.That(repository.Codes, Does.Contain(DurableBrokerProcessingCodes.TerminalFailure)); });
    }

    [Test]
    public async Task RetrySurvivesProcessorReplacementAndCompletesOnce()
    {
        var repository = new ConcurrentStore(Work("restart")); var attempts = 0;
        var dispatcher = new Dispatcher(_ => ++attempts == 1 ? new(DurableBrokerDispatchDisposition.Retryable, "broker.retryable") : new(DurableBrokerDispatchDisposition.Completed, "broker.accepted"));
        var options = new DurableBrokerProcessorOptions(1, 3, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4));
        Assert.That((await new OrderOutboxProcessor(repository, dispatcher, new Clock(), "old-host", options).DrainOnceAsync(default)).Retried, Is.EqualTo(1));
        repository.Advance();
        Assert.That((await new OrderOutboxProcessor(repository, dispatcher, new Clock(), "new-host", options).DrainOnceAsync(default)).Completed, Is.EqualTo(1));
        Assert.That(repository.Completed, Is.EqualTo(1));
    }

    private static OrderWorkEnvelope Work(string key) => new(OrderWorkItemId.New(), OrderId.New(), OrderWorkKind.Submit, key, "{\"account\":\"paper\"}", new($"paper:{key}"), 0, Now, Now);
    private sealed class Clock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Dispatcher(Func<OrderWorkEnvelope, DurableBrokerDispatchResult> action) : IOrderWorkDispatcher { public Task<DurableBrokerDispatchResult> DispatchAsync(OrderWorkEnvelope work, CancellationToken token) => Task.FromResult(action(work)); }
    private sealed class ConcurrentStore(params OrderWorkEnvelope[] values) : IOrderWorkRepository
    {
        private readonly object gate = new(); private readonly Queue<OrderWorkEnvelope> available = new(values); private readonly Dictionary<OrderWorkItemId, OrderWorkEnvelope> claimed = []; private readonly Queue<OrderWorkEnvelope> delayed = new();
        public List<string> Codes { get; } = []; public int Completed { get; private set; }
        public void Advance() { lock (gate) while (delayed.TryDequeue(out var value)) available.Enqueue(value); }
        public Task<PersistenceWriteResult> EnqueueAsync(OrderWorkEnvelope value, CancellationToken token) { lock (gate) available.Enqueue(value); return Success(); }
        public Task<IReadOnlyList<OrderWorkEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease, CancellationToken token) { lock (gate) { var result = new List<OrderWorkEnvelope>(); while (result.Count < limit && available.TryDequeue(out var item)) { item = new(item.Id, item.OrderId, item.Kind, item.IdempotencyKey, item.CanonicalPayload, item.CorrelationId, item.Attempt + 1, item.AvailableAt, item.CreatedAt); claimed[item.Id] = item; result.Add(item); } return Task.FromResult<IReadOnlyList<OrderWorkEnvelope>>(result); } }
        public Task<PersistenceWriteResult> CompleteAsync(OrderWorkItemId id, string owner, string result, DateTimeOffset at, CancellationToken token) { lock (gate) { if (!claimed.Remove(id)) return Conflict(); Codes.Add(result); Completed++; return Success(); } }
        public Task<PersistenceWriteResult> RetryAsync(OrderWorkItemId id, string owner, string code, DateTimeOffset availableAt, CancellationToken token) { lock (gate) { if (!claimed.Remove(id, out var item)) return Conflict(); Codes.Add(code); delayed.Enqueue(item); return Success(); } }
        public Task<PersistenceWriteResult> RenewAsync(OrderWorkItemId id, string owner, DateTimeOffset expiresAt, CancellationToken token) { lock (gate) return claimed.ContainsKey(id) ? Success() : Conflict(); }
        public Task<PersistenceWriteResult> FailAsync(OrderWorkItemId id, string owner, string code, DateTimeOffset failedAt, CancellationToken token) { lock (gate) { if (!claimed.Remove(id)) return Conflict(); Codes.Add(code); return Success(); } }
        private static Task<PersistenceWriteResult> Success() => Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded());
        private static Task<PersistenceWriteResult> Conflict() => Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.ConcurrencyConflict(0, null));
    }
}
