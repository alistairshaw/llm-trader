using System.Xml.Linq;

namespace Trading.UI.Wpf.Tests;

[TestFixture]
public sealed class ShellAccessibilityTests
{
    [Test]
    public void Shell_xaml_exposes_stable_automation_names_ids_live_state_and_keyboard_navigation()
    {
        var document = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "MainWindow.xaml"));
        var attributes = document.Descendants().SelectMany(element => element.Attributes()).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attributes.Count(attribute => attribute.Name.LocalName == "AutomationId"), Is.GreaterThanOrEqualTo(7));
            Assert.That(attributes.Count(attribute => attribute.Name.LocalName == "Name"), Is.GreaterThanOrEqualTo(5));
            Assert.That(attributes.Any(attribute => attribute.Name.LocalName == "HeadingLevel"), Is.True);
            Assert.That(attributes.Any(attribute => attribute.Name.LocalName == "LiveSetting" && attribute.Value == "Polite"), Is.True);
            Assert.That(attributes.Any(attribute => attribute.Name.LocalName == "TabNavigation" && attribute.Value == "Cycle"), Is.True);
            Assert.That(document.Descendants().Any(element => element.Name.LocalName == "Button" &&
                element.Attributes().Any(attribute => attribute.Name.LocalName == "Command")), Is.True);
        }
    }
}
