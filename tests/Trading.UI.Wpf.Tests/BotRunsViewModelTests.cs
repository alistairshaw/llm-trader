using System.Collections.Immutable;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Policies;
using Trading.Engine.Operators;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Tests;

[TestFixture]
[Category("BotRuns")]
public sealed class BotRunsViewModelTests
{
    private static readonly TradingBotId BotId = TradingBotId.Parse("01J5QH8M000000000000000801");
    private static readonly BotRunId RunId = BotRunId.Parse("01J5QH8M000000000000000802");
    private static readonly BotRunTriggerId TriggerId = BotRunTriggerId.Parse("01J5QH8M000000000000000803");
    private static readonly TradingBotConfigurationVersionId ConfigurationId = TradingBotConfigurationVersionId.Parse("01J5QH8M000000000000000804");
    private static readonly PortfolioDecisionSnapshotId SnapshotId = PortfolioDecisionSnapshotId.Parse("01J5QH8M000000000000000805");
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly OperatorPrincipal Principal = new("operator", [OperatorAuthority.ReadOperations, OperatorAuthority.TriggerRuns]);

    [Test]
    public async Task RefreshSeparatesActiveQueuedAndTerminalWorkAndLoadsAuditDetail()
    {
        var gateway = new Gateway();
        await using var viewModel = new BotRunsViewModel(gateway, gateway, Principal);

        await viewModel.RefreshAsync();
        viewModel.SelectedRun = viewModel.History.Single();
        await viewModel.LoadDetailAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.ActiveRuns.Single().Status, Is.EqualTo(BotRunStatus.Reasoning));
            Assert.That(viewModel.QueuedTriggers.Single().TriggerType, Is.EqualTo(BotRunTriggerType.Manual));
            Assert.That(viewModel.History.Single().Status, Is.EqualTo(BotRunStatus.Faulted));
            Assert.That(viewModel.Detail?.ConfigurationVersionId, Is.EqualTo(ConfigurationId));
            Assert.That(viewModel.Detail?.PortfolioSnapshotId, Is.EqualTo(SnapshotId));
            Assert.That(viewModel.Detail?.FailureCode, Is.EqualTo("runtime.recovered_expired_lease"));
            Assert.That(viewModel.Detail?.AcceptedNextRunAt, Is.EqualTo(Now.AddHours(2)));
            Assert.That(viewModel.Detail?.WasRecovered, Is.True);
        }
    }

    [Test]
    public async Task LeavingPageDoesNotCancelDurableManualTriggerSubmission()
    {
        var gateway = new Gateway { BlockTrigger = true };
        var viewModel = new BotRunsViewModel(gateway, gateway, Principal)
        {
            TradingBotId = BotId.ToString(),
            Reason = "operator review",
        };

        var submission = viewModel.TriggerAsync();
        await gateway.TriggerStarted.Task;
        await viewModel.DisposeAsync();
        gateway.ReleaseTrigger.TrySetResult();
        await submission;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gateway.TriggerCalls, Is.EqualTo(1));
            Assert.That(gateway.TriggerCancellation.CanBeCanceled, Is.False);
            Assert.That(viewModel.SubmissionCode, Is.EqualTo("bot_run.trigger_accepted"));
            Assert.That(viewModel.ErrorCode, Is.Null);
        }
    }

    [Test]
    public async Task CoalescedAndBlockedResultsRemainVisibleAsStableOutcomes()
    {
        var gateway = new Gateway { TriggerResult = new(OperatorResultStatus.Conflict, "bot_run.coalesced", RunId.ToString()) };
        await using var viewModel = new BotRunsViewModel(gateway, gateway, Principal)
        {
            TradingBotId = BotId.ToString(),
            Reason = "duplicate",
        };

        await viewModel.TriggerAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.SubmissionCode, Is.EqualTo("bot_run.coalesced"));
            Assert.That(viewModel.ErrorCode, Is.EqualTo("bot_run.coalesced"));
            Assert.That(gateway.TriggerCalls, Is.EqualTo(1));
        }
    }

    private sealed class Gateway : IOperatorQueries, IRunOperatorService
    {
        public int TriggerCalls { get; private set; }
        public bool BlockTrigger { get; set; }
        public CancellationToken TriggerCancellation { get; private set; }
        public OperatorCommandResult TriggerResult { get; set; } = new(OperatorResultStatus.Succeeded, "bot_run.trigger_accepted", TriggerId.ToString());
        public TaskCompletionSource TriggerStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseTrigger { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<OperatorQueryResult<OperatorOverview>> GetOverviewAsync(OperatorPrincipal principal,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OperatorQueryResult<OperatorPage<T>>> GetPageAsync<T>(OperatorPrincipal principal,
            OperatorPageKind page, OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken)
        {
            object result = typeof(T) == typeof(QueuedRunTriggerSummary)
                ? new OperatorPage<QueuedRunTriggerSummary>([new(TriggerId, BotId, BotRunTriggerType.Manual, "operator review", Now, "operator")], 0, null)
                : typeof(T) == typeof(RunDetail)
                    ? new OperatorPage<RunDetail>([Detail()], 0, null)
                    : new OperatorPage<RunSummary>([filter.Status == "active" ? Summary(BotRunStatus.Reasoning, null) : Summary(BotRunStatus.Faulted, Now.AddMinutes(5))], 0, null);
            return Task.FromResult(new OperatorQueryResult<OperatorPage<T>>(OperatorResultStatus.Succeeded, (OperatorPage<T>)result));
        }

        public async Task<OperatorCommandResult> TriggerAsync(OperatorPrincipal principal, TradingBotId id,
            string reason, CancellationToken cancellationToken)
        {
            TriggerCalls++;
            TriggerCancellation = cancellationToken;
            TriggerStarted.TrySetResult();
            if (BlockTrigger) await ReleaseTrigger.Task;
            return TriggerResult;
        }

        private static RunSummary Summary(BotRunStatus status, DateTimeOffset? completedAt) =>
            new(RunId, BotId, status, Now, completedAt, 2, 1.25m);

        private static RunDetail Detail()
        {
            var usage = new Usage(TimeSpan.FromMinutes(5), 1200, new Money(1.25m, Currency.USD), 2, 1, 0);
            return new(Summary(BotRunStatus.Faulted, Now.AddMinutes(5)), ConfigurationId, SnapshotId,
                [new(TriggerId, BotRunTriggerType.Manual, "operator review", Now, "operator")],
                [new("Tokens", "2000", "1200")], usage, null, Now.AddHours(1), Now.AddHours(2),
                "runtime.recovered_expired_lease", true);
        }
    }
}
