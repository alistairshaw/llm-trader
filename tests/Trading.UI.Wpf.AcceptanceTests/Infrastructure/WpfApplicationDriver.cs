using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Trading.UI.Wpf.AcceptanceTests.Pages;

namespace Trading.UI.Wpf.AcceptanceTests.Infrastructure;

internal sealed class WpfApplicationDriver(string scenarioName) : IAsyncDisposable
{
    private readonly TimeSpan startupTimeout = TimeSpan.FromSeconds(45);
    private readonly TimeSpan interactionTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeSpan shutdownTimeout = TimeSpan.FromSeconds(30);
    private readonly string runId = Guid.NewGuid().ToString("N");
    private readonly string runDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlmTrader", "WpfAcceptanceRuns",
        Guid.NewGuid().ToString("N"));
    private Application? application;
    private Process? process;
    private Task? outputCapture;
    private int processId;
    private UIA3Automation? automation;
    private Window? window;
    private bool closed;
    private bool started;
    public bool WasCleanlyStopped { get; private set; }

    public ShellPage Shell => new(window ?? throw new InvalidOperationException("The WPF window is not ready."));

    public async Task StartAsync()
    {
        if (started) return;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("WPF acceptance requires Windows.");
        var executable = RequiredExecutable();
        Directory.CreateDirectory(runDirectory);
        var logPath = Path.Combine(runDirectory, "application.log");
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false,
        };
        startInfo.Environment["LLM_TRADER_WPF_TEST_PROFILE"] = "1";
        startInfo.Environment["LLM_TRADER_WPF_RUN_ID"] = runId;
        startInfo.Environment["LLM_TRADER_WPF_DATA_DIRECTORY"] = runDirectory;
        startInfo.Environment["LLM_TRADER_WPF_READY_FILE"] = Path.Combine(runDirectory, "ready.json");
        startInfo.Environment["LLM_TRADER_WPF_SHUTDOWN_FILE"] = Path.Combine(runDirectory, "shutdown.json");
        process = Process.Start(startInfo) ?? throw new InvalidOperationException("The WPF process could not be started.");
        processId = process.Id;
        application = Application.Attach(process);
        outputCapture = CaptureOutputAsync(process, logPath);

        try
        {
            await BoundedWait.UntilAsync(IsReady, startupTimeout, "the bounded WPF readiness signal");
            automation = new UIA3Automation();
            await BoundedWait.UntilAsync(() =>
            {
                window = application.GetMainWindow(automation, TimeSpan.FromSeconds(1));
                return window?.AutomationId == ShellPage.WindowId;
            }, startupTimeout, $"the main window AutomationId '{ShellPage.WindowId}'");
            started = true;
        }
        catch
        {
            CaptureFailure();
            throw;
        }
    }

    public async Task WaitUntilAsync(Func<ShellPage, bool> condition, string description) =>
        await BoundedWait.UntilAsync(() => condition(Shell), interactionTimeout, description);

    public async Task NavigateAsync(string routeId, string workspaceId)
    {
        Shell.Navigate(routeId);
        await BoundedWait.UntilAsync(() => Shell.HasWorkspace(workspaceId), interactionTimeout,
            $"workspace AutomationId '{workspaceId}'");
    }

    public async Task CloseAndVerifyAsync()
    {
        await CloseAsync(deleteRunDirectory: true);
    }

    public async Task ClosePreservingStateAsync() => await CloseAsync(deleteRunDirectory: false);

    public async Task RestartAsync()
    {
        if (!closed || !Directory.Exists(runDirectory))
            throw new InvalidOperationException("Only a cleanly stopped preserved fixture can be restarted.");
        File.Delete(Path.Combine(runDirectory, "ready.json"));
        File.Delete(Path.Combine(runDirectory, "shutdown.json"));
        closed = false;
        started = false;
        await StartAsync();
    }

    private async Task CloseAsync(bool deleteRunDirectory)
    {
        if (closed) return;
        window?.Close();
        await BoundedWait.UntilAsync(() => !IsOwnedProcessAlive(), shutdownTimeout, "the WPF process to exit");
        Assert.That(File.Exists(Path.Combine(runDirectory, "shutdown.json")), Is.True,
            "The bounded shutdown signal must exist.");
        if (outputCapture is not null) await outputCapture;
        closed = true;
        started = false;
        DisposeAutomation();
        application!.Dispose();
        application = null;
        process?.Dispose();
        process = null;
        WasCleanlyStopped = true;
        if (deleteRunDirectory) Directory.Delete(runDirectory, true);
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(runDirectory), Is.EqualTo(!deleteRunDirectory),
                deleteRunDirectory ? "Fixture directory must be deleted on the first attempt." : "Fixture state must remain for restart.");
            Assert.That(IsOwnedProcessAlive(), Is.False, "The owned WPF process must not remain orphaned.");
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (!closed)
        {
            CaptureFailure();
            if (IsOwnedProcessAlive())
            {
                using var owned = Process.GetProcessById(processId);
                owned.Kill(true);
            }
            await BoundedWait.UntilAsync(() => !IsOwnedProcessAlive(), shutdownTimeout, "forced WPF process cleanup");
            if (outputCapture is not null) await outputCapture;
            DisposeAutomation();
            application?.Dispose();
            application = null;
            process?.Dispose();
            process = null;
            if (Directory.Exists(runDirectory)) Directory.Delete(runDirectory, true);
        }
        else if (Directory.Exists(runDirectory))
        {
            Directory.Delete(runDirectory, true);
            Assert.That(Directory.Exists(runDirectory), Is.False,
                "A preserved restart fixture must be deleted on the first teardown attempt.");
        }
    }

    private bool IsReady()
    {
        var path = Path.Combine(runDirectory, "ready.json");
        if (!File.Exists(path)) return false;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.GetProperty("runId").GetString() != runId) throw new InvalidOperationException("Readiness run identity mismatch.");
        var state = root.GetProperty("state").GetString();
        if (state == "failed") throw new InvalidOperationException($"WPF startup failed: {root.GetProperty("reasonCode").GetString()}");
        return state == "ready";
    }

    private void CaptureFailure() => new ArtifactWriter(scenarioName, runDirectory).Capture(window,
        Path.Combine(runDirectory, "application.log"));

    private bool IsOwnedProcessAlive()
    {
        if (processId == 0) return false;
        try
        {
            using var owned = Process.GetProcessById(processId);
            return !owned.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void DisposeAutomation()
    {
        window = null;
        automation?.Dispose();
        automation = null;
    }

    private static async Task CaptureOutputAsync(Process process, string logPath)
    {
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(outputTask, errorTask);
        var output = await outputTask;
        var error = await errorTask;
        await File.WriteAllTextAsync(logPath, output + Environment.NewLine + error);
    }

    private static string RequiredExecutable()
    {
        var path = Environment.GetEnvironmentVariable("LLM_TRADER_WPF_EXECUTABLE");
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
            throw new InvalidOperationException("LLM_TRADER_WPF_EXECUTABLE must name the published WPF executable.");
        return Path.GetFullPath(path);
    }
}
