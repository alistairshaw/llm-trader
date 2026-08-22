using Reqnroll;
using Trading.AcceptanceTests.Support;

namespace Trading.AcceptanceTests.Steps;

[Binding, Scope(Tag = "stage6")]
public sealed class Stage6ExecutionSteps(Stage6ExecutionDriver driver)
{
    [Given("(.*)")]
    public static void Given(string text) => Stage6ExecutionDriver.Arrange(text);

    [When("(.*)")]
    public Task When(string text) => driver.ActAsync(text);

    [Then("(.*)")]
    public void Then(string text) => driver.AssertObserved(text);
}
