using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Trading.Engine.Runtime;
using Trading.Host;

namespace Trading.IntegrationTests;

[TestFixture]
[Category("HeadlessHost")]
[Category("ResearchHost")]
public sealed class HeadlessHostTests
{
    [Test]
    public void InvalidConfigurationFailsBeforeHostIsBuilt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "headless-host", Guid.NewGuid().ToString("N"));
        var values = Configuration(directory, smoke: false);
        values["Trading:Mode"] = "Live";
        var exception = Assert.Throws<InvalidOperationException>(() => HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(values)));
        Assert.That(exception!.Message, Does.Contain("must be Simulated"));
        Assert.That(Directory.Exists(directory), Is.False);
    }

    [Test]
    public void NetworkResearchConfigurationFailsBeforeHostIsBuilt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "headless-host", Guid.NewGuid().ToString("N"));
        var values = Configuration(directory, smoke: true);
        values["Research:Mode"] = "Network";
        var exception = Assert.Throws<InvalidOperationException>(() => HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(values)));
        Assert.That(exception!.Message, Does.Contain("must be Fixture"));
        Assert.That(Directory.Exists(directory), Is.False);
    }

    [Test]
    public async Task SmokeModeMigratesSeedsRunsAndStopsCleanly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "headless-host", Guid.NewGuid().ToString("N"));
        try
        {
            using var host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(Configuration(directory, smoke: true)));
            var readiness = host.Services.GetRequiredService<RuntimeReadiness>();
            using (var scope = host.Services.CreateScope())
                Assert.That(scope.ServiceProvider.GetRequiredService<IToolDispatcher>().Definitions.Select(x => x.Name),
                    Does.Contain(StageFourTradingTools.GetReport));
            await host.RunAsync();
            await using var connection = new SqliteConnection($"Data Source={Path.Combine(directory, "smoke.db")}");
            await connection.OpenAsync();
            var status = await ScalarAsync(connection, "SELECT status FROM bot_runs");
            var reports = await ScalarAsync(connection, "SELECT COUNT(*) FROM research_reports");
            var completed = await ScalarAsync(connection, "SELECT COUNT(*) FROM research_requests WHERE status = 'Completed'");
            var delivered = await ScalarAsync(connection, "SELECT COUNT(*) FROM research_subscriptions WHERE notification_status = 'Delivered'");
            var latestVersion = await ScalarAsync(connection, "SELECT MAX(version_number) FROM research_reports");
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(Path.Combine(directory, "smoke.db")), Is.True);
                Assert.That(readiness.IsReady, Is.False);
                Assert.That(status, Is.EqualTo("Completed"));
                Assert.That(Convert.ToInt64(reports, CultureInfo.InvariantCulture), Is.EqualTo(3));
                Assert.That(Convert.ToInt64(completed, CultureInfo.InvariantCulture), Is.EqualTo(3));
                Assert.That(Convert.ToInt64(delivered, CultureInfo.InvariantCulture), Is.EqualTo(4));
                Assert.That(Convert.ToInt64(latestVersion, CultureInfo.InvariantCulture), Is.EqualTo(2));
            });
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }


    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private static Dictionary<string, string?> Configuration(string directory, bool smoke) => new()
    {
        ["Trading:Mode"] = "Simulated",
        ["Trading:DataDirectory"] = directory,
        ["Trading:SmokeMode"] = smoke.ToString(),
        ["Trading:GlobalRunConcurrency"] = "1",
        ["Trading:QueueCapacity"] = "2",
        ["Trading:LeaseSeconds"] = "30",
        ["Trading:ShutdownSeconds"] = "5",
        ["Research:Mode"] = "Fixture",
        ["Research:GlobalConcurrency"] = "2",
    };
}
