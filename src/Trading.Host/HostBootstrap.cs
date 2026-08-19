using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Data;
using Trading.Engine.Runtime;

namespace Trading.Host;

public sealed class TradingHostOptions
{
    public string Mode { get; init; } = "Simulated";
    public string DataDirectory { get; init; } = "/data";
    public bool SmokeMode { get; init; }
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
        if (!SmokeMode && BotIds.Length == 0) throw new InvalidOperationException("Trading:BotIds must contain at least one configured Bot.");
    }
}

public sealed class RuntimeReadiness { public bool IsReady { get; internal set; } }

public static class HostBootstrap
{
    public static IHost Build(string[] args, Action<IHostApplicationBuilder>? configure = null)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        configure?.Invoke(builder);
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        var options = builder.Configuration.GetSection("Trading").Get<TradingHostOptions>() ?? new();
        options.Validate();
        Directory.CreateDirectory(options.DataDirectory);
        var path = Path.Combine(options.DataDirectory, options.SmokeMode ? "smoke.db" : "trading.db");
        if (options.SmokeMode)
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" }) File.Delete(path + suffix);
        }
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<RuntimeReadiness>();
        builder.Services.AddSingleton<IUtcClock, SystemUtcClock>();
        builder.Services.AddSingleton<IRuntimeIdentifierGenerator, RuntimeIdentifiers>();
        builder.Services.AddDbContext<TradingDbContext>(x => x.UseSqlite($"Data Source={path};Default Timeout=5"));
        builder.Services.AddScoped<DatabaseInitializer>();
        builder.Services.AddScoped<ITradingBotRepository, TradingBotRepository>();
        builder.Services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        builder.Services.AddScoped<IPortfolioDecisionSnapshotRepository, PortfolioDecisionSnapshotRepository>();
        builder.Services.AddScoped<IPortfolioQueries, PortfolioQueries>();
        builder.Services.AddScoped<IBotRunRepository, BotRunRepository>();
        builder.Services.AddScoped<IBotRunInputAuditWriter>(x => (BotRunRepository)x.GetRequiredService<IBotRunRepository>());
        builder.Services.AddScoped<IBotRunTriggerRepository, BotRunTriggerRepository>();
        builder.Services.AddScoped<BotTriggerIngestionService>();
        builder.Services.AddScoped<BotTriggerCoalescingService>();
        builder.Services.AddScoped<IBotRunInputService, BotRunInputService>();
        builder.Services.AddScoped<IToolDispatcher, StageThreeToolDispatcher>();
        builder.Services.AddScoped<IModelLoop, BoundedModelLoop>();
        builder.Services.AddScoped<DeterministicSchedulingPolicy>();
        builder.Services.AddScoped<RuntimeRecoveryService>();
        builder.Services.AddScoped<BotRunOrchestrationService>();
        builder.Services.AddHostedService<TradingRuntimeHostedService>();
        return builder.Build();
    }

    public static Task RunAsync(string[] args) => Build(args).RunAsync();
}

internal sealed class SystemUtcClock : IUtcClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
internal sealed class RuntimeIdentifiers : IRuntimeIdentifierGenerator
{
    public BotRunId NewBotRunId() => BotRunId.New();
    public BotRunTriggerId NewTriggerId() => BotRunTriggerId.New();
    public ToolInvocationId NewToolInvocationId() => ToolInvocationId.New();
}

internal sealed class TradingRuntimeHostedService(IServiceScopeFactory scopes, TradingHostOptions options,
    RuntimeReadiness readiness, IHostApplicationLifetime lifetime, ILogger<TradingRuntimeHostedService> logger) : BackgroundService
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
            var recovery = await services.GetRequiredService<RuntimeRecoveryService>().RecoverExpiredLeasesAsync(stoppingToken);
            var ids = options.SmokeMode ? [SmokeFixture.BotId] : options.BotIds.Select(TradingBotId.Parse).ToArray();
            foreach (var id in ids) await ValidateBotAsync(services, id, stoppingToken);
            supervisor = new MultiBotSupervisor(new MultiBotSupervisorOptions { GlobalRunConcurrency = options.GlobalRunConcurrency, QueueCapacity = options.QueueCapacity }, services.GetRequiredService<BotRunOrchestrationService>());
            readiness.IsReady = true;
            RuntimeLogs.Ready(logger, Environment.MachineName, recovery.RecoveredRuns, recovery.FaultedRuns);
            var completions = new List<Task<BotRunExecutionResult>>();
            foreach (var id in ids)
            {
                if (options.SmokeMode) await services.GetRequiredService<BotTriggerIngestionService>().IngestAsync(new(id, BotRunTriggerType.Manual, "deterministic smoke", DateTimeOffset.UtcNow), stoppingToken);
                var queued = await supervisor.QueueAsync(new(id, Environment.MachineName, TimeSpan.FromSeconds(options.LeaseSeconds), SmokeSession()), stoppingToken);
                if (queued.Completion is not null) completions.Add(queued.Completion);
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
                lifetime.StopApplication();
            }
            else await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        finally { readiness.IsReady = false; }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        readiness.IsReady = false;
        if (supervisor is not null)
        {
            var result = await supervisor.ShutdownAsync(TimeSpan.FromSeconds(options.ShutdownSeconds), cancellationToken);
            RuntimeLogs.Stopped(logger, result.CancelledRuns, result.CompletedWithinDeadline);
            await supervisor.DisposeAsync();
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
    [LoggerMessage(1, LogLevel.Information, "Runtime ready HostInstance={HostInstance} RecoveredRuns={RecoveredRuns} FaultedRuns={FaultedRuns}")]
    public static partial void Ready(ILogger logger, string hostInstance, int recoveredRuns, int faultedRuns);
    [LoggerMessage(2, LogLevel.Information, "Smoke BotRun={BotRun} Outcome={Outcome}")]
    public static partial void SmokeResult(ILogger logger, string botRun, string outcome);
    [LoggerMessage(3, LogLevel.Information, "Runtime stopped CancelledRuns={CancelledRuns} CompletedWithinDeadline={CompletedWithinDeadline}")]
    public static partial void Stopped(ILogger logger, int cancelledRuns, bool completedWithinDeadline);
}

internal static class SmokeFixture
{
    public static TradingBotId BotId { get; } = TradingBotId.Parse("01J5QH8M000000000000000101");
    public static async Task SeedAsync(IServiceProvider services, CancellationToken token)
    {
        var bots = services.GetRequiredService<ITradingBotRepository>();
        if (await bots.GetAsync(BotId, token) is not null) return;
        var now = new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        var bot = new TradingBot(BotId, "smoke-bot", now);
        var config = bot.AddConfiguration(TradingBotConfigurationVersionId.Parse("01J5QH8M000000000000000102"), new InvestmentMandate("smoke", TimeSpan.FromDays(30), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])), new RiskPolicy([]), new ToolPolicy([new ToolAllowance(StageThreeTools.GetPortfolioSnapshot, 1), new ToolAllowance(StageThreeTools.Finish, 1)]), new RunBudget(TimeSpan.FromMinutes(1), 1000, new Money(10, Currency.USD), 3, 0, 0), new SchedulingPolicy(TimeSpan.FromHours(4), TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)), ExecutionMode.ResearchOnly, new ModelConfiguration("scripted", "smoke", 0, 1000), "smoke-v1", now);
        var portfolioId = PortfolioId.Parse("01J5QH8M000000000000000103");
        bot.AssignPortfolio(portfolioId, now); bot.ActivateConfiguration(config.Id, now); bot.Enable(now);
        var portfolio = new Portfolio(portfolioId, "smoke portfolio", Currency.USD, new Money(1000, Currency.USD), 0, now); portfolio.AssignTradingBot(bot.Id);
        var snapshot = new PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId.Parse("01J5QH8M000000000000000104"), portfolioId, bot.Id, config.Id, now, ReconciliationStatus.Reconciled, new Money(1000, Currency.USD), new Money(1000, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [], new DataFreshness(now, now, TimeSpan.FromMinutes(5)), now);
        _ = await bots.AddAsync(bot, token);
        _ = await services.GetRequiredService<IPortfolioRepository>().AddAsync(portfolio, token);
        _ = await services.GetRequiredService<IPortfolioDecisionSnapshotRepository>().PublishAsync(snapshot, token);
    }
}
