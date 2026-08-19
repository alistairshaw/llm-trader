using System.Xml.Linq;

namespace Trading.Architecture.Tests;

internal static class ProjectArchitecturePolicy
{
    internal static IReadOnlyList<string> ValidateProjectReferences(
        string projectName,
        IEnumerable<string> referencedProjects,
        IReadOnlyDictionary<string, IReadOnlySet<string>> allowedReferences)
    {
        var allowed = allowedReferences[projectName];

        return referencedProjects
            .Where(reference => !allowed.Contains(reference))
            .Select(reference => $"{projectName} must not reference {reference}.")
            .ToArray();
    }

    internal static string[] ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);

        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileNameWithoutExtension(path!.Replace('\\', '/')))
            .ToArray();
    }

    internal static string? ReadProperty(string projectPath, string propertyName)
    {
        var document = XDocument.Load(projectPath);
        return document.Descendants(propertyName).Select(element => element.Value).SingleOrDefault();
    }

    internal static string[] ReadPackageReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);

        return document
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(package => !string.IsNullOrWhiteSpace(package))
            .Select(package => package!)
            .ToArray();
    }
}
