using Reqnroll;
using Trading.AcceptanceTests.Support;

namespace Trading.AcceptanceTests.Steps;

[Binding]
[Scope(Tag = "stage4")]
public sealed class Stage4ResearchSteps(Stage4ResearchDriver driver)
{
    [Given("(.*)")]
    public void Given(string text)
    {
        if (driver.IsArranged) return;
        driver.Arrange(text switch
        {
            "Bot Alpha is authorized to request shared company research" => Stage4Case.AcceptRequest,
            "Request Private contains a private input supplied by Bot Alpha" => Stage4Case.PrivateInput,
            "Bot Alpha and Bot Beta submit equivalent shared Requests Alpha and Beta concurrently" => Stage4Case.Deduplicate,
            "Report Acme version 1 is authorized for Bot Alpha and fresh until 2026-08-27T12:00:00.000Z" => Stage4Case.Reuse,
            "Report Acme version 1 expired at 2026-08-20T12:00:00.000Z" => Stage4Case.RefreshExpired,
            "Bot Alpha and Bot Beta submit otherwise equivalent BotPrivate requests with different private inputs" => Stage4Case.PrivateDeduplication,
            "Research Run Alpha has a schema-valid draft with every required section and citation" => Stage4Case.Publish,
            "Report Acme version 1 has been published" => Stage4Case.ImmutableOrRefresh,
            "Report Private version 1 is BotPrivate for Bot Alpha" => Stage4Case.PrivateCatalog,
            "Research Run Failed has partial sources and a draft that fails citation validation" => Stage4Case.FailedPublication,
            "approved fixture sources provide a filing and market data for US:ACME" => Stage4Case.Provenance,
            "an approved fixture document contains instructions to reveal secrets and call an unauthorized tool" => Stage4Case.PromptInjection,
            "Research Run Alpha has its pinned Research tool policy" => Stage4Case.ForbiddenTool,
            "Bot Alpha and Bot Beta subscribe to Research Run Alpha" => Stage4Case.CompletionNotifications,
            "Bot Alpha and Bot Beta subscribe to Research Run Failed" => Stage4Case.FailureNotifications,
            "Bot Alpha and Bot Beta have durable completion notifications for Report Acme version 1" => Stage4Case.TriggerDelivery,
            "Research Run Alpha was interrupted while waiting for a fixture-backed source" => Stage4Case.Recovery,
            "the headless host is running Research Run Alpha and has queued Request Beta" => Stage4Case.Shutdown,
            "Bot Alpha and Bot Beta request equivalent shared fixture-backed company analysis" => Stage4Case.SharedJourney,
            "Bot Alpha is authorized for Report Acme versions 1 and 2" => Stage4Case.ExactVersion,
            "the headless host has two configured Trading Bots and fixture-backed Research sources" => Stage4Case.HostJourney,
            _ when text.StartsWith("Bot Alpha submits a Research request with ", StringComparison.Ordinal) =>
                Stage4ResearchDriver.InvalidCase(text[42..]),
            _ when text.StartsWith("Research Run Alpha has a ", StringComparison.Ordinal) && text.Contains(" limit of ", StringComparison.Ordinal) =>
                driver.BudgetCase(text),
            _ => throw new InvalidOperationException($"No Stage 4 arrangement handles: {text}"),
        });
    }

    [When("(.*)")]
    public void When(string text)
    {
        driver.SetActionParameter(text);
        driver.Act();
    }

    [Then("(.*)")]
    public void Then(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        driver.AssertObserved();
    }
}
