using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Trading.Data;

public static class TradingDbContextFactory
{
    public static DbContextOptions<TradingDbContext> CreateOptions(DatabaseOptions options, string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        var databasePath = options.ValidateAndGetFullPath(repositoryRoot);
        var directory = Path.GetDirectoryName(databasePath)!;
        Directory.CreateDirectory(directory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        }.ToString();

        return new DbContextOptionsBuilder<TradingDbContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable("__ef_migrations_history"))
            .AddInterceptors(new SqliteConnectionInterceptor(options.BusyTimeoutMilliseconds))
            .EnableSensitiveDataLogging(false)
            .Options;
    }
}
