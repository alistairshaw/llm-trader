namespace Trading.Architecture.Tests;

public sealed class ProductionProjectArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedReferences =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["Trading.Core"] = new HashSet<string>(StringComparer.Ordinal),
            ["Trading.Data"] = new HashSet<string>(["Trading.Core"], StringComparer.Ordinal),
            ["Trading.Brokers"] = new HashSet<string>(["Trading.Core"], StringComparer.Ordinal),
            ["Trading.Research"] = new HashSet<string>(["Trading.Core"], StringComparer.Ordinal),
            ["Trading.Engine"] = new HashSet<string>(
                ["Trading.Core", "Trading.Data", "Trading.Brokers", "Trading.Research"],
                StringComparer.Ordinal),
            ["Trading.Host"] = new HashSet<string>(
                ["Trading.Engine", "Trading.Data", "Trading.Brokers", "Trading.Research"],
                StringComparer.Ordinal),
            ["Trading.UI.Wpf"] = new HashSet<string>(
                ["Trading.Engine", "Trading.Data", "Trading.Brokers", "Trading.Research"],
                StringComparer.Ordinal),
        };

    private static readonly string[] CrossPlatformProjects =
    [
        "Trading.Core",
        "Trading.Data",
        "Trading.Brokers",
        "Trading.Engine",
        "Trading.Research",
        "Trading.Host",
    ];

    private static readonly string[] TestOnlyPackages =
    [
        "Microsoft.NET.Test.Sdk",
        "NUnit",
        "NUnit.Analyzers",
        "NUnit3TestAdapter",
        "Reqnroll.NUnit",
        "FlaUI.Core",
        "FlaUI.UIA3",
    ];

    [Test]
    public void ProductionProjectReferencesStayWithinAllowedBoundaries()
    {
        var violations = AllowedReferences.Keys
            .SelectMany(project => ProjectArchitecturePolicy.ValidateProjectReferences(
                project,
                ProjectArchitecturePolicy.ReadProjectReferences(ProjectPath(project)),
                AllowedReferences))
            .ToArray();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ForbiddenReferenceFixtureIsRejected()
    {
        var fixture = Path.Combine(
            RepositoryRoot,
            "tests",
            "ArchitectureFixtures",
            "ForbiddenCoreReference",
            "ForbiddenCoreReference.csproj");
        var violations = ProjectArchitecturePolicy.ValidateProjectReferences(
            "Trading.Core",
            ProjectArchitecturePolicy.ReadProjectReferences(fixture),
            AllowedReferences);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0], Does.Contain("Trading.UI.Wpf"));
    }

    [Test]
    public void ProductionProjectsTargetTheirApprovedFrameworks()
    {
        var violations = AllowedReferences.Keys
            .Select(project => new
            {
                Project = project,
                Actual = ProjectArchitecturePolicy.ReadProperty(ProjectPath(project), "TargetFramework"),
                Expected = project == "Trading.UI.Wpf" ? "net10.0-windows" : "net10.0",
            })
            .Where(result => !string.Equals(result.Actual, result.Expected, StringComparison.Ordinal))
            .Select(result => $"{result.Project} targets {result.Actual ?? "<missing>"}; expected {result.Expected}.")
            .ToArray();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CrossPlatformProjectsDoNotEnableWindowsDesktopOrUseWindowsNamespaces()
    {
        var violations = new List<string>();

        foreach (var project in CrossPlatformProjects)
        {
            var projectPath = ProjectPath(project);
            if (string.Equals(ProjectArchitecturePolicy.ReadProperty(projectPath, "UseWPF"), "true", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{project} enables WPF.");
            }

            var projectDirectory = Path.GetDirectoryName(projectPath)!;
            foreach (var sourceFile in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !IsGeneratedPath(path)))
            {
                var source = File.ReadAllText(sourceFile);
                if (source.Contains("System.Windows", StringComparison.Ordinal) ||
                    source.Contains("Microsoft.WindowsDesktop", StringComparison.Ordinal))
                {
                    violations.Add($"{project} uses a Windows-only namespace in {Path.GetFileName(sourceFile)}.");
                }
            }
        }

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ProductionProjectsDoNotReferenceTestOnlyPackagesOrAssemblies()
    {
        var violations = AllowedReferences.Keys.SelectMany(project =>
        {
            var projectPath = ProjectPath(project);
            var packageViolations = ProjectArchitecturePolicy.ReadPackageReferences(projectPath)
                .Where(package => TestOnlyPackages.Contains(package, StringComparer.OrdinalIgnoreCase))
                .Select(package => $"{project} references test-only package {package}.");
            var assemblyViolations = ProjectArchitecturePolicy.ReadProjectReferences(projectPath)
                .Where(reference => reference.EndsWith(".Tests", StringComparison.Ordinal) ||
                                    reference.EndsWith("TestSupport", StringComparison.Ordinal))
                .Select(reference => $"{project} references test assembly {reference}.");
            return packageViolations.Concat(assemblyViolations);
        }).ToArray();

        Assert.That(violations, Is.Empty);
    }

    private static string ProjectPath(string projectName) =>
        Path.Combine(RepositoryRoot, "src", projectName, $"{projectName}.csproj");

    private static bool IsGeneratedPath(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TradingBot.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
