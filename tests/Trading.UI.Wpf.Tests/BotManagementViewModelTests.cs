using System.Collections.Immutable;
using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Engine.Operators;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf.Tests;

[TestFixture]
[Category("BotManagement")]
public sealed class BotManagementViewModelTests
{
    private static readonly TradingBotId BotId = TradingBotId.Parse("01J5QH8M000000000000000701");
    private static readonly TradingBotConfigurationVersionId ConfigurationId = TradingBotConfigurationVersionId.Parse("01J5QH8M000000000000000702");
    private static readonly OperatorPrincipal Principal = new("operator", [OperatorAuthority.ReadOperations, OperatorAuthority.ManageBots]);

    [Test]
    public async Task SuccessfulCommandRefreshesDurableSummaryAndClearsConfirmation()
    {
        var gateway = new Gateway(Summary(0));
        await using var viewModel = new BotManagementViewModel(gateway, gateway, Principal) { Name = "Income", Confirmation = "RETIRE" };

        await viewModel.CreateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gateway.Calls, Is.EqualTo(1));
            Assert.That(viewModel.Items.Single().Version, Is.EqualTo(1));
            Assert.That(viewModel.Confirmation, Is.Null);
            Assert.That(viewModel.ErrorCode, Is.Null);
        }
    }

    [TestCase(OperatorResultStatus.Invalid, "operator.validation")]
    [TestCase(OperatorResultStatus.Unavailable, "operator.unavailable")]
    [TestCase(OperatorResultStatus.Conflict, "operator.concurrency")]
    public async Task FailedCommandPreservesInputAndShowsStableActionableCode(OperatorResultStatus status, string code)
    {
        var gateway = new Gateway(Summary(3)) { CommandResult = new(status, code) };
        await using var viewModel = new BotManagementViewModel(gateway, gateway, Principal) { Name = "Preserve me" };

        await viewModel.CreateAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.Name, Is.EqualTo("Preserve me"));
            Assert.That(viewModel.ErrorCode, Is.EqualTo(code));
            Assert.That(viewModel.Items, Is.Empty);
        }
    }

    [Test]
    public async Task RetirementAndModePromotionRequireExplicitDistinctConfirmation()
    {
        var gateway = new Gateway(Summary(2));
        await using var viewModel = new BotManagementViewModel(gateway, gateway, Principal);
        await viewModel.RefreshAsync();
        viewModel.SelectedBot = viewModel.Items.Single();

        await viewModel.ChangeLifecycleAsync(OperatorCommandKind.RetireBot);
        Assert.That(viewModel.ErrorCode, Is.EqualTo("bot_management.retirement_confirmation_required"));

        gateway.Detail = new BotDetail(Summary(2), ExecutionMode.ResearchOnly, null, ImmutableArray<OperatorWarning>.Empty);
        await viewModel.LoadDetailAsync();
        viewModel.ExecutionMode = ExecutionMode.PaperTrading;
        await viewModel.SaveConfigurationAsync();
        Assert.That(viewModel.ErrorCode, Is.EqualTo("bot_management.mode_confirmation_required"));

        viewModel.Confirmation = "PAPERTRADING";
        await viewModel.SaveConfigurationAsync();
        Assert.That(gateway.Calls, Is.EqualTo(1));
        Assert.That(viewModel.ExecutionModes, Is.EqualTo(new[] { ExecutionMode.ResearchOnly, ExecutionMode.HumanApproval, ExecutionMode.PaperTrading }));
    }

    [Test]
    public async Task CancellationEndsBusyStateWithStableResult()
    {
        var gateway = new Gateway(Summary(0)) { BlockQuery = true };
        await using var viewModel = new BotManagementViewModel(gateway, gateway, Principal);
        using var cancellation = new CancellationTokenSource();

        var loading = viewModel.RefreshAsync(cancellation.Token);
        await gateway.QueryStarted.Task;
        cancellation.Cancel();
        await loading;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.ErrorCode, Is.EqualTo("bot_management.cancelled"));
        }
    }

    private static BotSummary Summary(long version) => new(BotId, "Income", TradingBotStatus.Paused, null, ConfigurationId, version);

    private sealed class Gateway(BotSummary summary) : IOperatorQueries, IBotOperatorService
    {
        public OperatorCommandResult CommandResult { get; set; } = new(OperatorResultStatus.Succeeded, "operator.succeeded");
        public BotDetail? Detail { get; set; }
        public bool BlockQuery { get; set; }
        public int Calls { get; private set; }
        public TaskCompletionSource QueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<OperatorQueryResult<OperatorOverview>> GetOverviewAsync(OperatorPrincipal principal, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<OperatorQueryResult<OperatorPage<T>>> GetPageAsync<T>(OperatorPrincipal principal,
            OperatorPageKind page, OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken)
        {
            QueryStarted.TrySetResult();
            if (BlockQuery) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            object value = typeof(T) == typeof(BotDetail)
                ? new OperatorPage<BotDetail>(Detail is null ? [] : [Detail], 0, null)
                : new OperatorPage<BotSummary>([summary with { Version = summary.Version + Calls }], 0, null);
            return new(OperatorResultStatus.Succeeded, (OperatorPage<T>)value);
        }

        public Task<OperatorCommandResult> CreateAsync(OperatorPrincipal principal, string name, CancellationToken cancellationToken) => Complete();
        public Task<OperatorCommandResult> ConfigureAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion, BotConfigurationInput configuration, CancellationToken cancellationToken) => Complete();
        public Task<OperatorCommandResult> AssignAsync(OperatorPrincipal principal, TradingBotId id, PortfolioId portfolioId, long expectedVersion, CancellationToken cancellationToken) => Complete();
        public Task<OperatorCommandResult> PauseAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion, CancellationToken cancellationToken) => Complete();
        public Task<OperatorCommandResult> ResumeAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion, CancellationToken cancellationToken) => Complete();
        public Task<OperatorCommandResult> RetireAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion, CancellationToken cancellationToken) => Complete();
        private Task<OperatorCommandResult> Complete() { Calls++; return Task.FromResult(CommandResult); }
    }
}
