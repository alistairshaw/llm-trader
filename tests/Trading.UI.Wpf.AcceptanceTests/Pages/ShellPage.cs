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

    private AutomationElement Require(string automationId) =>
        window.FindFirstDescendant(factory => factory.ByAutomationId(automationId))
        ?? throw new InvalidOperationException($"Automation element '{automationId}' was not found.");
}
