using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reqnroll;
using Trading.Core.Identifiers;
using Trading.Core.Operations;
using Trading.Core.Orders;
using Trading.Core.Proposals;
using Trading.Engine.Operators;
using Trading.Host;
using Trading.TestInfrastructure;
using Trading.UI.Wpf.Services;

namespace Trading.AcceptanceTests.Support;

public sealed class Stage7OperatorDriver : IAsyncDisposable
{
    private const string BotId = "01J5QH8M000000000000000101";
    private const string PortfolioId = "01J5QH8M000000000000000103";
    private const string AccountId = "01J5QH8M000000000000000302";
    private const string ProposalAlphaId = "01J5QH8M000000000000000401";
    private const string OrderAlphaId = "01J5QH8M000000000000000501";
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "trading-stage7-acceptance", Guid.NewGuid().ToString("N"));
    private TradingApplicationLifecycle? lifecycle;
    private IServiceScope? scope;
    private ScenarioOperatorState? state;
    private IProposalOperatorService? proposals;
    private IKillSwitchOperatorService? switches;
    private IRunOperatorService? runs;
    private IOperatorQueries? queries;
    private BoundedOperatorUpdateBuffer? updates;
    private OperatorCommandResult? commandResult;
    private IAsyncEnumerator<OperatorUpdate>? updateEnumerator;
    private Stopwatch? shutdown;

    public Stage7OperatorDriver(ScenarioContext context) => ArgumentNullException.ThrowIfNull(context);

    public async Task StartUnauthorizedApprovalAsync()
    {
        await StartAsync().ConfigureAwait(false);
        state!.ProposalStatus = ProposalStatus.AwaitingHumanApproval;
    }

    public void ArrangeAwaitingProposal() => Assert.That(state?.ProposalStatus, Is.EqualTo(ProposalStatus.AwaitingHumanApproval));

    public async Task RequestApprovalAsync() => commandResult = await proposals!.ApproveAsync(
        Reader, TradeProposalId.Parse(ProposalAlphaId), 1, "reviewed", CancellationToken.None).ConfigureAwait(false);

    public void AssertApprovalDenied() => Assert.That(commandResult, Is.EqualTo(OperatorCommandResult.Unavailable()));

    public async Task AssertProposalAwaitingAsync()
    {
        var page = await QueryPageAsync<ProposalSummary>(OperatorPageKind.Proposals,
            new(OperatorResourceKind.TradeProposal, ProposalAlphaId)).ConfigureAwait(false);
        Assert.That(page.Items.Single().Status, Is.EqualTo(ProposalStatus.AwaitingHumanApproval), Diagnostic());
    }

    public async Task AssertDenialAuditAsync()
    {
        var page = await QueryPageAsync<AuditSummary>(OperatorPageKind.RiskAndAudit, OperatorResource.Platform)
            .ConfigureAwait(false);
        var denial = page.Items.Single(x => x.Code == "operator.authorization.denied");
        Assert.Multiple(() =>
        {
            Assert.That(denial.Kind, Is.EqualTo("authorization"));
            Assert.That(denial.CorrelationId, Does.StartWith("operator-command-"));
            Assert.That(denial.CorrelationId.Length, Is.LessThanOrEqualTo(64));
            Assert.That(Diagnostic(), Does.Not.Contain("reviewed"));
        });
    }

    public async Task StartAssignedBotAsync()
    {
        await StartAsync().ConfigureAwait(false);
        state!.DurableWorkRecoverable = true;
    }

    public async Task ActivatePlatformSwitchAsync() => commandResult = await switches!.ActivateAsync(
        Manager, OperatorResource.Platform, 0, "operator safety test", "ACTIVATE Platform platform",
        CancellationToken.None).ConfigureAwait(false);

    public async Task AssertInheritedSwitchAsync()
    {
        Assert.That(commandResult?.Status, Is.EqualTo(OperatorResultStatus.Succeeded), Diagnostic());
        var page = await QueryPageAsync<KillSwitchDetail>(OperatorPageKind.RiskAndAudit,
            new(OperatorResourceKind.TradingBot, BotId)).ConfigureAwait(false);
        var detail = page.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(detail.Direct.IsActive, Is.False);
            Assert.That(detail.Effective?.IsActive, Is.True);
            Assert.That(detail.Effective?.Scope, Is.EqualTo(OperatorResource.Platform));
        });
    }

    public async Task AssertSwitchAuditAsync()
    {
        var page = await QueryPageAsync<KillSwitchDetail>(OperatorPageKind.RiskAndAudit, OperatorResource.Platform)
            .ConfigureAwait(false);
        var change = page.Items.Single().History.Single();
        Assert.Multiple(() =>
        {
            Assert.That(change.ActorId, Is.EqualTo("operator-manager"));
            Assert.That(change.Reason, Is.EqualTo("operator safety test"));
            Assert.That(change.ChangedAt, Is.EqualTo(Now));
            Assert.That(change.ChangedAt.Offset, Is.EqualTo(TimeSpan.Zero));
        });
    }

    public void AssertDurableWorkRecoverable() => Assert.That(state?.DurableWorkRecoverable, Is.True, Diagnostic());

    public async Task StartUpdateObservationAsync()
    {
        await StartAsync().ConfigureAwait(false);
        updateEnumerator = updates!.SubscribeAsync(new HashSet<OperatorUpdateKind> { OperatorUpdateKind.Fills },
            CancellationToken.None).GetAsyncEnumerator();
    }

    public async Task DeliverFillsAsync()
    {
        state!.OrderStatus = OrderStatus.PartiallyFilled;
        await updates!.PublishAsync(new(OperatorUpdateKind.Fills, "fill-alpha-partial", 1)).ConfigureAwait(false);
        state.OrderStatus = OrderStatus.Filled;
        await updates.PublishAsync(new(OperatorUpdateKind.Fills, "fill-alpha-final", 2, true)).ConfigureAwait(false);
    }

    public async Task AssertOrderedFillUpdatesAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var observed = new List<OperatorUpdate>();
        while (observed.Count < 2 && await updateEnumerator!.MoveNextAsync().AsTask().WaitAsync(timeout.Token).ConfigureAwait(false))
            observed.Add(updateEnumerator.Current);
        Assert.Multiple(() =>
        {
            Assert.That(observed.Select(x => x.Sequence), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(observed[1].IsTerminal, Is.True);
            Assert.That(observed.All(x => x.Identity.Length <= OperatorUpdate.MaximumIdentityLength), Is.True);
        });
    }

    public async Task AssertFinalOrderStatusAsync()
    {
        var page = await QueryPageAsync<ExecutionSummary>(OperatorPageKind.Execution,
            new(OperatorResourceKind.Portfolio, PortfolioId)).ConfigureAwait(false);
        Assert.That(page.Items.Single().Status, Is.EqualTo(OrderStatus.Filled), Diagnostic());
    }

    public async Task StartActiveHostAsync()
    {
        await StartAsync().ConfigureAwait(false);
        state!.ActiveRun = true;
        state.DurableWorkRecoverable = true;
    }

    public async Task StopAsync()
    {
        shutdown = Stopwatch.StartNew();
        scope?.Dispose();
        scope = null;
        await lifecycle!.StopAsync().ConfigureAwait(false);
        shutdown.Stop();
    }

    public async Task AssertCommandsStoppedAsync()
    {
        var result = await runs!.TriggerAsync(Manager, TradingBotId.Parse(BotId), "after shutdown",
            CancellationToken.None).ConfigureAwait(false);
        Assert.That(result, Is.EqualTo(OperatorCommandResult.Unavailable()), Diagnostic());
    }

    public void AssertBoundedStop() => Assert.Multiple(() =>
    {
        Assert.That(lifecycle?.State, Is.EqualTo(ApplicationLifecycleState.Stopped));
        Assert.That(shutdown?.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)));
    });

    public void AssertShutdownState() => Assert.Multiple(() =>
    {
        Assert.That(state?.AcceptingCommands, Is.False);
        Assert.That(state?.DurableWorkRecoverable, Is.True);
        Assert.That(state?.ActiveRun, Is.True);
        Assert.That(state?.Stopped, Is.True);
    });

    private async Task StartAsync()
    {
        if (lifecycle is not null) return;
        Directory.CreateDirectory(directory);
        var host = HostBootstrap.Build([], builder =>
        {
            builder.Configuration.AddInMemoryCollection(Configuration());
            builder.Services.AddSingleton<ScenarioOperatorState>();
            builder.Services.AddSingleton<ScenarioAuthorization>();
            builder.Services.AddSingleton<IOperatorAuthorization>(x => x.GetRequiredService<ScenarioAuthorization>());
            builder.Services.AddScoped<ScenarioOperatorWorkflow>();
            builder.Services.AddScoped<IOperatorWorkflowPort>(x => x.GetRequiredService<ScenarioOperatorWorkflow>());
            builder.Services.AddScoped<AuthorizedOperatorService>();
            builder.Services.AddScoped<IOperatorQueries>(x => x.GetRequiredService<AuthorizedOperatorService>());
            builder.Services.AddScoped<IProposalOperatorService>(x => x.GetRequiredService<AuthorizedOperatorService>());
            builder.Services.AddScoped<IKillSwitchOperatorService>(x => x.GetRequiredService<AuthorizedOperatorService>());
            builder.Services.AddScoped<IRunOperatorService>(x => x.GetRequiredService<AuthorizedOperatorService>());
            builder.Services.AddSingleton<BoundedOperatorUpdateBuffer>(_ => new(8));
        });
        lifecycle = new(host, TimeSpan.FromSeconds(8));
        using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await lifecycle.StartAsync(startup.Token).ConfigureAwait(false);
        state = lifecycle.Services.GetRequiredService<ScenarioOperatorState>();
        state.Attach(lifecycle.Services.GetRequiredService<IHostApplicationLifetime>());
        updates = lifecycle.Services.GetRequiredService<BoundedOperatorUpdateBuffer>();
        scope = lifecycle.Services.CreateScope();
        queries = scope.ServiceProvider.GetRequiredService<IOperatorQueries>();
        proposals = scope.ServiceProvider.GetRequiredService<IProposalOperatorService>();
        switches = scope.ServiceProvider.GetRequiredService<IKillSwitchOperatorService>();
        runs = scope.ServiceProvider.GetRequiredService<IRunOperatorService>();
    }

    private async Task<OperatorPage<T>> QueryPageAsync<T>(OperatorPageKind page, OperatorResource resource)
    {
        var result = await queries!.GetPageAsync<T>(Reader, page, resource, new(), new(0, 20), CancellationToken.None)
            .ConfigureAwait(false);
        Assert.That(result.Status, Is.EqualTo(OperatorResultStatus.Succeeded), Diagnostic());
        return result.Value ?? throw new InvalidOperationException("The authorized operator page was unavailable.");
    }

    private string Diagnostic() => state is null ? "stage7-state=unavailable" :
        $"accepting={state.AcceptingCommands};proposal={state.ProposalStatus};order={state.OrderStatus};audits={state.Audits.Count};recoverable={state.DurableWorkRecoverable};stopped={state.Stopped};database={Path.GetFileName(directory)}";

    public async ValueTask DisposeAsync()
    {
        if (updateEnumerator is not null) await updateEnumerator.DisposeAsync().ConfigureAwait(false);
        updateEnumerator = null;
        scope?.Dispose();
        scope = null;
        if (lifecycle is not null) await lifecycle.DisposeAsync().ConfigureAwait(false);
        lifecycle = null;
        SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory,
            SqliteTestDatabaseCleanup.HostConnectionString(Path.Combine(directory, "trading.db")));
    }

    private Dictionary<string, string?> Configuration() => new()
    {
        ["Trading:Mode"] = "Simulated",
        ["Trading:DataDirectory"] = directory,
        ["Trading:OperatorMode"] = "true",
        ["Trading:WpfTestProfile"] = "true",
        ["Trading:GlobalRunConcurrency"] = "1",
        ["Trading:QueueCapacity"] = "2",
        ["Trading:LeaseSeconds"] = "30",
        ["Trading:ShutdownSeconds"] = "5",
        ["Research:Mode"] = "Fixture",
        ["Research:FixtureVersion"] = "v1",
        ["Research:ModelProvider"] = "scripted",
        ["Research:ModelId"] = "research",
    };

    private static OperatorPrincipal Reader { get; } = new("operator-alice", [OperatorAuthority.ReadOperations]);
    private static OperatorPrincipal Manager { get; } = new("operator-manager", Enum.GetValues<OperatorAuthority>());

    private sealed class ScenarioOperatorState
    {
        private int attached;
        public bool AcceptingCommands { get; set; } = true;
        public bool DurableWorkRecoverable { get; set; }
        public bool ActiveRun { get; set; }
        public bool Stopped { get; set; }
        public ProposalStatus ProposalStatus { get; set; } = ProposalStatus.AwaitingHumanApproval;
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Submitted;
        public ConcurrentQueue<AuditSummary> Audits { get; } = new();

        public void Attach(IHostApplicationLifetime lifetime)
        {
            if (Interlocked.Exchange(ref attached, 1) != 0) return;
            lifetime.ApplicationStopping.Register(() => AcceptingCommands = false);
            lifetime.ApplicationStopped.Register(() => Stopped = true);
        }
    }

    private sealed class ScenarioAuthorization(ScenarioOperatorState state) : IOperatorAuthorization
    {
        private long sequence;
        public Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission,
            OperatorResource resource, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var allowed = state.AcceptingCommands && principal.Permissions.Contains(permission);
            if (!allowed)
            {
                var id = Interlocked.Increment(ref sequence);
                state.Audits.Enqueue(new($"authorization-{id}", "authorization", "operator.authorization.denied",
                    Now, $"operator-command-{id}"));
            }
            return Task.FromResult(allowed);
        }
    }

    private sealed class ScenarioOperatorWorkflow(IKillSwitchStore switches, ScenarioOperatorState state) : IOperatorWorkflowPort
    {
        public async Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
            OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken)
        {
            object value = typeof(T) switch
            {
                var type when type == typeof(OperatorPage<ProposalSummary>) => new OperatorPage<ProposalSummary>(
                    [new(TradeProposalId.Parse(ProposalAlphaId), TradingBotId.Parse(BotId),
                        Trading.Core.Identifiers.PortfolioId.Parse(PortfolioId), state.ProposalStatus, Now.AddHours(1), 1)], 0, null),
                var type when type == typeof(OperatorPage<AuditSummary>) => new OperatorPage<AuditSummary>(state.Audits, 0, null),
                var type when type == typeof(OperatorPage<ExecutionSummary>) => new OperatorPage<ExecutionSummary>(
                    [new(Trading.Core.Identifiers.OrderId.Parse(OrderAlphaId), Trading.Core.Identifiers.PortfolioId.Parse(PortfolioId), state.OrderStatus,
                        "ACME", 10m, state.OrderStatus == OrderStatus.Filled ? 10m : 0m, "USD", Now)], 0, null),
                var type when type == typeof(OperatorPage<KillSwitchDetail>) => new OperatorPage<KillSwitchDetail>(
                    [await KillSwitchAsync(resource, cancellationToken).ConfigureAwait(false)], 0, null),
                _ => throw new InvalidOperationException($"Unsupported scenario query contract '{typeof(T).Name}'."),
            };
            return new(OperatorResultStatus.Succeeded, (T)value);
        }

        public async Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command,
            CancellationToken cancellationToken)
        {
            if (command.Kind is not (OperatorCommandKind.ActivateKillSwitch or OperatorCommandKind.ClearKillSwitch))
                return new(OperatorResultStatus.Succeeded, "operator.command.accepted", command.Resource.Id, command.ExpectedVersion);
            var scope = ToScope(command.Resource);
            var desired = command.Kind == OperatorCommandKind.ActivateKillSwitch ? KillSwitchState.Active : KillSwitchState.Clear;
            var result = await switches.ChangeAsync(new($"stage7-{principal.ActorId}-{command.Kind}", scope, desired,
                command.ExpectedVersion ?? 0, command.Arguments["reason"], principal.ActorId,
                command.Arguments["confirmation"], Now), cancellationToken).ConfigureAwait(false);
            return result.Status is KillSwitchChangeStatus.Applied or KillSwitchChangeStatus.Idempotent
                ? new(OperatorResultStatus.Succeeded, result.ReasonCode, command.Resource.Id, result.Snapshot?.Version)
                : new(OperatorResultStatus.Conflict, result.ReasonCode, command.Resource.Id, result.Snapshot?.Version);
        }

        private async Task<KillSwitchDetail> KillSwitchAsync(OperatorResource resource, CancellationToken token)
        {
            var directScope = ToScope(resource);
            var direct = await switches.GetAsync(directScope, token).ConfigureAwait(false);
            var effective = await switches.GetEffectiveAsync(new(AccountId, PortfolioId, BotId), token).ConfigureAwait(false);
            var history = await switches.GetHistoryAsync(directScope, token).ConfigureAwait(false);
            return new(ToSummary(directScope, direct), effective.Source is null ? null : ToSummary(effective.Source.Scope, effective.Source),
                history.Select(x => new OperatorKillSwitchHistory(x.ResultingState == KillSwitchState.Active, x.Reason,
                    x.ActorId, x.Confirmation, x.ChangedAt, x.Version)).ToImmutableArray());
        }

        private static KillSwitchSummary ToSummary(KillSwitchScope scope, KillSwitchSnapshot? snapshot) => new(
            ToResource(scope), snapshot?.State == KillSwitchState.Active, snapshot?.Reason ?? string.Empty,
            snapshot?.ActorId ?? string.Empty, snapshot?.ChangedAt ?? Now, snapshot?.Version ?? 0);

        private static KillSwitchScope ToScope(OperatorResource resource) => resource.Kind switch
        {
            OperatorResourceKind.Platform => KillSwitchScope.Platform,
            OperatorResourceKind.BrokerAccount => new(KillSwitchScopeKind.BrokerAccount, resource.Id),
            OperatorResourceKind.Portfolio => new(KillSwitchScopeKind.Portfolio, resource.Id),
            OperatorResourceKind.TradingBot => new(KillSwitchScopeKind.TradingBot, resource.Id),
            _ => throw new InvalidOperationException("The resource is not a kill-switch scope."),
        };

        private static OperatorResource ToResource(KillSwitchScope scope) => new(scope.Kind switch
        {
            KillSwitchScopeKind.Platform => OperatorResourceKind.Platform,
            KillSwitchScopeKind.BrokerAccount => OperatorResourceKind.BrokerAccount,
            KillSwitchScopeKind.Portfolio => OperatorResourceKind.Portfolio,
            KillSwitchScopeKind.TradingBot => OperatorResourceKind.TradingBot,
            _ => throw new ArgumentOutOfRangeException(nameof(scope)),
        }, scope.Id);
    }
}
