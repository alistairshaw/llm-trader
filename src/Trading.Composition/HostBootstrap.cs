using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trading.Brokers.Simulation;
using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Operations;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Core.Proposals;
using Trading.Data;
using Trading.Engine.Execution;
using Trading.Engine.Operations;
using Trading.Engine.Proposals;
using Trading.Engine.Runtime;
using Trading.Research;
using Trading.Research.Contracts;
using Trading.Research.Sources;

namespace Trading.Host;

public sealed class TradingHostOptions
{
    public string Mode { get; init; } = "Simulated";
    public string DataDirectory { get; init; } = "/data";
    public bool SmokeMode { get; init; }
    public bool ExecutePaperSmoke { get; init; } = true;
    public bool OperatorMode { get; init; }
    public int GlobalRunConcurrency { get; init; } = 1;
    public int QueueCapacity { get; init; } = 16;
    public int LeaseSeconds { get; init; } = 300;
    public int ShutdownSeconds { get; init; } = 30;
    public string[] BotIds { get; init; } = [];

    public void Validate()
    {
        if (!string.Equals(Mode, "Simulated", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Trading:Mode must be Simulated for the Stage 3 host.");
        if (string.IsNullOrWhiteSpace(DataDirectory) || !Path.IsPathFullyQualified(DataDirectory)) throw new InvalidOperationException("Trading:DataDirectory must be an absolute path outside the source tree.");
        if (GlobalRunConcurrency is < 1 or > 32) throw new InvalidOperationException("Trading:GlobalRunConcurrency must be between 1 and 32.");
        if (QueueCapacity is < 1 or > 10000) throw new InvalidOperationException("Trading:QueueCapacity must be between 1 and 10000.");
        if (LeaseSeconds is < 10 or > 3600) throw new InvalidOperationException("Trading:LeaseSeconds must be between 10 and 3600.");
        if (ShutdownSeconds is < 1 or > 300) throw new InvalidOperationException("Trading:ShutdownSeconds must be between 1 and 300.");
        foreach (var id in BotIds) _ = TradingBotId.Parse(id);
        if (!SmokeMode && !OperatorMode && BotIds.Length == 0) throw new InvalidOperationException("Trading:BotIds must contain at least one configured Bot.");
    }
}

public sealed class ResearchHostOptions
{
    public string Mode { get; init; } = "Fixture";
    public string FixtureVersion { get; init; } = "v1";
    public string ModelProvider { get; init; } = "scripted";
    public string ModelId { get; init; } = "research";
    public string ModelVersion { get; init; } = "1";
    public string PromptVersion { get; init; } = "prompt-v1";
    public string ToolSetVersion { get; init; } = "tools-v1";
    public string ReportSchemaVersion { get; init; } = "1";
    public int GlobalConcurrency { get; init; } = 2;
    public int QueueBatchSize { get; init; } = 20;
    public int NotificationBatchSize { get; init; } = 50;
    public int NotificationAttempts { get; init; } = 3;
    public int WallClockSeconds { get; init; } = 120;
    public int TokenLimit { get; init; } = 4000;
    public int ToolCallLimit { get; init; } = 12;
    public int DocumentLimit { get; init; } = 4;
    public int RetainedByteLimit { get; init; } = 100000;
    public int ConsecutiveFailureLimit { get; init; } = 3;
    public int OrphanAgeSeconds { get; init; } = 600;

    public void Validate()
    {
        if (Mode != "Fixture") throw new InvalidOperationException("Research:Mode must be Fixture; the local host does not enable network Research providers.");
        if (FixtureVersion != "v1") throw new InvalidOperationException("Research:FixtureVersion must identify the embedded v1 fixture set.");
        if (ModelProvider != "scripted" || ModelId != "research") throw new InvalidOperationException("Research model configuration must use the scripted research client.");
        if (new[] { ModelVersion, PromptVersion, ToolSetVersion, ReportSchemaVersion }.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("Research version pins are required.");
        if (GlobalConcurrency is < 1 or > 16 || QueueBatchSize is < 1 or > 100 || NotificationBatchSize is < 1 or > 1000 || NotificationAttempts is < 1 or > 10) throw new InvalidOperationException("Research capacity and batch options are outside safe bounds.");
        if (WallClockSeconds is < 1 or > 900 || TokenLimit is < 1 or > 100000 || ToolCallLimit is < 1 or > 100 || DocumentLimit is < 1 or > 20 || RetainedByteLimit is < 1 or > 1000000 || ConsecutiveFailureLimit is < 1 or > 10 || OrphanAgeSeconds is < 1 or > 86400) throw new InvalidOperationException("Research budget or recovery options are outside safe bounds.");
    }
}

public enum RuntimeStartupState { Starting, Ready, Failed, Stopped }

public sealed class RuntimeReadiness
{
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int state;

    public bool IsReady => State == RuntimeStartupState.Ready;
    public RuntimeStartupState State => (RuntimeStartupState)Volatile.Read(ref state);
    public Exception? Failure { get; private set; }

    public Task WaitForReadyAsync(CancellationToken cancellationToken) => completion.Task.WaitAsync(cancellationToken);

    internal void MarkReady()
    {
        if (Interlocked.CompareExchange(ref state, (int)RuntimeStartupState.Ready, (int)RuntimeStartupState.Starting) == (int)RuntimeStartupState.Starting)
            completion.TrySetResult();
    }

    internal void MarkFailed(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (Interlocked.CompareExchange(ref state, (int)RuntimeStartupState.Failed, (int)RuntimeStartupState.Starting) == (int)RuntimeStartupState.Starting)
        {
            Failure = failure;
            completion.TrySetException(failure);
        }
    }

    internal void MarkStopped()
    {
        var prior = Interlocked.Exchange(ref state, (int)RuntimeStartupState.Stopped);
        if (prior == (int)RuntimeStartupState.Starting) completion.TrySetCanceled();
    }
}

public sealed record HostDatabaseOwner(string Name, string DisposalBoundary);

public sealed class HostDatabaseIdentity
{
    internal HostDatabaseIdentity(string databasePath, string connectionString)
    {
        DatabasePath = databasePath;
        ConnectionString = connectionString;
    }

    public string DatabasePath { get; }
    public string ConnectionString { get; }
    public string DiagnosticIdentity
    {
        get
        {
            var value = new SqliteConnectionStringBuilder(ConnectionString);
            return $"path={DatabasePath};mode={value.Mode};cache={value.Cache};pooling={value.Pooling};timeout={value.DefaultTimeout};owners={string.Join(',', Owners.Select(x => x.Name))}";
        }
    }
    public IReadOnlyList<HostDatabaseOwner> Owners { get; } =
    [
        new("TradingDbContext registration and scoped repositories", "scope disposal, then host/root-provider disposal"),
        new("TradingRuntimeHostedService smoke scope", "hosted-service completion and host disposal"),
        new("external smoke inspection", "inspection connection disposal before exact-pool cleanup"),
    ];
}

public static class HostBootstrap
{
    public static IHost Build(string[] args, Action<IHostApplicationBuilder>? configure = null)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        configure?.Invoke(builder);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        var options = builder.Configuration.GetSection("Trading").Get<TradingHostOptions>() ?? new();
        var research = builder.Configuration.GetSection("Research").Get<ResearchHostOptions>() ?? new();
        options.Validate();
        research.Validate();
        Directory.CreateDirectory(options.DataDirectory);
        var databaseOptions = new DatabaseOptions
        {
            DatabasePath = Path.Combine(options.DataDirectory, options.SmokeMode ? "smoke.db" : "trading.db"),
        };
        var path = databaseOptions.ValidateAndGetFullPath(AppContext.BaseDirectory);
        var databaseIdentity = new HostDatabaseIdentity(
            path,
            TradingDbContextFactory.CreateConnectionString(databaseOptions, AppContext.BaseDirectory));
        if (options.SmokeMode)
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" }) File.Delete(path + suffix);
        }
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(research);
        builder.Services.AddSingleton(databaseOptions);
        builder.Services.AddSingleton(databaseIdentity);
        builder.Services.AddSingleton<RuntimeReadiness>();
        builder.Services.AddSingleton<HostClock>(_ => new(options.SmokeMode ? new DateTimeOffset(2026, 8, 20, 23, 0, 0, TimeSpan.Zero) : null));
        builder.Services.AddSingleton<IUtcClock>(x => x.GetRequiredService<HostClock>());
        builder.Services.AddSingleton<IResearchClock>(x => x.GetRequiredService<HostClock>());
        builder.Services.AddSingleton<IRuntimeIdentifierGenerator, RuntimeIdentifiers>();
        builder.Services.AddDbContext<TradingDbContext>(x => x.UseSqlite(databaseIdentity.ConnectionString));
        builder.Services.AddScoped<DatabaseInitializer>();
        builder.Services.AddScoped<IKillSwitchStore, KillSwitchStore>();
        builder.Services.AddScoped<KillSwitchEnforcement>();
        builder.Services.AddScoped<ITradingBotRepository, TradingBotRepository>();
        builder.Services.AddScoped<IBrokerConnectionRepository, BrokerConnectionRepository>();
        builder.Services.AddScoped<IBrokerAccountRepository, BrokerAccountRepository>();
        builder.Services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        builder.Services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        builder.Services.AddScoped<IPortfolioDecisionSnapshotRepository, PortfolioDecisionSnapshotRepository>();
        builder.Services.AddScoped<IPortfolioQueries, PortfolioQueries>();
        builder.Services.AddScoped<IBotRunRepository, BotRunRepository>();
        builder.Services.AddScoped<IBotRunInputAuditWriter>(x => (BotRunRepository)x.GetRequiredService<IBotRunRepository>());
        builder.Services.AddScoped<IBotRunTriggerRepository, BotRunTriggerRepository>();
        builder.Services.AddScoped<BotTriggerIngestionService>();
        builder.Services.AddScoped<BotTriggerCoalescingService>();
        builder.Services.AddScoped<IBotRunInputService, BotRunInputService>();
        builder.Services.AddScoped<StageThreeToolDispatcher>();
        builder.Services.AddScoped<IModelLoop, BoundedModelLoop>();
        builder.Services.AddScoped<DeterministicSchedulingPolicy>();
        builder.Services.AddScoped<RuntimeRecoveryService>();
        builder.Services.AddScoped<BotRunOrchestrationService>();
        builder.Services.AddScoped<IHypothesisRepository, HypothesisRepository>();
        builder.Services.AddScoped<ITradeProposalRepository, TradeProposalRepository>();
        builder.Services.AddScoped<ICapitalReservationRepository, CapitalReservationRepository>();
        builder.Services.AddScoped<IAtomicCapitalReservationRepository, AtomicCapitalReservationRepository>();
        builder.Services.AddScoped<IProposalQueries, ProposalQueries>();
        builder.Services.AddScoped<IOrderExecutionQueries, OrderExecutionQueries>();
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<IOrderWorkRepository, OrderWorkRepository>();
        builder.Services.AddScoped<IBrokerInboxRepository, BrokerInboxRepository>();
        builder.Services.AddScoped<IAtomicOrderConversionRepository, AtomicOrderConversionRepository>();
        builder.Services.AddScoped<IOrderSubmissionRepository, OrderSubmissionRepository>();
        builder.Services.AddScoped<IOrderReconciliationRepository, OrderReconciliationRepository>();
        builder.Services.AddScoped<IBrokerOrderEventRepository, BrokerOrderEventRepository>();
        builder.Services.AddScoped<IFillAccountingRepository, FillAccountingRepository>();
        builder.Services.AddScoped<IPaperExecutionRecoveryRepository, PaperExecutionRecoveryRepository>();
        builder.Services.AddScoped<IBrokerReconciliationRepository, BrokerReconciliationRepository>();
        builder.Services.AddSingleton<PaperSmokeState>();
        builder.Services.AddSingleton<IOrderExecutionClock>(x => x.GetRequiredService<PaperSmokeState>());
        builder.Services.AddSingleton<IOrderExecutionIdentifierSource>(x => x.GetRequiredService<PaperSmokeState>());
        builder.Services.AddSingleton<ISimulatedBrokerClock>(x => x.GetRequiredService<PaperSmokeState>());
        builder.Services.AddSingleton<ISimulatedBrokerIdentifierSource>(x => x.GetRequiredService<PaperSmokeState>());
        builder.Services.AddSingleton<ISimulatedBrokerLatency, ImmediatePaperLatency>();
        builder.Services.AddSingleton(x => new SimulatedPaperBroker(
            BrokerConnectionId.Parse("01J5QH8M000000000000000304"), SmokeFixture.AccountTwoId,
            "Deterministic paper fixture", x.GetRequiredService<ISimulatedBrokerClock>(),
            x.GetRequiredService<ISimulatedBrokerIdentifierSource>(), x.GetRequiredService<ISimulatedBrokerLatency>()));
        builder.Services.AddSingleton<IPaperBrokerGateway>(x => x.GetRequiredService<SimulatedPaperBroker>());
        builder.Services.AddScoped<IOrderConversionService, ProposalOrderConversionService>();
        builder.Services.AddScoped<IPaperBrokerAccountReconciler, SmokePaperAccountReconciler>();
        builder.Services.AddScoped<IOrderWorkDispatcher>(x => new PaperWorkDispatcher(
            new PaperOrderSubmissionDispatcher(x.GetRequiredService<IOrderSubmissionRepository>(),
                x.GetRequiredService<IPaperBrokerGateway>(), x.GetRequiredService<IOrderExecutionClock>(),
                x.GetRequiredService<IOrderExecutionIdentifierSource>(), PaperOrderSubmissionOptions.Default),
            new PaperOrderReconciliationDispatcher(x.GetRequiredService<IOrderReconciliationRepository>(),
                x.GetRequiredService<IPaperBrokerGateway>(), x.GetRequiredService<IOrderExecutionClock>(),
                x.GetRequiredService<IOrderExecutionIdentifierSource>(), PaperOrderReconciliationOptions.Default)));
        builder.Services.AddScoped<IBrokerInboxDispatcher>(x => new PaperInboxDispatcher(
            new BrokerOrderEventDispatcher(x.GetRequiredService<IBrokerOrderEventRepository>(),
                x.GetRequiredService<IOrderExecutionClock>(), "paper-host-worker"),
            new FillAccountingDispatcher(x.GetRequiredService<IFillAccountingRepository>(),
                x.GetRequiredService<IOrderExecutionClock>(), "paper-host-worker")));
        builder.Services.AddScoped(x => new OrderOutboxProcessor(x.GetRequiredService<IOrderWorkRepository>(),
            x.GetRequiredService<IOrderWorkDispatcher>(), x.GetRequiredService<IOrderExecutionClock>(),
            "paper-host-worker", DurableBrokerProcessorOptions.Default));
        builder.Services.AddScoped(x => new BrokerInboxProcessor(x.GetRequiredService<IBrokerInboxRepository>(),
            x.GetRequiredService<IBrokerInboxDispatcher>(), x.GetRequiredService<IOrderExecutionClock>(),
            "paper-host-worker", DurableBrokerProcessorOptions.Default));
        builder.Services.AddScoped(x => new PaperExecutionRecoveryService(
            x.GetRequiredService<IPaperExecutionRecoveryRepository>(), x.GetRequiredService<IBrokerReconciliationRepository>(),
            x.GetRequiredService<IPaperBrokerAccountReconciler>(), x.GetRequiredService<OrderOutboxProcessor>(),
            x.GetRequiredService<BrokerInboxProcessor>(), x.GetRequiredService<IOrderExecutionClock>(),
            x.GetRequiredService<IOrderExecutionIdentifierSource>(), PaperExecutionRecoveryOptions.Default));
        builder.Services.AddSingleton<ProposalSmokeState>();
        builder.Services.AddSingleton<IProposalGovernanceClock>(x => x.GetRequiredService<ProposalSmokeState>());
        builder.Services.AddSingleton<IProposalGovernanceIdentifierSource>(x => x.GetRequiredService<ProposalSmokeState>());
        builder.Services.AddSingleton<IFreshProposalStateProvider>(x => x.GetRequiredService<ProposalSmokeState>());
        builder.Services.AddSingleton<IProposalGovernanceContextProvider>(x => x.GetRequiredService<ProposalSmokeState>());
        builder.Services.AddSingleton<IProposalDecisionAuthorizer, SmokeProposalDecisionAuthorizer>();
        builder.Services.AddScoped<IGuardrailPolicyEvaluator, DeterministicGuardrailPolicyEvaluator>();
        builder.Services.AddScoped<IGuardrailEvaluationService, GuardrailEvaluationService>();
        builder.Services.AddScoped<IHumanProposalDecisionService, HumanProposalDecisionService>();
        builder.Services.AddScoped<ICapitalReservationService, CapitalReservationService>();
        builder.Services.AddScoped<IProposalGovernanceOrchestrator, ProposalGovernanceOrchestrator>();
        AddResearch(builder.Services, research, options.SmokeMode);
        builder.Services.AddScoped<TradingBotResearchToolDispatcher>();
        builder.Services.AddScoped<IToolDispatcher, ProposalToolDispatcher>();
        builder.Services.AddHostedService<TradingRuntimeHostedService>();
        return builder.Build();
    }

    private static void AddResearch(IServiceCollection services, ResearchHostOptions options, bool smoke)
    {
        services.AddSingleton(new ResearchRunDefaults(new(options.ModelProvider, options.ModelId, options.ModelVersion, options.PromptVersion, options.ToolSetVersion, options.ReportSchemaVersion),
            new(TimeSpan.FromSeconds(options.WallClockSeconds), options.TokenLimit, new Money(10, Currency.USD), options.ToolCallLimit, options.DocumentLimit, options.RetainedByteLimit, options.ConsecutiveFailureLimit),
            options.GlobalConcurrency, options.QueueBatchSize, options.NotificationBatchSize, options.NotificationAttempts, TimeSpan.FromSeconds(options.OrphanAgeSeconds)));
        services.AddSingleton(new ResearchToolPolicy(options.ToolSetVersion, StageFourResearchTools.Names.Select(x => new KeyValuePair<string, int>(x, options.ToolCallLimit))));
        services.AddSingleton(new ResearchIdentifiers(smoke));
        services.AddSingleton<IResearchIdentifierSource>(x => x.GetRequiredService<ResearchIdentifiers>());
        services.AddSingleton<IResearchNotificationIdentifierSource>(x => x.GetRequiredService<ResearchIdentifiers>());
        services.AddSingleton<IResearchDelay, ImmediateResearchDelay>();
        services.AddSingleton<IResearchModelSessionFactory, FixtureResearchModelSessionFactory>();
        services.AddScoped<IResearchRequestDecisionRepository, ResearchRequestDecisionRepository>();
        services.AddScoped<IResearchRequestRepository, ResearchRequestRepository>();
        services.AddScoped<IResearchRunAttemptRepository, ResearchRunAttemptRepository>();
        services.AddScoped<IResearchReportRepository, ResearchReportRepository>();
        services.AddScoped<IResearchReportCatalogQueries, ResearchReportCatalogQueries>();
        services.AddScoped<IResearchOrchestrationRepository, ResearchOrchestrationRepository>();
        services.AddScoped<IResearchNotificationRepository, ResearchNotificationRepository>();
        services.AddScoped<IResearchReportCatalog, HostResearchCatalog>();
        services.AddScoped<IResearchArtifactStore, DurableResearchArtifactStore>();
        services.AddScoped<IFixtureResearchSource, FixtureResearchSource>();
        services.AddScoped<IResearchToolDispatcher, ResearchToolDispatcher>();
        services.AddScoped<IResearchDraftValidator, ResearchReportDraftValidator>();
        services.AddScoped<IResearchReportPublisher, ResearchReportPublisher>();
        services.AddScoped<ResearchRequestService>();
        services.AddScoped<ResearchNotificationDeliveryService>();
        services.AddScoped<ResearchRunOrchestrator>();
        services.AddScoped<ResearchRunSupervisor>();
        services.AddScoped<ResearchRestartRecovery>();
    }

    public static async Task RunAsync(string[] args)
    {
        var host = Build(args);
        try
        {
            await host.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            if (host is IAsyncDisposable asyncHost)
                await asyncHost.DisposeAsync().ConfigureAwait(false);
            else
                host.Dispose();
        }
    }
}

internal sealed class HostClock(DateTimeOffset? fixedAt) : IUtcClock, IResearchClock { public DateTimeOffset UtcNow => fixedAt ?? DateTimeOffset.UtcNow; }
internal sealed class RuntimeIdentifiers : IRuntimeIdentifierGenerator
{
    public BotRunId NewBotRunId() => BotRunId.New();
    public BotRunTriggerId NewTriggerId() => BotRunTriggerId.New();
    public ToolInvocationId NewToolInvocationId() => ToolInvocationId.New();
}

internal sealed class TradingRuntimeHostedService(IServiceScopeFactory scopes, TradingHostOptions options,
    RuntimeReadiness readiness, IHostApplicationLifetime lifetime, IUtcClock clock,
    ILogger<TradingRuntimeHostedService> logger) : BackgroundService
{
    private MultiBotSupervisor? supervisor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var services = scope.ServiceProvider;
            await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(stoppingToken);
            if (options.SmokeMode) await SmokeFixture.SeedAsync(services, stoppingToken);
            var researchRecovery = await services.GetRequiredService<ResearchRestartRecovery>().RecoverAsync(stoppingToken);
            var recovery = await services.GetRequiredService<RuntimeRecoveryService>().RecoverExpiredLeasesAsync(stoppingToken);
            var paperRecovery = await services.GetRequiredService<PaperExecutionRecoveryService>().RecoverAndDrainAsync(stoppingToken);
            if (!paperRecovery.IsReady) throw new InvalidOperationException("Paper execution recovery did not reach readiness.");
            var ids = options.SmokeMode ? [SmokeFixture.BotId, SmokeFixture.BotTwoId] : options.BotIds.Select(TradingBotId.Parse).ToArray();
            foreach (var id in ids) await ValidateBotAsync(services, id, stoppingToken);
            supervisor = new MultiBotSupervisor(new MultiBotSupervisorOptions { GlobalRunConcurrency = options.GlobalRunConcurrency, QueueCapacity = options.QueueCapacity }, services.GetRequiredService<BotRunOrchestrationService>());
            readiness.MarkReady();
            RuntimeLogs.Ready(logger, Environment.MachineName, recovery.RecoveredRuns, recovery.FaultedRuns, researchRecovery);
            var completions = new List<Task<BotRunExecutionResult>>();
            foreach (var id in ids)
            {
                if (options.SmokeMode) await services.GetRequiredService<BotTriggerIngestionService>().IngestAsync(
                    new(id, BotRunTriggerType.Manual, "deterministic smoke", clock.UtcNow), stoppingToken);
                var queued = await supervisor.QueueAsync(new(id, Environment.MachineName, TimeSpan.FromSeconds(options.LeaseSeconds), SmokeSession()), stoppingToken);
                if (queued.Completion is not null)
                {
                    completions.Add(queued.Completion);
                    if (options.SmokeMode) _ = await queued.Completion;
                }
            }
            if (options.SmokeMode)
            {
                var results = await Task.WhenAll(completions);
                foreach (var result in results) RuntimeLogs.SmokeResult(logger, result.RunId?.ToString() ?? "none", result.Outcome.ToString());
                if (results.Any(x => x.Outcome != BotRunExecutionOutcome.Completed))
                {
                    Environment.ExitCode = 1;
                    throw new InvalidOperationException("One or more smoke Bots did not complete.");
                }
                await ResearchSmoke.RunAsync(services, logger, stoppingToken);
                var reservation = await ProposalSmoke.RunAsync(services, results, logger, stoppingToken);
                if (options.ExecutePaperSmoke)
                    await PaperSmoke.RunAsync(services, reservation, logger, stoppingToken);
                lifetime.StopApplication();
            }
            else await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            readiness.MarkFailed(exception);
            throw;
        }
        finally { readiness.MarkStopped(); }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        readiness.MarkStopped();
        var activeSupervisor = Interlocked.Exchange(ref supervisor, null);
        if (activeSupervisor is not null)
        {
            var result = await activeSupervisor.ShutdownAsync(TimeSpan.FromSeconds(options.ShutdownSeconds), cancellationToken);
            RuntimeLogs.Stopped(logger, result.CancelledRuns, result.CompletedWithinDeadline);
            await activeSupervisor.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }

    private static ScriptedLlmClient SmokeSession() => new([new ScriptedModelStep.Response(new AssistantResponse(null,
        [new ModelToolCall(ToolInvocationId.New(), StageThreeTools.Finish, 1, "{\"status\":\"Completed\",\"summary\":\"smoke complete\"}")], new ModelUsage(10, 5, 0), null))], new ImmediateDelay());

    private static async Task ValidateBotAsync(IServiceProvider services, TradingBotId id, CancellationToken token)
    {
        var bot = await services.GetRequiredService<ITradingBotRepository>().GetAsync(id, token) ?? throw new InvalidOperationException($"Configured Bot '{id}' does not exist.");
        var queries = services.GetRequiredService<IPortfolioQueries>();
        var portfolios = await queries.GetPortfoliosAsync(new PortfolioQueryFilter(TradingBotId: id), new PageRequest(0, 1), token);
        if (bot.Status != TradingBotStatus.Enabled || bot.ActiveConfigurationVersionId is null || portfolios.Count == 0) throw new InvalidOperationException($"Configured Bot '{id}' must be enabled with an active configuration and Portfolio.");
        var snapshots = await queries.GetDecisionSnapshotsAsync(new PortfolioDecisionSnapshotQueryFilter(TradingBotId: id), new PageRequest(0, 1), token);
        if (snapshots.Count == 0) throw new InvalidOperationException($"Configured Bot '{id}' has no decision snapshot.");
    }
    private sealed class ImmediateDelay : IAsyncDelay { public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.CompletedTask; }
}

internal static partial class RuntimeLogs
{
    [LoggerMessage(1, LogLevel.Information, "Runtime ready HostInstance={HostInstance} RecoveredRuns={RecoveredRuns} FaultedRuns={FaultedRuns} RecoveredResearch={RecoveredResearch}")]
    public static partial void Ready(ILogger logger, string hostInstance, int recoveredRuns, int faultedRuns, int recoveredResearch);
    [LoggerMessage(2, LogLevel.Information, "Smoke BotRun={BotRun} Outcome={Outcome}")]
    public static partial void SmokeResult(ILogger logger, string botRun, string outcome);
    [LoggerMessage(3, LogLevel.Information, "Runtime stopped CancelledRuns={CancelledRuns} CompletedWithinDeadline={CompletedWithinDeadline}")]
    public static partial void Stopped(ILogger logger, int cancelledRuns, bool completedWithinDeadline);
}

internal static class SmokeFixture
{
    public static TradingBotId BotId { get; } = TradingBotId.Parse("01J5QH8M000000000000000101");
    public static TradingBotId BotTwoId { get; } = TradingBotId.Parse("01J5QH8M000000000000000201");
    public static PortfolioId PortfolioId { get; } = PortfolioId.Parse("01J5QH8M000000000000000103");
    public static PortfolioId PortfolioTwoId { get; } = PortfolioId.Parse("01J5QH8M000000000000000203");
    public static PortfolioDecisionSnapshotId SnapshotId { get; } = PortfolioDecisionSnapshotId.Parse("01J5QH8M000000000000000104");
    public static PortfolioDecisionSnapshotId SnapshotTwoId { get; } = PortfolioDecisionSnapshotId.Parse("01J5QH8M000000000000000204");
    public static InstrumentId InstrumentId { get; } = InstrumentId.Parse("01J5QH8M000000000000000301");
    public static BrokerAccountId AccountId { get; } = BrokerAccountId.Parse("01J5QH8M000000000000000302");
    public static BrokerAccountId AccountTwoId { get; } = BrokerAccountId.Parse("01J5QH8M000000000000000303");
    public static async Task SeedAsync(IServiceProvider services, CancellationToken token)
    {
        var bots = services.GetRequiredService<ITradingBotRepository>();
        if (await bots.GetAsync(BotId, token) is not null) return;
        var now = new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        var bot = new TradingBot(BotId, "smoke-bot", now);
        var config = bot.AddConfiguration(TradingBotConfigurationVersionId.Parse("01J5QH8M000000000000000102"), new InvestmentMandate("smoke", TimeSpan.FromDays(30), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])), new RiskPolicy([]), new ToolPolicy([new ToolAllowance(StageThreeTools.GetPortfolioSnapshot, 1), new ToolAllowance(StageThreeTools.Finish, 1), new ToolAllowance(StageFourTradingTools.RequestResearch, 1), new ToolAllowance(StageFourTradingTools.ListReports, 1), new ToolAllowance(StageFourTradingTools.GetReport, 1)]), new RunBudget(TimeSpan.FromMinutes(1), 1000, new Money(10, Currency.USD), 5, 1, 0), new SchedulingPolicy(TimeSpan.FromHours(4), TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)), ExecutionMode.ResearchOnly, new ModelConfiguration("scripted", "smoke", 0, 1000), "smoke-v1", now);
        var portfolioId = PortfolioId;
        bot.AssignPortfolio(portfolioId, now); bot.ActivateConfiguration(config.Id, now); bot.Enable(now);
        await SeedMarketAsync(services, now, token);
        var portfolio = new Portfolio(portfolioId, "smoke portfolio", Currency.USD, new Money(1000, Currency.USD), 0, now); portfolio.AssignTradingBot(bot.Id); portfolio.AssociateBrokerAccount(AccountId);
        var snapshot = new PortfolioDecisionSnapshot(SnapshotId, portfolioId, bot.Id, config.Id, now, ReconciliationStatus.Reconciled, new Money(1000, Currency.USD), new Money(1000, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [], new DataFreshness(now, now, TimeSpan.FromMinutes(5)), now);
        _ = await bots.AddAsync(bot, token);
        _ = await services.GetRequiredService<IPortfolioRepository>().AddAsync(portfolio, token);
        _ = await services.GetRequiredService<IPortfolioDecisionSnapshotRepository>().PublishAsync(snapshot, token);
        await SeedSecondAsync(services, now, token);
    }

    private static async Task SeedSecondAsync(IServiceProvider services, DateTimeOffset now, CancellationToken token)
    {
        var bots = services.GetRequiredService<ITradingBotRepository>();
        if (await bots.GetAsync(BotTwoId, token) is not null) return;
        var bot = new TradingBot(BotTwoId, "smoke-bot-two", now);
        var config = bot.AddConfiguration(TradingBotConfigurationVersionId.Parse("01J5QH8M000000000000000202"), new InvestmentMandate("smoke", TimeSpan.FromDays(30), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])), new RiskPolicy([]), new ToolPolicy([new ToolAllowance(StageThreeTools.GetPortfolioSnapshot, 1), new ToolAllowance(StageThreeTools.Finish, 1), new ToolAllowance(StageFiveTradingTools.ProposeTrade, 2), new ToolAllowance(StageFiveTradingTools.ProposeTargetAllocation, 1)]), new RunBudget(TimeSpan.FromMinutes(1), 1000, new Money(10, Currency.USD), 5, 1, 3), new SchedulingPolicy(TimeSpan.FromHours(4), TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)), ExecutionMode.HumanApproval, new ModelConfiguration("scripted", "smoke", 0, 1000), "smoke-v1", now);
        var portfolioId = PortfolioTwoId; bot.AssignPortfolio(portfolioId, now); bot.ActivateConfiguration(config.Id, now); bot.Enable(now);
        var portfolio = new Portfolio(portfolioId, "smoke portfolio two", Currency.USD, new Money(1000, Currency.USD), 0, now); portfolio.AssignTradingBot(bot.Id); portfolio.AssociateBrokerAccount(AccountTwoId);
        var snapshot = new PortfolioDecisionSnapshot(SnapshotTwoId, portfolioId, bot.Id, config.Id, now, ReconciliationStatus.Reconciled, new Money(1000, Currency.USD), new Money(1000, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [], new DataFreshness(now, now, TimeSpan.FromMinutes(5)), now);
        _ = await bots.AddAsync(bot, token); _ = await services.GetRequiredService<IPortfolioRepository>().AddAsync(portfolio, token); _ = await services.GetRequiredService<IPortfolioDecisionSnapshotRepository>().PublishAsync(snapshot, token);
    }

    private static async Task SeedMarketAsync(IServiceProvider services, DateTimeOffset now, CancellationToken token)
    {
        var connection = new BrokerConnection(BrokerConnectionId.Parse("01J5QH8M000000000000000304"), "fixture", "Deterministic paper fixture", BrokerEnvironment.Paper, "fixture://no-secret", [], now);
        connection.Enable();
        _ = await services.GetRequiredService<IBrokerConnectionRepository>().AddAsync(connection, token);
        var accountA = new BrokerAccount(AccountId, connection.Id, "paper-a", "Paper A", "Cash", Currency.USD, createdAt: now); accountA.Reconcile(now);
        var accountB = new BrokerAccount(AccountTwoId, connection.Id, "paper-b", "Paper B", "Cash", Currency.USD, createdAt: now); accountB.Reconcile(now);
        _ = await services.GetRequiredService<IBrokerAccountRepository>().AddAsync(accountA, token);
        _ = await services.GetRequiredService<IBrokerAccountRepository>().AddAsync(accountB, token);
        var instrument = new Instrument(InstrumentId, InstrumentType.Equity, "ACME", "ACME fixture", Currency.USD, "FIXTURE", createdAt: now);
        instrument.AddBrokerMapping(InstrumentBrokerMappingId.Parse("01J5QH8M000000000000000305"), connection.Id, "ACME-PAPER", "ACME", "FIXTURE", now);
        _ = await services.GetRequiredService<IInstrumentRepository>().AddAsync(instrument, token);
    }
}
