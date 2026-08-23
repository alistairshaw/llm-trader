using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Engine.Operators;
using Trading.Engine.Runtime;
using Trading.Host;
using Trading.UI.Wpf.Navigation;
using Trading.UI.Wpf.Services;
using Trading.UI.Wpf.ViewModels;

namespace Trading.UI.Wpf;

public partial class App : Application, IAsyncDisposable
{
    private TradingApplicationLifecycle? lifecycle;
    private WpfTestProfileRuntime? testProfile;
    private bool closing;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        testProfile = WpfTestProfileRuntime.FromEnvironment();
        var dataDirectory = testProfile?.DataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlmTrader");
        var configuration = testProfile?.Configuration ?? new Dictionary<string, string?>
        {
            ["Trading:Mode"] = "Simulated",
            ["Trading:DataDirectory"] = dataDirectory,
            ["Trading:OperatorMode"] = "true",
            ["Research:Mode"] = "Fixture",
        };
        var host = HostBootstrap.Build(e.Args, builder => builder.Configuration.AddInMemoryCollection(configuration));
        lifecycle = new(host, TimeSpan.FromSeconds(30));
        var startupPhase = "host-start";
        try
        {
            await lifecycle.StartAsync(CancellationToken.None);
            startupPhase = "service-resolution";
            var queries = lifecycle.Services.GetService<IOperatorQueries>();
            var botService = lifecycle.Services.GetService<IBotOperatorService>();
            var runService = lifecycle.Services.GetService<IRunOperatorService>();
            var researchService = lifecycle.Services.GetService<IResearchOperatorService>();
            var proposalService = lifecycle.Services.GetService<IProposalOperatorService>();
            var killSwitchService = lifecycle.Services.GetService<IKillSwitchOperatorService>();
            var portfolioQueries = lifecycle.Services.GetService<IOperatorPortfolioBrokerQueries>();
            var executionQueries = lifecycle.Services.GetService<IOrderExecutionQueries>();
            var principal = lifecycle.Services.GetService<OperatorPrincipal>();
            var clock = lifecycle.Services.GetService<IUtcClock>();
            var updates = new PollingOperatorUpdateSource(TimeSpan.FromSeconds(2));
            var dispatcher = new WpfUiDispatcher(Dispatcher);
            startupPhase = "window-construction";
            var window = queries is not null && botService is not null && runService is not null && researchService is not null && proposalService is not null && killSwitchService is not null && portfolioQueries is not null && executionQueries is not null && principal is not null && clock is not null
                ? new MainWindow(new WpfNavigationPageFactory(
                    () => new BotManagementViewModel(queries, botService, principal),
                    () => new BotRunsViewModel(queries, runService, principal),
                    () => new PortfolioBrokerViewModel(new AuthorizedPortfolioBrokerViewSource(portfolioQueries,
                        TradingBotId.Parse("01J5QH8M000000000000000101"),
                        BrokerAccountId.Parse("01J5QH8M000000000000000302")),
                        new ClockTimeProvider(clock)),
                    () => new ResearchCatalogViewModel(queries, researchService, principal),
                    () => new ExecutionRiskAuditViewModel(executionQueries,
                        new ExecutionQueryPrincipal(principal.ActorId, false,
                            [TradingBotId.Parse("01J5QH8M000000000000000101"), TradingBotId.Parse("01J5QH8M000000000000000201")],
                            [PortfolioId.Parse("01J5QH8M000000000000000103"), PortfolioId.Parse("01J5QH8M000000000000000203")],
                            [BrokerAccountId.Parse("01J5QH8M000000000000000302"), BrokerAccountId.Parse("01J5QH8M000000000000000303")])),
                    createProposals: () => new ProposalReviewViewModel(queries, proposalService, principal,
                        () => clock.UtcNow),
                    createKillSwitches: () => new KillSwitchViewModel(queries, killSwitchService, principal),
                    updates: updates,
                    dispatcher: dispatcher))
                : new MainWindow();
            window.Closing += OnMainWindowClosing;
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            startupPhase = "window-show";
            window.Show();
            testProfile?.PublishReady();
        }
        catch (Exception exception)
        {
            testProfile?.PublishFailed($"{startupPhase}.{exception.GetType().Name}.{exception.Message}");
            MessageBox.Show($"Trading Bot could not start.\n\n{exception.Message}", "Startup failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        closing = true;
        if (lifecycle is not null) await lifecycle.StopAsync();
        testProfile?.PublishStopped();
        if (sender is Window window)
        {
            window.Closing -= OnMainWindowClosing;
            window.Close();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        lifecycle?.StopAsync().GetAwaiter().GetResult();
        testProfile?.PublishStopped();
        base.OnExit(e);
    }

    public async ValueTask DisposeAsync()
    {
        if (lifecycle is not null) await lifecycle.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private sealed class ClockTimeProvider(IUtcClock clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => clock.UtcNow;
    }
}
