using System.Text.Json;
using Trading.UI.Wpf.Services;

namespace Trading.UI.Wpf.Tests;

[TestFixture, Category("WpfTestProfile")]
public sealed class WpfTestProfileTests
{
    [Test]
    public void ProfileIsPaperOnlyFixtureBackedAndPublishesBoundedRedactedSignals()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wpf-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var profile = new WpfTestProfileRuntime("test-run-1", directory,
                Path.Combine(directory, "ready.json"), Path.Combine(directory, "shutdown.json"));

            Assert.Multiple(() =>
            {
                Assert.That(profile.Configuration["Trading:Mode"], Is.EqualTo("Simulated"));
                Assert.That(profile.Configuration["Trading:WpfTestProfile"], Is.EqualTo("true"));
                Assert.That(profile.Configuration["Research:Mode"], Is.EqualTo("Fixture"));
                Assert.That(profile.Configuration.Keys, Has.None.Contains("Credential"));
                Assert.That(profile.Configuration.Keys, Has.None.Contains("Url"));
            });

            profile.PublishReady();
            profile.PublishStopped();
            AssertSignal(profile.ReadyFile, "test-run-1", "ready");
            AssertSignal(profile.ShutdownFile, "test-run-1", "stopped");
        }
        finally
        {
            Directory.Delete(directory, true);
            Assert.That(Directory.Exists(directory), Is.False);
        }
    }

    [Test]
    public void SignalOutsideRuntimeDirectoryIsRejectedBeforeWriting()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wpf-profile-{Guid.NewGuid():N}");
        var escaped = Path.Combine(Path.GetTempPath(), $"escaped-ready-{Guid.NewGuid():N}.json");
        var profile = new WpfTestProfileRuntime("test-run-2", directory,
            escaped, Path.Combine(directory, "shutdown.json"));

        Assert.That(profile.PublishReady, Throws.InstanceOf<InvalidOperationException>());
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(directory), Is.False);
            Assert.That(File.Exists(escaped), Is.False);
        });
    }

    private static void AssertSignal(string path, string runId, string state)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.EnumerateObject().Count(), Is.EqualTo(4));
            Assert.That(document.RootElement.GetProperty("runId").GetString(), Is.EqualTo(runId));
            Assert.That(document.RootElement.GetProperty("state").GetString(), Is.EqualTo(state));
        });
    }
}
