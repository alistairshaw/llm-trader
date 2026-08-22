using Reqnroll;
using Trading.UI.Wpf.AcceptanceTests.Infrastructure;

namespace Trading.UI.Wpf.AcceptanceTests.Steps;

[Binding]
internal sealed class HarnessSmokeSteps(ScenarioContext scenarioContext)
{
    private WpfApplicationDriver Driver => scenarioContext.Get<WpfApplicationDriver>();

    [BeforeScenario("ui")]
    public void CreateDriver() => scenarioContext.Set(new WpfApplicationDriver(scenarioContext.ScenarioInfo.Title));

    [AfterScenario("ui")]
    public async Task DisposeDriverAsync()
    {
        if (scenarioContext.TryGetValue<WpfApplicationDriver>(out var driver)) await driver.DisposeAsync();
    }

    [Given("the deterministic WPF application is ready")]
    public async Task GivenTheApplicationIsReady()
    {
        await Driver.StartAsync();
        Assert.That(Driver.Shell.IsDisplayed, Is.True);
    }

    [When("I navigate to the Bot Runs workspace")]
    public Task WhenINavigateToBotRuns() => Driver.NavigateAsync("Nav.Runs", "Runs.Workspace");

    [Then("the Bot Runs workspace is displayed")]
    public void ThenBotRunsIsDisplayed() => Assert.That(Driver.Shell.HasWorkspace("Runs.Workspace"), Is.True);

    [Then("the application closes without an orphan or fixture data")]
    public Task ThenApplicationClosesCleanly() => Driver.CloseAndVerifyAsync();
}
