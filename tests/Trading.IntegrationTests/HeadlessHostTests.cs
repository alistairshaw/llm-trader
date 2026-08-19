using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Trading.Host;

namespace Trading.IntegrationTests;

[TestFixture]
[Category("HeadlessHost")]
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
    public async Task SmokeModeMigratesSeedsRunsAndStopsCleanly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "headless-host", Guid.NewGuid().ToString("N"));
        try
        {
            using var host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(Configuration(directory, smoke: true)));
            var readiness = host.Services.GetRequiredService<RuntimeReadiness>();
            await host.RunAsync();
            await using var connection = new SqliteConnection($"Data Source={Path.Combine(directory, "smoke.db")}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT status FROM bot_runs";
            var status = await command.ExecuteScalarAsync();
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(Path.Combine(directory, "smoke.db")), Is.True);
                Assert.That(readiness.IsReady, Is.False);
                Assert.That(status, Is.EqualTo("Completed"));
            });
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
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
    };
}
