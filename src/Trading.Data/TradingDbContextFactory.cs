using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Trading.Data;

public static class TradingDbContextFactory
{
    public static string CreateConnectionString(DatabaseOptions options, string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        var databasePath = options.ValidateAndGetFullPath(repositoryRoot);
        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        }.ToString();
    }

    public static DbContextOptions<TradingDbContext> CreateOptions(DatabaseOptions options, string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        var databasePath = options.ValidateAndGetFullPath(repositoryRoot);
        var directory = Path.GetDirectoryName(databasePath)!;
        Directory.CreateDirectory(directory);
        var connectionString = CreateConnectionString(options, repositoryRoot);

        return new DbContextOptionsBuilder<TradingDbContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable("__ef_migrations_history"))
            .AddInterceptors(new SqliteConnectionInterceptor(options.BusyTimeoutMilliseconds))
            .EnableSensitiveDataLogging(false)
            .Options;
    }
}
