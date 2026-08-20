using Reqnroll;
using Trading.AcceptanceTests.Support;

namespace Trading.AcceptanceTests.Steps;

[Binding]
[Scope(Tag = "stage5")]
public sealed class Stage5GovernanceSteps(Stage5GovernanceDriver driver)
{
    [Given("(.*)")]
    public void Given(string text)
    {
        driver.Arrange(text);
    }

    [When("(.*)")]
    public void When(string text)
    {
        driver.Act(text);
    }

    [Then("(.*)")]
    public void Then(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        driver.AssertObserved();
    }
}
