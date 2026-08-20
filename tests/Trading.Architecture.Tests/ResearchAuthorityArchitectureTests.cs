namespace Trading.Architecture.Tests;

public sealed class ResearchAuthorityArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string[] ForbiddenAuthority =
    [
        "Trading.Core.Proposals", "Trading.Core.Orders", "Trading.Core.Brokers", "Trading.Data",
        "Microsoft.EntityFrameworkCore", "Microsoft.Data.Sqlite", "System.Windows",
    ];

    [Test]
    public void ResearchProductionCodeHasNoTradingOrInfrastructureAuthority()
    {
        var directory = Path.Combine(RepositoryRoot, "src", "Trading.Research");
        var violations = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => ForbiddenAuthority.Where(value => File.ReadAllText(path).Contains(value, StringComparison.Ordinal))
                .Select(value => $"{Path.GetFileName(path)} references {value}."))
            .ToArray();
        Assert.That(violations, Is.Empty);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TradingBot.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
