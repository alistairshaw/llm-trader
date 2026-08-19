namespace Trading.Data;

public sealed class DatabaseOptions
{
    public const int DefaultBusyTimeoutMilliseconds = 5_000;

    public required string DatabasePath { get; init; }

    public int BusyTimeoutMilliseconds { get; init; } = DefaultBusyTimeoutMilliseconds;

    public string ValidateAndGetFullPath(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        if (!Path.IsPathFullyQualified(DatabasePath))
        {
            throw new InvalidOperationException("The database path must be absolute.");
        }

        if (BusyTimeoutMilliseconds is < 1 or > 60_000)
        {
            throw new InvalidOperationException("The database busy timeout must be between 1 and 60000 milliseconds.");
        }

        var fullDatabasePath = Path.GetFullPath(DatabasePath);
        var fullRepositoryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        var relativePath = Path.GetRelativePath(fullRepositoryRoot, fullDatabasePath);
        if (relativePath == "." ||
            (!Path.IsPathFullyQualified(relativePath) &&
             relativePath != ".." &&
             !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The database path must be outside the repository source tree.");
        }

        return fullDatabasePath;
    }
}
