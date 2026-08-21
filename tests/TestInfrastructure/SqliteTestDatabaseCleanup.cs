using Microsoft.Data.Sqlite;

namespace Trading.TestInfrastructure;

internal static class SqliteTestDatabaseCleanup
{
    public static void DeleteOwnedDirectory(string directory, string connectionString)
    {
        // EF closes pooled connections when their owning context/provider is disposed. Clearing only
        // this database's pool releases those already-closed native handles before Windows deletion.
        using var poolIdentity = new SqliteConnection(connectionString);
        SqliteConnection.ClearPool(poolIdentity);

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public static string ConnectionString(string databasePath) => new SqliteConnectionStringBuilder
    {
        DataSource = Path.GetFullPath(databasePath),
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        Pooling = true,
    }.ToString();

    public static string HostConnectionString(string databasePath) =>
        $"Data Source={Path.GetFullPath(databasePath)};Default Timeout=5";
}
