using Microsoft.Data.Sqlite;

namespace Trading.TestInfrastructure;

internal static class SqliteTestDatabaseCleanup
{
    public static void DeleteOwnedDirectory(string directory, string connectionString, string? ownershipDiagnostic = null)
    {
        // EF closes pooled connections when their owning context/provider is disposed. Clearing only
        // this database's pool releases those already-closed native handles before Windows deletion.
        using var poolIdentity = new SqliteConnection(connectionString);
        SqliteConnection.ClearPool(poolIdentity);

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException exception)
        {
            var identity = new SqliteConnectionStringBuilder(connectionString);
            var boundedIdentity = $"path={Path.GetFullPath(identity.DataSource)};mode={identity.Mode};cache={identity.Cache};pooling={identity.Pooling};timeout={identity.DefaultTimeout}";
            throw new IOException($"First-attempt SQLite fixture deletion failed. identity=[{boundedIdentity}] ownership=[{ownershipDiagnostic ?? "not supplied"}]", exception);
        }
    }

    public static string ConnectionString(string databasePath) => new SqliteConnectionStringBuilder
    {
        DataSource = Path.GetFullPath(databasePath),
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        Pooling = true,
    }.ToString();

    public static string HostConnectionString(string databasePath) => new SqliteConnectionStringBuilder
    {
        DataSource = Path.GetFullPath(databasePath),
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared,
        Pooling = true,
    }.ToString();
}
