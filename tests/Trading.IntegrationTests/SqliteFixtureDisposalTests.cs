using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Trading.Data;
using Trading.Host;
using Trading.TestInfrastructure;

namespace Trading.IntegrationTests;

[TestFixture]
[Category("FixtureDisposal")]
public sealed class SqliteFixtureDisposalTests
{
    [Test]
    public async Task ScopedContextAndProviderReleaseDatabaseBeforeFirstDeletionAttempt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sqlite-scope-disposal", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "scoped.db");
        var connectionString = SqliteTestDatabaseCleanup.ConnectionString(path);
        Directory.CreateDirectory(directory);
        var services = new ServiceCollection();
        services.AddDbContext<TradingDbContext>(options => options.UseSqlite(connectionString));

        await using (var provider = services.BuildServiceProvider())
        {
            await using var scope = provider.CreateAsyncScope();
            await new DatabaseInitializer(scope.ServiceProvider.GetRequiredService<TradingDbContext>())
                .InitializeAsync().ConfigureAwait(false);
        }

        SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory, connectionString);
        Assert.That(Directory.Exists(directory), Is.False);
    }

    [Test]
    public async Task StoppedAndDisposedHostReleasesDatabaseBeforeFirstDeletionAttempt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sqlite-host-disposal", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "smoke.db");
        var connectionString = SqliteTestDatabaseCleanup.HostConnectionString(path);
        var host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Trading:Mode"] = "Simulated",
            ["Trading:DataDirectory"] = directory,
            ["Trading:SmokeMode"] = "true",
            ["Trading:ShutdownSeconds"] = "5",
            ["Research:Mode"] = "Fixture",
            ["Research:GlobalConcurrency"] = "2",
        }));

        await host.StartAsync().ConfigureAwait(false);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            await host.WaitForShutdownAsync(timeout.Token).ConfigureAwait(false);
        if (host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync().ConfigureAwait(false);
        else host.Dispose();

        SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory, connectionString);
        Assert.That(Directory.Exists(directory), Is.False);
    }
}
