using Trading.UI.Wpf.Services;
using Trading.UI.Wpf.ViewModels;
using Trading.UI.Wpf.Views;

namespace Trading.UI.Wpf.Navigation;

public sealed class WpfNavigationPageFactory(Func<BotManagementViewModel> createBots,
    Func<BotRunsViewModel> createRuns,
    Func<ResearchCatalogViewModel>? createResearch = null,
    Func<ExecutionRiskAuditViewModel>? createExecution = null,
    Func<ProposalReviewViewModel>? createProposals = null,
    Func<KillSwitchViewModel>? createKillSwitches = null,
    IOperatorUpdateSource? updates = null,
    IUiDispatcher? dispatcher = null) : INavigationPageFactory
{
    public INavigationPage Create(ShellRoute route)
    {
        if (route.Key == "bots")
        {
            var bots = createBots();
            return CreateLive(new BotManagementView { DataContext = bots }, bots.RefreshAsync, bots,
                OperatorUpdateKind.Bots);
        }
        if (route.Key == "runs")
        {
            var runs = createRuns();
            return CreateLive(new BotRunsView { DataContext = runs }, runs.RefreshAsync, runs,
                OperatorUpdateKind.Runs, OperatorUpdateKind.Warnings);
        }
        if (route.Key == "research" && createResearch is not null)
        {
            var research = createResearch();
            return CreateLive(new ResearchCatalogView { DataContext = research }, research.RefreshAsync, research,
                OperatorUpdateKind.Research);
        }
        if ((route.Key == "execution" || route.Key == "risk") && createExecution is not null)
        {
            var execution = createExecution();
            return CreateLive(new ExecutionRiskAuditView { DataContext = execution }, execution.RefreshAsync, execution,
                OperatorUpdateKind.Orders, OperatorUpdateKind.Fills, OperatorUpdateKind.Positions,
                OperatorUpdateKind.Reconciliation, OperatorUpdateKind.Warnings);
        }
        if (route.Key == "proposals" && createProposals is not null)
        {
            var proposals = createProposals();
            return CreateLive(new ProposalReviewView { DataContext = proposals }, proposals.RefreshAsync, proposals,
                OperatorUpdateKind.Proposals);
        }
        if (route.Key == "settings" && createKillSwitches is not null)
        {
            var killSwitches = createKillSwitches();
            return CreateLive(new KillSwitchView { DataContext = killSwitches }, killSwitches.RefreshAsync, killSwitches,
                OperatorUpdateKind.Switches, OperatorUpdateKind.Warnings);
        }
        return new Placeholder(route.Title);
    }

    private INavigationPage CreateLive(object content, Func<CancellationToken, Task> load,
        IAsyncDisposable lifetime, params OperatorUpdateKind[] kinds)
    {
        if (updates is null || dispatcher is null) return new Page(content, load, lifetime);
        return new LivePage(content, load, lifetime,
            new LiveWorkspaceUpdater(updates, dispatcher, kinds.ToHashSet(), load));
    }

    private sealed class Page(object content, Func<CancellationToken, Task> load, IAsyncDisposable lifetime) : INavigationPage
    {
        public object Content { get; } = content;
        public async ValueTask LoadAsync(CancellationToken cancellationToken) => await load(cancellationToken);
        public ValueTask DisposeAsync() => lifetime.DisposeAsync();
    }

    private sealed class LivePage(object content, Func<CancellationToken, Task> load,
        IAsyncDisposable lifetime, LiveWorkspaceUpdater updater) : INavigationPage
    {
        public object Content { get; } = content;
        public async ValueTask LoadAsync(CancellationToken cancellationToken)
        {
            await load(cancellationToken);
            updater.Start();
        }
        public async ValueTask DisposeAsync()
        {
            await updater.DisposeAsync();
            await lifetime.DisposeAsync();
        }
    }

    private sealed class Placeholder(string title) : INavigationPage
    {
        public object Content { get; } = $"{title} workspace";
        public ValueTask LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
