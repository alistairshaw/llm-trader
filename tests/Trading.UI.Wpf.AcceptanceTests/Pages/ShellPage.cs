using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Trading.UI.Wpf.AcceptanceTests.Pages;

internal sealed class ShellPage(Window window)
{
    public const string WindowId = "Shell.Window";

    public bool IsDisplayed => window.AutomationId == WindowId;

    public void Navigate(string routeAutomationId)
    {
        var route = Require(routeAutomationId);
        if (route.ControlType != ControlType.Button)
            throw new InvalidOperationException($"Automation element '{routeAutomationId}' is not a button.");
        route.AsButton().Invoke();
    }

    public bool HasWorkspace(string automationId) =>
        window.FindFirstDescendant(factory => factory.ByAutomationId(automationId)) is not null;

    public string Text(string automationId)
    {
        var element = Require(automationId);
        return element.Patterns.Value.IsSupported ? element.Patterns.Value.Pattern.Value : element.Name ?? string.Empty;
    }

    public void SetText(string automationId, string value) => Require(automationId).AsTextBox().Text = value;

    public void Invoke(string automationId) => Require(automationId).AsButton().Invoke();

    public void SelectFirst(string automationId)
    {
        var container = Require(automationId);
        var item = container.FindFirstDescendant(factory => factory.ByControlType(ControlType.DataItem)) ??
            container.FindFirstDescendant(factory => factory.ByControlType(ControlType.ListItem)) ??
            throw new InvalidOperationException($"Automation element '{automationId}' contains no selectable item.");
        item.Patterns.SelectionItem.Pattern.Select();
    }

    public void SelectComboIndex(string automationId, int index) => Require(automationId).AsComboBox().Select(index);

    public void Confirm(string automationId)
    {
        var checkBox = Require(automationId).AsCheckBox();
        if (checkBox.IsChecked != true) checkBox.Toggle();
    }

    public int ItemCount(string automationId)
    {
        var container = Require(automationId);
        var data = container.FindAllDescendants(factory => factory.ByControlType(ControlType.DataItem));
        if (data.Length > 0) return data.Length;
        var list = container.FindAllDescendants(factory => factory.ByControlType(ControlType.ListItem));
        return list.Length > 0 ? list.Length : container.FindAllChildren().Length;
    }

    public void AssertAccessible(params string[] automationIds)
    {
        foreach (var id in automationIds)
        {
            var element = Require(id);
            Assert.Multiple(() =>
            {
                Assert.That(element.AutomationId, Is.EqualTo(id), $"stable AutomationId for '{id}'");
                Assert.That(element.Name, Is.Not.Null.And.Not.Empty, $"accessible name for '{id}'");
                Assert.That(element.ControlType, Is.Not.EqualTo(ControlType.Unknown), $"UIA role for '{id}'");
                Assert.That(element.IsEnabled, Is.TypeOf<bool>(), $"accessible enabled state for '{id}'");
            });
        }
    }

    public void AssertKeyboardFocusable(params string[] automationIds)
    {
        foreach (var id in automationIds)
        {
            var element = Require(id);
            element.Focus();
            Assert.That(element.Properties.HasKeyboardFocus.Value, Is.True, $"keyboard focus for '{id}'");
        }
    }

    private AutomationElement Require(string automationId) =>
        window.FindFirstDescendant(factory => factory.ByAutomationId(automationId))
        ?? throw new InvalidOperationException($"Automation element '{automationId}' was not found.");
}
