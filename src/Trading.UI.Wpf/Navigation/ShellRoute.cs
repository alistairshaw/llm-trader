namespace Trading.UI.Wpf.Navigation;

public sealed record ShellRoute(string Key, string Title, string AutomationId)
{
    public static IReadOnlyList<ShellRoute> All { get; } =
    [
        new("bots", "Bots", "Nav.Bots"),
        new("portfolios", "Portfolios", "Nav.Portfolios"),
        new("runs", "Runs", "Nav.Runs"),
        new("research", "Research", "Nav.Research"),
        new("proposals", "Proposals", "Nav.Proposals"),
        new("execution", "Execution", "Nav.Execution"),
        new("risk", "Risk", "Nav.Risk"),
        new("settings", "Settings", "Nav.Settings"),
    ];
}
