using System.IO;
using System.Text.Json;

namespace Trading.UI.Wpf.Services;

public sealed record WpfTestProfileRuntime(
    string RunId,
    string DataDirectory,
    string ReadyFile,
    string ShutdownFile)
{
    public const string EnabledVariable = "LLM_TRADER_WPF_TEST_PROFILE";

    public static WpfTestProfileRuntime? FromEnvironment()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnabledVariable), "1", StringComparison.Ordinal))
            return null;

        var profile = new WpfTestProfileRuntime(
            Required("LLM_TRADER_WPF_RUN_ID"),
            RequiredPath("LLM_TRADER_WPF_DATA_DIRECTORY"),
            RequiredPath("LLM_TRADER_WPF_READY_FILE"),
            RequiredPath("LLM_TRADER_WPF_SHUTDOWN_FILE"));
        profile.Validate();
        return profile;
    }

    public IReadOnlyDictionary<string, string?> Configuration => new Dictionary<string, string?>
    {
        ["Trading:Mode"] = "Simulated",
        ["Trading:DataDirectory"] = DataDirectory,
        ["Trading:OperatorMode"] = "true",
        ["Trading:WpfTestProfile"] = "true",
        ["Trading:ShutdownSeconds"] = "10",
        ["Research:Mode"] = "Fixture",
        ["Research:FixtureVersion"] = "v1",
        ["Research:ModelProvider"] = "scripted",
        ["Research:ModelId"] = "research",
    };

    public void PublishReady() => Publish(ReadyFile, "ready", null);
    public void PublishFailed(string reasonCode) => Publish(ReadyFile, "failed", BoundedCode(reasonCode));
    public void PublishStopped() => Publish(ShutdownFile, "stopped", null);

    private void Validate()
    {
        if (RunId.Length is < 1 or > 64 || RunId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new InvalidOperationException("The WPF test run identity is invalid.");
        if (!IsWithin(DataDirectory, ReadyFile) || !IsWithin(DataDirectory, ShutdownFile))
            throw new InvalidOperationException("WPF test signals must remain inside the isolated runtime directory.");
        if (string.Equals(ReadyFile, ShutdownFile, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WPF readiness and shutdown signals require distinct files.");
    }

    private void Publish(string path, string state, string? reasonCode)
    {
        Validate();
        Directory.CreateDirectory(DataDirectory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new { schemaVersion = 1, runId = RunId, state, reasonCode }));
        File.Move(temporary, path, true);
    }

    private static bool IsWithin(string directory, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value)
        ? value.Trim()
        : throw new InvalidOperationException($"{name} is required by the WPF test profile.");

    private static string RequiredPath(string name)
    {
        var value = Required(name);
        return Path.IsPathFullyQualified(value) ? Path.GetFullPath(value) : throw new InvalidOperationException($"{name} must be an absolute path.");
    }

    private static string BoundedCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var code = new string(value.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-').Take(80).ToArray());
        return code.Length > 0 ? code : "startup-error";
    }
}
