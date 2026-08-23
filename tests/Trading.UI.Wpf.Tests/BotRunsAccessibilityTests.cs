using System.Xml.Linq;

namespace Trading.UI.Wpf.Tests;

[TestFixture]
[Category("BotRuns")]
public sealed class BotRunsAccessibilityTests
{
    [Test]
    public void CriticalRunControlsAndStateExposeStableAutomationMetadata()
    {
        var document = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "BotRunsView.xaml"));
        var attributes = document.Descendants().SelectMany(x => x.Attributes()).ToArray();
        var ids = attributes.Where(x => x.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal))
            .Select(x => x.Value).ToArray();
        var inspect = document.Descendants().Single(x => x.Attributes().Any(a => a.Value == "Runs.Inspect"));
        var status = document.Descendants().Single(x => x.Attributes().Any(a => a.Value == "Runs.Status"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Does.Contain("Runs.Trigger"));
            Assert.That(ids, Does.Contain("Runs.Active"));
            Assert.That(ids, Does.Contain("Runs.Queued"));
            Assert.That(ids, Does.Contain("Runs.History"));
            Assert.That(ids, Does.Contain("Runs.FailureCode"));
            Assert.That(ids, Does.Contain("Runs.AcceptedSchedule"));
            Assert.That(ids, Does.Contain("Runs.Configuration"));
            Assert.That(ids, Does.Contain("Runs.Snapshot"));
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".LiveSetting", StringComparison.Ordinal) && x.Value == "Assertive"), Is.True);
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".ItemStatus", StringComparison.Ordinal) && x.Value == "{Binding IsBusy}"), Is.True);
            Assert.That(inspect.Attributes().Single(x => x.Name.LocalName.EndsWith(".ItemStatus", StringComparison.Ordinal)).Value,
                Is.EqualTo("{Binding SelectedRun.Id}"));
            Assert.That(status.Attributes().Single(x => x.Name.LocalName.EndsWith(".ItemStatus", StringComparison.Ordinal)).Value,
                Is.EqualTo("{Binding Detail.Summary.Status}"));
            Assert.That(attributes.Where(x => x.Name.LocalName.EndsWith(".Name", StringComparison.Ordinal)).All(x => !string.IsNullOrWhiteSpace(x.Value)), Is.True);
        }
    }
}
