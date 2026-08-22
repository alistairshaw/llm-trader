using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;

namespace Trading.UI.Wpf.AcceptanceTests.Infrastructure;

internal sealed partial class ArtifactWriter(string scenarioName, string runDirectory)
{
    private const int MaximumTextLength = 256 * 1024;
    private readonly string artifactDirectory = Path.Combine(
        Environment.GetEnvironmentVariable("LLM_TRADER_WPF_ARTIFACT_DIRECTORY")
            ?? Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "wpf-ui"),
        SafeName(scenarioName));

    public void Capture(Window? window, string logPath)
    {
        Directory.CreateDirectory(artifactDirectory);
        if (window is not null)
        {
            try { FlaUI.Core.Capturing.Capture.Element(window).ToFile(Path.Combine(artifactDirectory, "safe-fixture-screen.png")); }
            catch (Exception exception) when (IsStaleUi(exception))
            { WriteRedacted("screenshot-error.txt", $"stale-ui:{exception.GetType().Name}"); }
            WriteRedacted("uia-tree.txt", BuildTree(window));
        }

        if (File.Exists(logPath)) WriteRedacted("application.log", File.ReadAllText(logPath));
        WriteRedacted("run.txt", $"scenario={scenarioName}{Environment.NewLine}runDirectory={runDirectory}");
    }

    private void WriteRedacted(string fileName, string value)
    {
        var bounded = value.Length <= MaximumTextLength ? value : value[..MaximumTextLength] + "\n[truncated]";
        var redacted = SecretPattern().Replace(bounded, "$1=[redacted]");
        File.WriteAllText(Path.Combine(artifactDirectory, fileName), redacted);
    }

    private static string BuildTree(AutomationElement root)
    {
        try
        {
            var lines = new List<string>();
            foreach (var element in root.FindAllDescendants().Prepend(root))
            {
                try { lines.Add($"{element.ControlType} id={element.AutomationId} name={element.Name}"); }
                catch (Exception exception) when (IsStaleUi(exception))
                { lines.Add($"Unavailable id=[unsupported] name=[unsupported] reason={exception.GetType().Name}"); }
            }
            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception exception) when (IsStaleUi(exception))
        {
            return $"UIA tree unavailable: {exception.GetType().Name}";
        }
    }

    private static bool IsStaleUi(Exception exception) => exception is
        FlaUI.Core.Exceptions.PropertyNotSupportedException or System.Runtime.InteropServices.COMException
        or InvalidOperationException;

    private static string SafeName(string value)
    {
        var safe = new string(value.Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-').ToArray());
        return safe.Length > 80 ? safe[..80] : safe;
    }

    [GeneratedRegex("(?i)(password|secret|token|authorization|api[-_]?key)\\s*[:=]\\s*[^\\s,;]+")]
    private static partial Regex SecretPattern();
}
