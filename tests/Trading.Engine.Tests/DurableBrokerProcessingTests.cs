using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.Engine.Tests;

[TestFixture, Category("DurableBrokerProcessing")]
public sealed class DurableBrokerProcessingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 16, 0, 0, TimeSpan.Zero);
    private static readonly DurableBrokerProcessorOptions Options = new(8, 3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(20));
    private static readonly string[] ClaimRenew = ["claim", "renew"];
    private static readonly string[] ClaimRenewComplete = ["claim", "renew", "complete"];

    [Test]
    public async Task OutboxClaimsCommitsThenRenewsBeforeDispatchAndCompletes()
    {
        var work = Work(); var repository = new WorkRepository(work); var dispatcher = new WorkDispatcher(_ =>
        {
            Assert.That(repository.Events, Is.EqualTo(ClaimRenew));
            return new(DurableBrokerDispatchDisposition.Completed, "broker.accepted");
        });
        var result = await new OrderOutboxProcessor(repository, dispatcher, new Clock(), "worker-a", Options).DrainOnceAsync(default);
        Assert.Multiple(() => { Assert.That(result.Completed, Is.EqualTo(1)); Assert.That(result.Processed, Is.EqualTo(1)); Assert.That(repository.Events, Is.EqualTo(ClaimRenewComplete)); });
    }

    [Test]
    public async Task OutboxRetryUsesBoundedExponentialBackoffAndExhaustionIsTerminal()
    {
        var retry = Work(attempt: 2); var repository = new WorkRepository(retry);
        var processor = new OrderOutboxProcessor(repository, new WorkDispatcher(_ => new(DurableBrokerDispatchDisposition.Retryable, "broker.retryable")), new Clock(), "worker", Options);
        Assert.That((await processor.DrainOnceAsync(default)).Retried, Is.EqualTo(1));
        Assert.That(repository.AvailableAt, Is.EqualTo(Now.AddSeconds(4)));
        repository.Reset(Work(attempt: 3));
        Assert.That((await processor.DrainOnceAsync(default)).Failed, Is.EqualTo(1));
        Assert.That(repository.Code, Is.EqualTo(DurableBrokerProcessingCodes.RetryExhausted));
    }

    [TestCase("not-json", DurableBrokerProcessingCodes.MalformedPayload)]
    [TestCase("{\"value\":1,\"value\":2}", DurableBrokerProcessingCodes.MalformedPayload)]
    [TestCase("{ \"value\": 1 }", DurableBrokerProcessingCodes.PayloadNotCanonical)]
    public async Task MalformedOrNonCanonicalOutboxPayloadTerminatesWithoutDispatch(string payload, string code)
    {
        var repository = new WorkRepository(Work(payload: payload)); var dispatcher = new WorkDispatcher(_ => throw new AssertionException("must not dispatch"));
        Assert.That((await new OrderOutboxProcessor(repository, dispatcher, new Clock(), "worker", Options).DrainOnceAsync(default)).Failed, Is.EqualTo(1));
        Assert.That(repository.Code, Is.EqualTo(code));
    }

    [Test]
    public async Task ItemFailureIsContainedAndNextClaimedItemCompletes()
    {
        var first = Work(); var second = new OrderWorkEnvelope(OrderWorkItemId.New(), OrderId.New(), OrderWorkKind.Submit, "submit-2", "{\"value\":1}", new("account:paper:corr-2"), 1, Now, Now);
        var repository = new WorkRepository(first, second); var calls = 0;
        var dispatcher = new WorkDispatcher(_ => ++calls == 1 ? throw new InvalidOperationException("secret provider payload") : new(DurableBrokerDispatchDisposition.Completed, "broker.accepted"));
        var result = await new OrderOutboxProcessor(repository, dispatcher, new Clock(), "worker", Options).DrainOnceAsync(default);
        Assert.Multiple(() => { Assert.That(result.Failed, Is.EqualTo(1)); Assert.That(result.Completed, Is.EqualTo(1)); Assert.That(repository.Codes, Does.Contain(DurableBrokerProcessingCodes.TerminalFailure)); Assert.That(repository.Codes.Any(x => x.Contains("secret", StringComparison.OrdinalIgnoreCase)), Is.False); });
    }

    [Test]
    public async Task InboxCancellationReleasesLeaseAndDuplicateReceiptIsRepositoryIdempotent()
    {
        var message = Message(); var repository = new InboxRepository(message); using var cancellation = new CancellationTokenSource();
        var dispatcher = new InboxDispatcher(_ => { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); });
        var result = await new BrokerInboxProcessor(repository, dispatcher, new Clock(), "worker", Options).DrainOnceAsync(cancellation.Token);
        Assert.Multiple(() => { Assert.That(result.Retried, Is.EqualTo(1)); Assert.That(repository.Code, Is.EqualTo(DurableBrokerProcessingCodes.Cancelled)); Assert.That(repository.DispatchCount, Is.Zero); });
        Assert.That(await repository.ReceiveAsync(message with { Id = BrokerMessageId.New() }, default), Is.TypeOf<PersistenceWriteResult.UniquenessConflict>());
    }

    [Test]
    public async Task ExpiredLeaseCanBeRecoveredWithoutDuplicateHistory()
    {
        var repository = new WorkRepository(Work()) { ClaimedByOtherUntil = Now.AddSeconds(-1) };
        var result = await new OrderOutboxProcessor(repository, new WorkDispatcher(_ => new(DurableBrokerDispatchDisposition.Completed, "broker.accepted")), new Clock(), "recovery", Options).DrainOnceAsync(default);
        Assert.Multiple(() => { Assert.That(result.Completed, Is.EqualTo(1)); Assert.That(repository.CompletionCount, Is.EqualTo(1)); Assert.That(repository.Events.Count(x => x == "complete"), Is.EqualTo(1)); });
    }

    private static OrderWorkEnvelope Work(int attempt = 1, string payload = "{\"value\":1}") => new(OrderWorkItemId.New(), OrderId.New(), OrderWorkKind.Submit, "submit-1", payload, new("account:paper:corr-1"), attempt, Now, Now);
    private static BrokerInboxEnvelope Message() => new(BrokerMessageId.New(), "simulated:event-1", "{\"value\":1}", new("account:paper:corr-1"), Now, 1);
    private sealed class Clock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class WorkDispatcher(Func<OrderWorkEnvelope, DurableBrokerDispatchResult> dispatch) : IOrderWorkDispatcher { public Task<DurableBrokerDispatchResult> DispatchAsync(OrderWorkEnvelope work, CancellationToken token) => Task.FromResult(dispatch(work)); }
    private sealed class InboxDispatcher(Func<BrokerInboxEnvelope, DurableBrokerDispatchResult> dispatch) : IBrokerInboxDispatcher { public Task<DurableBrokerDispatchResult> DispatchAsync(BrokerInboxEnvelope message, CancellationToken token) => Task.FromResult(dispatch(message)); }

    private sealed class WorkRepository(params OrderWorkEnvelope[] work) : IOrderWorkRepository
    {
        private List<OrderWorkEnvelope> pending = [.. work];
        public List<string> Events { get; } = []; public List<string> Codes { get; } = []; public string? Code => Codes.LastOrDefault(); public DateTimeOffset? AvailableAt { get; private set; }
        public DateTimeOffset? ClaimedByOtherUntil { get; init; }
        public int CompletionCount { get; private set; }
        public void Reset(OrderWorkEnvelope item) { pending = [item]; Events.Clear(); Codes.Clear(); }
        public Task<PersistenceWriteResult> EnqueueAsync(OrderWorkEnvelope value, CancellationToken token) => Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded());
        public Task<IReadOnlyList<OrderWorkEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease, CancellationToken token) { Events.Add("claim"); IReadOnlyList<OrderWorkEnvelope> result = ClaimedByOtherUntil > now ? [] : pending.Take(limit).ToArray(); pending.Clear(); return Task.FromResult(result); }
        public Task<PersistenceWriteResult> CompleteAsync(OrderWorkItemId id, string owner, string result, DateTimeOffset at, CancellationToken token) { Events.Add("complete"); Codes.Add(result); CompletionCount++; return Success(); }
        public Task<PersistenceWriteResult> RetryAsync(OrderWorkItemId id, string owner, string code, DateTimeOffset availableAt, CancellationToken token) { Events.Add("retry"); Codes.Add(code); AvailableAt = availableAt; return Success(); }
        public Task<PersistenceWriteResult> RenewAsync(OrderWorkItemId id, string owner, DateTimeOffset expiresAt, CancellationToken token) { Events.Add("renew"); return Success(); }
        public Task<PersistenceWriteResult> FailAsync(OrderWorkItemId id, string owner, string code, DateTimeOffset failedAt, CancellationToken token) { Events.Add("fail"); Codes.Add(code); return Success(); }
    }

    private sealed class InboxRepository(BrokerInboxEnvelope message) : IBrokerInboxRepository
    {
        private bool claimed;
        public string? Code { get; private set; }
        public int DispatchCount { get; private set; }
        public Task<PersistenceWriteResult> ReceiveAsync(BrokerInboxEnvelope value, CancellationToken token) => Task.FromResult<PersistenceWriteResult>(value.IdempotencyKey == message.IdempotencyKey ? new PersistenceWriteResult.UniquenessConflict("inbox_idempotency_key") : new PersistenceWriteResult.Succeeded());
        public Task<IReadOnlyList<BrokerInboxEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease, CancellationToken token) { IReadOnlyList<BrokerInboxEnvelope> result = claimed ? [] : [message]; claimed = true; return Task.FromResult(result); }
        public Task<PersistenceWriteResult> CompleteAsync(BrokerMessageId id, string owner, string result, DateTimeOffset at, CancellationToken token) { DispatchCount++; Code = result; return Success(); }
        public Task<PersistenceWriteResult> RetryAsync(BrokerMessageId id, string owner, string code, DateTimeOffset availableAt, CancellationToken token) { Code = code; return Success(); }
        public Task<PersistenceWriteResult> RenewAsync(BrokerMessageId id, string owner, DateTimeOffset expiresAt, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> FailAsync(BrokerMessageId id, string owner, string code, DateTimeOffset failedAt, CancellationToken token) { Code = code; return Success(); }
    }
    private static Task<PersistenceWriteResult> Success() => Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded());
}
