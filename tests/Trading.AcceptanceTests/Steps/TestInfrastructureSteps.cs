using Reqnroll;
using Trading.AcceptanceTests.Support;

namespace Trading.AcceptanceTests.Steps;

[Binding]
public sealed class TestInfrastructureSteps(Stage1ScenarioState state, ScenarioContext scenarioContext)
{
    [Given("a fresh Stage 1 scenario context")]
    public void GivenAFreshStage1ScenarioContext()
    {
        Assert.That(state.InfrastructureMarkerRecorded, Is.False);
        Assert.That(scenarioContext, Is.Not.Null);
    }

    [When("an infrastructure marker is recorded")]
    public void WhenAnInfrastructureMarkerIsRecorded()
    {
        state.InfrastructureMarkerRecorded = true;
    }

    [Then("the marker should be available to later steps")]
    public void ThenTheMarkerShouldBeAvailableToLaterSteps()
    {
        Assert.That(state.InfrastructureMarkerRecorded, Is.True);
    }
}
