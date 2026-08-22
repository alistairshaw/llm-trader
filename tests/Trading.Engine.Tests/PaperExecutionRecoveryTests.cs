using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Engine.Execution;

namespace Trading.Engine.Tests;

[TestFixture, Category("ExecutionRecovery")]
public sealed class PaperExecutionRecoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ReconcilesAccountsBeforeDurableWorkersAndPersistsRecoveryAudit()
    {
        var trace = new List<string>();
        var account = BrokerAccountId.New();
        var result = new PaperExecutionRecoveryResult(1, 2, 3, 1, 1,
            [new(account, PortfolioId.New(), OrderId.New())]);
        var audit = new Audit(trace);
        var service = Service(new Recovery(result, trace), audit, new Accounts(trace, true),
            new WorkStore(trace), new InboxStore(trace));

        var completed = await service.RecoverAndDrainAsync(default);

        Assert.Multiple(() =>
        {
            Assert.That(completed.IsReady, Is.True);
            Assert.That(completed.ReconciledAccounts, Is.EqualTo(1));
            Assert.That(audit.Records, Has.Count.EqualTo(1));
            Assert.That(audit.Records[0].ResolutionJson,
                Does.Contain("paper_execution.recovery.account_reconciled"));
            Assert.That(string.Join(",", trace), Is.EqualTo("recover,account,audit,outbox,inbox"));
        });
    }

    [Test]
    public async Task FailedRequiredAccountReconciliationKeepsRuntimeNotReadyAndDoesNotClaimWork()
    {
        var trace = new List<string>();
        var result = new PaperExecutionRecoveryResult(0, 0, 0, 0, 0,
            [new(BrokerAccountId.New(), PortfolioId.New(), OrderId.New())]);
        var service = Service(new Recovery(result, trace), new Audit(trace), new Accounts(trace, false),
            new WorkStore(trace), new InboxStore(trace));

        var completed = await service.RecoverAndDrainAsync(default);

        Assert.Multiple(() =>
        {
            Assert.That(completed.IsReady, Is.False);
            Assert.That(completed.ReconciledAccounts, Is.Zero);
            Assert.That(string.Join(",", trace), Is.EqualTo("recover,account,audit"));
        });
    }

    [Test]
    public void CancellationBeforeWorkerClaimLeavesDurableRecoveryCommittedAndClaimsNothing()
    {
        var trace = new List<string>();
        var recovery = new Recovery(new(0, 1, 1, 0, 0, []), trace);
        var clock = new Clock();
        var options = new DurableBrokerProcessorOptions(2, 3, TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        var service = new PaperExecutionRecoveryService(recovery, new Audit(trace), new Accounts(trace, true),
            new(new WorkStore(trace), new WorkDispatcher(), clock, "recovery-worker", options),
            new(new InboxStore(trace), new InboxDispatcher(), clock, "recovery-worker", options),
            clock, new Ids(), new(4, 2), new CancelAtOutbox(cancellation));

        Assert.That(async () => await service.RecoverAndDrainAsync(cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(trace, Does.Contain("recover"));
            Assert.That(trace, Does.Not.Contain("outbox"));
            Assert.That(trace, Does.Not.Contain("inbox"));
        });
    }

    private static PaperExecutionRecoveryService Service(IPaperExecutionRecoveryRepository recovery,
        IBrokerReconciliationRepository audit, IPaperBrokerAccountReconciler accounts,
        IOrderWorkRepository work, IBrokerInboxRepository inbox)
    {
        var clock = new Clock();
        var options = new DurableBrokerProcessorOptions(2, 3, TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        return new(recovery, audit, accounts,
            new(work, new WorkDispatcher(), clock, "recovery-worker", options),
            new(inbox, new InboxDispatcher(), clock, "recovery-worker", options),
            clock, new Ids(), new(4, 2));
    }

    private sealed class Recovery(PaperExecutionRecoveryResult result, List<string> trace)
        : IPaperExecutionRecoveryRepository
    {
        public Task<PaperExecutionRecoveryResult> RecoverAsync(PaperExecutionRecoveryRequest request,
            CancellationToken token)
        {
            trace.Add("recover");
            return Task.FromResult(result);
        }
    }

    private sealed class Accounts(List<string> trace, bool result) : IPaperBrokerAccountReconciler
    {
        public Task<bool> ReconcileAsync(BrokerAccountId accountId, CancellationToken token)
        {
            trace.Add("account");
            return Task.FromResult(result);
        }
    }

    private sealed class Audit(List<string> trace) : IBrokerReconciliationRepository
    {
        public List<BrokerReconciliationRecord> Records { get; } = [];
        public Task<PersistenceWriteResult> AppendAsync(BrokerReconciliationRecord value, CancellationToken token)
        {
            trace.Add("audit"); Records.Add(value); return Success();
        }
        public Task<IReadOnlyList<BrokerReconciliationRecord>> ListAsync(BrokerAccountId account,
            CancellationToken token) => Task.FromResult<IReadOnlyList<BrokerReconciliationRecord>>(Records);
    }

    private sealed class WorkStore(List<string> trace) : IOrderWorkRepository
    {
        public Task<IReadOnlyList<OrderWorkEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease,
            CancellationToken token)
        { trace.Add("outbox"); return Task.FromResult<IReadOnlyList<OrderWorkEnvelope>>([]); }
        public Task<PersistenceWriteResult> EnqueueAsync(OrderWorkEnvelope value, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> CompleteAsync(OrderWorkItemId id, string owner, string result, DateTimeOffset at, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> RetryAsync(OrderWorkItemId id, string owner, string errorCode, DateTimeOffset availableAt, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> RenewAsync(OrderWorkItemId id, string owner, DateTimeOffset expiresAt, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> FailAsync(OrderWorkItemId id, string owner, string errorCode, DateTimeOffset failedAt, CancellationToken token) => Success();
    }

    private sealed class InboxStore(List<string> trace) : IBrokerInboxRepository
    {
        public Task<IReadOnlyList<BrokerInboxEnvelope>> ClaimAsync(int limit, DateTimeOffset now, DurableWorkLease lease,
            CancellationToken token)
        { trace.Add("inbox"); return Task.FromResult<IReadOnlyList<BrokerInboxEnvelope>>([]); }
        public Task<PersistenceWriteResult> ReceiveAsync(BrokerInboxEnvelope value, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> CompleteAsync(BrokerMessageId id, string owner, string result, DateTimeOffset at, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> RetryAsync(BrokerMessageId id, string owner, string errorCode, DateTimeOffset availableAt, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> RenewAsync(BrokerMessageId id, string owner, DateTimeOffset expiresAt, CancellationToken token) => Success();
        public Task<PersistenceWriteResult> FailAsync(BrokerMessageId id, string owner, string errorCode, DateTimeOffset failedAt, CancellationToken token) => Success();
    }

    private sealed class WorkDispatcher : IOrderWorkDispatcher
    { public Task<DurableBrokerDispatchResult> DispatchAsync(OrderWorkEnvelope work, CancellationToken token) => Task.FromResult(new DurableBrokerDispatchResult(DurableBrokerDispatchDisposition.Completed, DurableBrokerProcessingCodes.Completed)); }
    private sealed class InboxDispatcher : IBrokerInboxDispatcher
    { public Task<DurableBrokerDispatchResult> DispatchAsync(BrokerInboxEnvelope message, CancellationToken token) => Task.FromResult(new DurableBrokerDispatchResult(DurableBrokerDispatchDisposition.Completed, DurableBrokerProcessingCodes.Completed)); }
    private sealed class Clock : IOrderExecutionClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IOrderExecutionIdentifierSource
    {
        public OrderId NewOrderId() => OrderId.New();
        public OrderTransitionId NewTransitionId() => OrderTransitionId.New();
        public FillId NewFillId() => FillId.New();
        public OrderWorkItemId NewWorkItemId() => OrderWorkItemId.New();
        public BrokerMessageId NewBrokerMessageId() => BrokerMessageId.New();
        public CorrelationIdentity NewCorrelationId() => new("recovery-correlation");
    }
    private sealed class CancelAtOutbox(CancellationTokenSource cancellation) : IPaperExecutionRecoveryObserver
    {
        public Task CheckpointAsync(string checkpoint, CancellationToken token)
        {
            if (checkpoint == PaperExecutionRecoveryCheckpoints.BeforeOutboxDrain) cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
    private static Task<PersistenceWriteResult> Success() =>
        Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded());
}
