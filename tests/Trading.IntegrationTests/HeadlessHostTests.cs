using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Trading.Data;
using Trading.Engine.Runtime;
using Trading.Host;
using Trading.TestInfrastructure;

namespace Trading.IntegrationTests;

[TestFixture]
[Category("HeadlessHost")]
[Category("ResearchHost")]
public sealed class HeadlessHostTests
{
    private static readonly string[] ExpectedDatabaseOwners =
    [
        "TradingDbContext registration and scoped repositories",
        "TradingRuntimeHostedService smoke scope",
        "external smoke inspection",
    ];

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

    [Test, Category("Stage5Host")]
    public async Task SmokeModeMigratesSeedsRunsAndStopsCleanly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "headless-host", Guid.NewGuid().ToString("N"));
        string? ownershipDiagnostic = null;
        try
        {
            var host = HostBootstrap.Build([], builder => builder.Configuration.AddInMemoryCollection(Configuration(directory, smoke: true)));
            try
            {
                var readiness = host.Services.GetRequiredService<RuntimeReadiness>();
                var database = host.Services.GetRequiredService<HostDatabaseIdentity>();
                ownershipDiagnostic = database.DiagnosticIdentity;
                Assert.Multiple(() =>
                {
                    Assert.That(database.DatabasePath, Is.EqualTo(Path.GetFullPath(Path.Combine(directory, "smoke.db"))));
                    Assert.That(database.Owners.Select(x => x.Name), Is.EquivalentTo(ExpectedDatabaseOwners));
                    Assert.That(database.Owners, Has.All.Property(nameof(HostDatabaseOwner.DisposalBoundary)).Not.Empty);
                });
                using (var scope = host.Services.CreateScope())
                    Assert.That(scope.ServiceProvider.GetRequiredService<IToolDispatcher>().Definitions.Select(x => x.Name),
                        Does.Contain(StageFourTradingTools.GetReport).And.Contain(StageFiveTradingTools.ProposeTrade));
                await host.RunAsync();
                await using (var connection = new SqliteConnection(database.ConnectionString))
                {
                    await connection.OpenAsync();
                    var status = await ScalarAsync(connection, "SELECT status FROM bot_runs");
                    var reports = await ScalarAsync(connection, "SELECT COUNT(*) FROM research_reports");
                    var completed = await ScalarAsync(connection, "SELECT COUNT(*) FROM research_requests WHERE status = 'Completed'");
                    var delivered = await ScalarAsync(connection, "SELECT COUNT(*) FROM research_subscriptions WHERE notification_status = 'Delivered'");
                    var latestVersion = await ScalarAsync(connection, "SELECT MAX(version_number) FROM research_reports");
                    var proposals = await ScalarAsync(connection, "SELECT COUNT(*) FROM trade_proposals");
                    var evaluations = await ScalarAsync(connection, "SELECT COUNT(*) FROM guardrail_evaluations");
                    var approvals = await ScalarAsync(connection, "SELECT COUNT(*) FROM proposal_approvals");
                    var reservations = await ScalarAsync(connection, "SELECT COUNT(*) FROM capital_reservations WHERE status = 'Active'");
                    var reserved = await ScalarAsync(connection, "SELECT amount FROM capital_reservations WHERE status = 'Active'");
                    var invalid = await ScalarAsync(connection, "SELECT status FROM trade_proposals WHERE id = '01J5QH8M000000000000000403'");
                    var researchOnly = await ScalarAsync(connection, "SELECT c.execution_mode || ':' || p.status FROM trade_proposals p JOIN trading_bot_configuration_versions c ON c.id = p.configuration_version_id WHERE p.id = '01J5QH8M000000000000000404'");
                    var initialHash = await ScalarAsync(connection, "SELECT content_hash FROM guardrail_evaluations WHERE trade_proposal_id = '01J5QH8M000000000000000401' ORDER BY evaluation_sequence LIMIT 1");
                    var freshHash = await ScalarAsync(connection, "SELECT content_hash FROM guardrail_evaluations WHERE trade_proposal_id = '01J5QH8M000000000000000401' ORDER BY evaluation_sequence DESC LIMIT 1");
                    Assert.Multiple(() =>
                    {
                        Assert.That(File.Exists(Path.Combine(directory, "smoke.db")), Is.True);
                        Assert.That(readiness.IsReady, Is.False);
                        Assert.That(status, Is.EqualTo("Completed"));
                        Assert.That(Convert.ToInt64(reports, CultureInfo.InvariantCulture), Is.EqualTo(3));
                        Assert.That(Convert.ToInt64(completed, CultureInfo.InvariantCulture), Is.EqualTo(3));
                        Assert.That(Convert.ToInt64(delivered, CultureInfo.InvariantCulture), Is.EqualTo(4));
                        Assert.That(Convert.ToInt64(latestVersion, CultureInfo.InvariantCulture), Is.EqualTo(2));
                        Assert.That(Convert.ToInt64(proposals, CultureInfo.InvariantCulture), Is.EqualTo(4));
                        Assert.That(Convert.ToInt64(evaluations, CultureInfo.InvariantCulture), Is.EqualTo(6));
                        Assert.That(Convert.ToInt64(approvals, CultureInfo.InvariantCulture), Is.EqualTo(2));
                        Assert.That(Convert.ToInt64(reservations, CultureInfo.InvariantCulture), Is.EqualTo(1));
                        Assert.That(reserved, Is.EqualTo("700"));
                        Assert.That(invalid, Is.EqualTo("Rejected"));
                        Assert.That(researchOnly, Is.EqualTo("ResearchOnly:Rejected"));
                        Assert.That(initialHash, Is.Not.EqualTo(freshHash));
                    });
                }
            }
            finally
            {
                if (host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync();
                else host.Dispose();
            }
        }
        finally
        {
            SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory,
                TradingDbContextFactory.CreateConnectionString(
                    new DatabaseOptions { DatabasePath = Path.Combine(directory, "smoke.db") }, AppContext.BaseDirectory),
                ownershipDiagnostic);
        }
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
