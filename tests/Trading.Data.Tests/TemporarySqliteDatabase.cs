using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Trading.TestInfrastructure;

namespace Trading.Data.Tests;

internal sealed class TemporarySqliteDatabase : IAsyncDisposable
{
    private TemporarySqliteDatabase(string directoryPath, string databasePath, TradingDbContext context)
    {
        DirectoryPath = directoryPath;
        DatabasePath = databasePath;
        Context = context;
    }

    public string DirectoryPath { get; }
    public string DatabasePath { get; }
    public TradingDbContext Context { get; }

    public static async Task<TemporarySqliteDatabase> CreateAsync()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "trading-data-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directoryPath, "test.db");
        var options = new DatabaseOptions { DatabasePath = databasePath };
        var context = new TradingDbContext(TradingDbContextFactory.CreateOptions(options, TestContext.CurrentContext.TestDirectory));
        await context.Database.OpenConnectionAsync().ConfigureAwait(false);
        return new TemporarySqliteDatabase(directoryPath, databasePath, context);
    }

    public async ValueTask DisposeAsync()
    {
        var connectionString = Context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The temporary SQLite database has no connection string.");
        await Context.Database.CloseConnectionAsync().ConfigureAwait(false);
        await Context.DisposeAsync().ConfigureAwait(false);
        SqliteTestDatabaseCleanup.DeleteOwnedDirectory(DirectoryPath, connectionString);
    }
}
