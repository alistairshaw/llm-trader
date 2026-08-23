using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Core.Proposals;
using Trading.Engine.Runtime;
using Trading.Host;

namespace Trading.IntegrationTests;

[TestFixture, Category("HostLifecycle"), Category("WpfHostLifecycle")]
public sealed class WpfHostLifecycleTests
{
    private static readonly string[] ExpectedProfilePortfolios = ["smoke portfolio", "smoke portfolio two"];

    [Test, Category("WpfTestProfile")]
    public async Task DeterministicProfileMigratesSeedsPaperJourneysAndCleansUpOnFirstAttempt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"trading-wpf-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "trading.db");
        try
        {
            var host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Trading:Mode"] = "Simulated",
                    ["Trading:DataDirectory"] = directory,
                    ["Trading:OperatorMode"] = "true",
                    ["Trading:WpfTestProfile"] = "true",
                    ["Research:Mode"] = "Fixture",
                }));
            await using var lifecycle = new TradingApplicationLifecycle(host, TimeSpan.FromSeconds(10));

            await lifecycle.StartAsync(default);
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var portfolios = await scope.ServiceProvider.GetRequiredService<IPortfolioQueries>()
                    .GetPortfoliosAsync(new PortfolioQueryFilter(), new PageRequest(0, 10), default);
                Assert.That(portfolios, Has.Count.EqualTo(2));
                Assert.That(portfolios.Select(x => x.Name), Is.EquivalentTo(ExpectedProfilePortfolios));
                Assert.That(host.Services.GetRequiredService<IUtcClock>().UtcNow,
                    Is.EqualTo(new DateTimeOffset(2026, 8, 20, 23, 0, 0, TimeSpan.Zero)));

                var now = host.Services.GetRequiredService<IUtcClock>().UtcNow;
                var proposals = await scope.ServiceProvider.GetRequiredService<IProposalQueries>()
                    .GetQueueAsync(new("fixture-operator", true), new(), new(0, 10), now, default);
                var execution = scope.ServiceProvider.GetRequiredService<IOrderExecutionQueries>();
                var orders = await execution.GetOrdersAsync(new("fixture-operator", true, [], [], []),
                    new(Status: OrderStatus.Filled), new(0, 10), default);
                var order = await execution.GetOrderAsync(new("fixture-operator", true, [], [], []),
                    orders.Single().Id, default);
                var proposal = proposals.SingleOrDefault();
                Assert.Multiple(() =>
                {
                    Assert.That(proposals, Has.Count.EqualTo(1));
                    Assert.That(proposal, Is.Not.Null);
                    Assert.That(proposal!.Status, Is.EqualTo(ProposalStatus.AwaitingHumanApproval));
                    Assert.That(proposal.IsExpired, Is.False);
                    Assert.That(proposal.EvaluationCount, Is.GreaterThan(0));
                    Assert.That(orders, Has.Count.EqualTo(1));
                    Assert.That(order, Is.Not.Null);
                    Assert.That(order!.Order.Status, Is.EqualTo(OrderStatus.Filled));
                    Assert.That(order.Fills, Has.Count.EqualTo(2));
                    Assert.That(order.FilledQuantity, Is.EqualTo(70));
                    Assert.That(order.ReservationStatus, Is.EqualTo("Consumed"));
                });
            }

            await lifecycle.StopAsync();
            await lifecycle.DisposeAsync();

            var restartedHost = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Trading:Mode"] = "Simulated",
                    ["Trading:DataDirectory"] = directory,
                    ["Trading:OperatorMode"] = "true",
                    ["Trading:WpfTestProfile"] = "true",
                    ["Research:Mode"] = "Fixture",
                }));
            await using (var restarted = new TradingApplicationLifecycle(restartedHost, TimeSpan.FromSeconds(10)))
            {
                await restarted.StartAsync(default);
                Assert.That(restartedHost.Services.GetRequiredService<RuntimeReadiness>().IsReady, Is.True);
                await restarted.StopAsync();
            }
            Directory.Delete(directory, recursive: true);
            Assert.That(Directory.Exists(directory), Is.False);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            Assert.That(Directory.Exists(directory), Is.False);
        }
    }

    [Test]
    public async Task ReadinessFollowsMigrationAndRecoveryAndDisposalReleasesDatabaseImmediately()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"trading-wpf-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "trading.db");
        try
        {
            var host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Trading:Mode"] = "Simulated",
                    ["Trading:DataDirectory"] = directory,
                    ["Trading:OperatorMode"] = "true",
                    ["Research:Mode"] = "Fixture",
                }));
            await using var lifecycle = new TradingApplicationLifecycle(host, TimeSpan.FromSeconds(10));

            await lifecycle.StartAsync(default);

            Assert.Multiple(() =>
            {
                Assert.That(lifecycle.State, Is.EqualTo(ApplicationLifecycleState.Ready));
                Assert.That(host.Services.GetRequiredService<RuntimeReadiness>().IsReady, Is.True);
                Assert.That(File.Exists(database), Is.True, "readiness requires completed database migration");
            });

            await lifecycle.StopAsync();
            await lifecycle.StopAsync();
            Directory.Delete(directory, recursive: true);
            Assert.That(Directory.Exists(directory), Is.False);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task StopReleasesOnlyTheOwnedDatabasePool()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"trading-wpf-host-owned-{Guid.NewGuid():N}");
        var unrelatedDirectory = Path.Combine(Path.GetTempPath(), $"trading-wpf-host-unrelated-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(unrelatedDirectory);
        var unrelatedConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(unrelatedDirectory, "unrelated.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        }.ToString();

        await using var unrelatedConnection = new SqliteConnection(unrelatedConnectionString);
        try
        {
            await unrelatedConnection.OpenAsync();
            await using (var command = unrelatedConnection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE retained(value INTEGER NOT NULL); INSERT INTO retained VALUES (7);";
                await command.ExecuteNonQueryAsync();
            }

            var host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Trading:Mode"] = "Simulated",
                    ["Trading:DataDirectory"] = directory,
                    ["Trading:OperatorMode"] = "true",
                    ["Research:Mode"] = "Fixture",
                }));
            await using var lifecycle = new TradingApplicationLifecycle(host, TimeSpan.FromSeconds(10));
            await lifecycle.StartAsync(default);

            await lifecycle.StopAsync();
            Directory.Delete(directory, recursive: true);

            await using var verification = unrelatedConnection.CreateCommand();
            verification.CommandText = "SELECT value FROM retained";
            Assert.Multiple(() =>
            {
                Assert.That(Directory.Exists(directory), Is.False);
                Assert.That(verification.ExecuteScalar(), Is.EqualTo(7L));
            });
        }
        finally
        {
            await unrelatedConnection.CloseAsync();
            SqliteConnection.ClearPool(unrelatedConnection);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            if (Directory.Exists(unrelatedDirectory)) Directory.Delete(unrelatedDirectory, true);
        }
    }

    [Test]
    public async Task CancellationBeforeReadinessStopsAndDisposesOwnershipExactlyOnce()
    {
        var readiness = new RuntimeReadiness();
        var host = new RecordingHost(new ServiceCollection().AddSingleton(readiness).BuildServiceProvider());
        var lifecycle = new TradingApplicationLifecycle(host, TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();
        var start = lifecycle.StartAsync(cancellation.Token);
        await host.Started.Task;
        cancellation.Cancel();

        Assert.That(async () => await start, Throws.InstanceOf<OperationCanceledException>());
        await lifecycle.StopAsync();
        await lifecycle.DisposeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(host.StopCount, Is.EqualTo(1));
            Assert.That(host.DisposeCount, Is.EqualTo(1));
            Assert.That(lifecycle.State, Is.EqualTo(ApplicationLifecycleState.Stopped));
        });
    }

    [Test]
    public async Task StartupFailureStopsAndDisposesOwnershipExactlyOnce()
    {
        var failure = new InvalidOperationException("fixture startup failure");
        var host = new RecordingHost(new ServiceCollection().AddSingleton(new RuntimeReadiness()).BuildServiceProvider(), failure);
        var lifecycle = new TradingApplicationLifecycle(host, TimeSpan.FromSeconds(1));

        Assert.That(async () => await lifecycle.StartAsync(default), Throws.InstanceOf<InvalidOperationException>());
        await lifecycle.StopAsync();
        await lifecycle.DisposeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(host.StopCount, Is.EqualTo(1));
            Assert.That(host.DisposeCount, Is.EqualTo(1));
            Assert.That(lifecycle.StartupFailure, Is.SameAs(failure));
        });
    }

    private sealed class RecordingHost(IServiceProvider services, Exception? startFailure = null) : IHost, IAsyncDisposable
    {
        public IServiceProvider Services { get; } = services;
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return startFailure is null ? Task.CompletedTask : Task.FromException(startFailure);
        }
        public Task StopAsync(CancellationToken cancellationToken = default) { StopCount++; return Task.CompletedTask; }
        public void Dispose() => DisposeCount++;
        public ValueTask DisposeAsync() { DisposeCount++; return ValueTask.CompletedTask; }
    }
}
