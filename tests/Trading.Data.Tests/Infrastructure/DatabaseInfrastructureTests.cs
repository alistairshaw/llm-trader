using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Trading.Data.Tests.Infrastructure;

[TestFixture]
[Category("Infrastructure")]
internal sealed class DatabaseInfrastructureTests
{
    [Test]
    public void OptionsRequireAnAbsolutePathOutsideTheRepository()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "repository", Guid.NewGuid().ToString("N"));

        Assert.Multiple(() =>
        {
            Assert.That(
                () => new DatabaseOptions { DatabasePath = "relative.db" }.ValidateAndGetFullPath(repositoryRoot),
                Throws.InvalidOperationException.With.Message.Contains("absolute"));
            Assert.That(
                () => new DatabaseOptions { DatabasePath = Path.Combine(repositoryRoot, "runtime", "trading.db") }.ValidateAndGetFullPath(repositoryRoot),
                Throws.InvalidOperationException.With.Message.Contains("outside"));
        });
    }

    [Test]
    public async Task EachFixtureUsesAnIsolatedRealSqliteDatabase()
    {
        await using var first = await TemporarySqliteDatabase.CreateAsync();
        await using var second = await TemporarySqliteDatabase.CreateAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first.DatabasePath, Is.Not.EqualTo(second.DatabasePath));
            Assert.That(File.Exists(first.DatabasePath), Is.True);
            Assert.That(File.Exists(second.DatabasePath), Is.True);
            Assert.That(first.Context.Database.ProviderName, Is.EqualTo("Microsoft.EntityFrameworkCore.Sqlite"));
        });
    }

    [Test]
    public async Task EveryOpenedConnectionEnablesForeignKeysWalAndBoundedBusyTimeout()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();

        Assert.Multiple(() =>
        {
            Assert.That(ReadPragma(database.Context, "foreign_keys"), Is.EqualTo("1"));
            Assert.That(ReadPragma(database.Context, "journal_mode"), Is.EqualTo("wal").IgnoreCase);
            Assert.That(ReadPragma(database.Context, "busy_timeout"), Is.EqualTo(DatabaseOptions.DefaultBusyTimeoutMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        });
    }

    [Test]
    public async Task InitializationUsesTheMigrationPipeline()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var initializer = new DatabaseInitializer(database.Context);

        Assert.That(async () => await initializer.InitializeAsync(), Throws.Nothing);
        Assert.That(await database.Context.Database.GetPendingMigrationsAsync(), Is.Empty);
    }

    [Test]
    public void InvalidBusyTimeoutIsRejectedBeforeOpeningTheDatabase()
    {
        var options = new DatabaseOptions
        {
            DatabasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db"),
            BusyTimeoutMilliseconds = 60_001,
        };

        Assert.That(
            () => TradingDbContextFactory.CreateOptions(options, TestContext.CurrentContext.TestDirectory),
            Throws.InvalidOperationException.With.Message.Contains("busy timeout"));
    }

    private static string ReadPragma(TradingDbContext context, string pragma)
    {
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragma};";
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture)!;
    }
}
