using System.Xml.Linq;

namespace Trading.UI.Wpf.Tests;

[TestFixture]
[Category("BotManagement")]
public sealed class BotManagementAccessibilityTests
{
    [Test]
    public void BotManagementControlsExposeStableAutomationMetadataAndTextualModes()
    {
        var document = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "BotManagementView.xaml"));
        var attributes = document.Descendants().SelectMany(x => x.Attributes()).ToArray();
        var ids = attributes.Where(x => x.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal)).Select(x => x.Value).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Does.Contain("Bots.List"));
            Assert.That(ids, Does.Contain("Bots.ExecutionMode"));
            Assert.That(ids, Does.Contain("Bots.ConfigurationIdentity"));
            Assert.That(ids, Does.Contain("Bots.Retire"));
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".LiveSetting", StringComparison.Ordinal) && x.Value == "Polite"), Is.True);
            Assert.That(attributes.Any(x => x.Name.LocalName.EndsWith(".ItemStatus", StringComparison.Ordinal) && x.Value == "{Binding IsBusy}"), Is.True);
            Assert.That(document.ToString(), Does.Contain("ResearchOnly"));
            Assert.That(document.ToString(), Does.Contain("HumanApproval"));
            Assert.That(document.ToString(), Does.Contain("PaperTrading"));
        }
    }
}
