using System.Xml.Linq;

namespace Trading.UI.Wpf.Tests;

[TestFixture]
public sealed class ShellAccessibilityTests
{
    [Test]
    public void ShellXamlExposesStableAutomationNamesIdsLiveStateAndKeyboardNavigation()
    {
        var document = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "MainWindow.xaml"));
        var attributes = document.Descendants().SelectMany(element => element.Attributes()).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attributes.Count(attribute => attribute.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(7));
            Assert.That(attributes.Count(attribute => attribute.Name.LocalName.EndsWith(".Name", StringComparison.Ordinal)), Is.GreaterThanOrEqualTo(5));
            Assert.That(attributes.Any(attribute => attribute.Name.LocalName.EndsWith(".HeadingLevel", StringComparison.Ordinal)), Is.True);
            Assert.That(attributes.Any(attribute => attribute.Name.LocalName.EndsWith(".LiveSetting", StringComparison.Ordinal) && attribute.Value == "Polite"), Is.True);
            Assert.That(attributes.Any(attribute => attribute.Name.LocalName.EndsWith(".TabNavigation", StringComparison.Ordinal) && attribute.Value == "Cycle"), Is.True);
            Assert.That(document.Descendants().Any(element => element.Name.LocalName == "Button" &&
                element.Attributes().Any(attribute => attribute.Name.LocalName == "Command")), Is.True);
        }
    }
}
