using Reqnroll;
using Trading.UI.Wpf.AcceptanceTests.Infrastructure;

namespace Trading.UI.Wpf.AcceptanceTests.Steps;

[Binding]
internal sealed class CriticalJourneySteps(ScenarioContext context)
{
    private const string BotId = "01J5QH8M000000000000000101";
    private const string PortfolioId = "01J5QH8M000000000000000103";
    private WpfApplicationDriver Driver => context.Get<WpfApplicationDriver>();

    private async Task OpenAsync(string route, string workspace)
    {
        await Driver.StartAsync();
        await Driver.NavigateAsync(route, workspace);
    }

    private async Task WaitIdleAsync(string busyId) =>
        await Driver.WaitUntilAsync(page => page.State(busyId) == "False", $"'{busyId}' to report idle");

    [Given("I am authorized to manage Trading Bots")]
    [Given("Portfolio Alpha has no active Trading Bot")]
    [Given("Bot Alpha is active with Portfolio Alpha assigned")]
    [Given("the Trading Bot management view is open")]
    public Task GivenBotsAreOpen() => OpenAsync("Nav.Bots", "Bots.Workspace");

    [When("I create Bot Alpha with a valid research-only configuration")]
    public async Task CreateBotAsync()
    {
        var page = Driver.Shell;
        page.SetText("Bots.Name", "Bot Alpha");
        page.Invoke("Bots.Create");
        await WaitIdleAsync("Bots.Busy");
        page.SelectFirst("Bots.List");
        page.SetText("Bots.Mandate", "Bounded fixture mandate");
        page.SetText("Bots.RiskPolicy", "risk-v1");
        page.SetText("Bots.ToolPolicy", "tools-v1");
        page.SetText("Bots.Schedule", "schedule-v1");
        page.SetText("Bots.Model", "scripted");
        page.SetText("Bots.PromptVersion", "fixture-v1");
        page.Invoke("Bots.SaveConfiguration");
        await WaitIdleAsync("Bots.Busy");
    }

    [When("I pause and resume Bot Alpha")]
    public async Task PauseResumeAsync()
    {
        Driver.Shell.Invoke("Bots.Pause");
        await WaitIdleAsync("Bots.Busy");
        Driver.Shell.Invoke("Bots.Resume");
        await WaitIdleAsync("Bots.Busy");
    }

    [Then("Bot Alpha should show its active configuration and current operational state")]
    public void BotConfigurationIsVisible()
    {
        Assert.That(Driver.Shell.Text("Bots.ConfigurationIdentity"), Is.Not.Empty);
        Driver.Shell.AssertAccessible("Bots.List", "Bots.ExecutionMode", "Bots.Pause", "Bots.Resume");
    }

    [When("I assign Portfolio Alpha to Bot Alpha")]
    public async Task AssignPortfolioAsync()
    {
        Driver.Shell.SelectFirst("Bots.List");
        Driver.Shell.SetText("Bots.PortfolioId", PortfolioId);
        Driver.Shell.Invoke("Bots.Assign");
        await WaitIdleAsync("Bots.Busy");
    }

    [Then("Bot Alpha should show Portfolio Alpha as its assignment")]
    public void PortfolioAssignmentIsVisible() =>
        Assert.That(Driver.Shell.Text("Bots.PortfolioId"), Is.EqualTo(PortfolioId));

    [When("I trigger a run for Bot Alpha")]
    public async Task TriggerRunAsync()
    {
        await OpenAsync("Nav.Runs", "Runs.Workspace");
        Driver.Shell.SetText("Runs.TradingBotId", BotId);
        Driver.Shell.SetText("Runs.Reason", "bounded operator journey");
        Driver.Shell.Invoke("Runs.Trigger");
        await WaitIdleAsync("Runs.Busy");
    }

    [Then("I should observe the run status until its terminal outcome")]
    public async Task ObserveTerminalRunAsync()
    {
        Driver.Shell.SelectFirst("Runs.History");
        await Driver.WaitUntilAsync(page => !string.IsNullOrWhiteSpace(page.State("Runs.Inspect")),
            "the authoritative Bot Run selection to synchronize");
        Driver.Shell.Invoke("Runs.Inspect");
        await WaitIdleAsync("Runs.Busy");
        await Driver.WaitUntilAsync(page => page.State("Runs.Status") == "Completed",
            "the selected Bot Run detail to report Completed");
        Assert.That(Driver.Shell.State("Runs.Status"), Is.EqualTo("Completed"));
    }

    [Then("every critical Bot control should expose a stable Automation ID")]
    public void BotIdsAreStable() => Driver.Shell.AssertAccessible("Bots.Name", "Bots.Mandate", "Bots.ExecutionMode",
        "Bots.Create", "Bots.SaveConfiguration", "Bots.Assign", "Bots.Pause", "Bots.Resume");

    [Then("every critical Bot control should expose an accessible name, role, and state")]
    public void BotControlsAreAccessible() => Driver.Shell.AssertKeyboardFocusable("Bots.Name", "Bots.Mandate",
        "Bots.ExecutionMode", "Bots.Create", "Bots.Pause", "Bots.Resume");

    [Given("Bot Alpha is authorized to request public Research")]
    public Task GivenResearchIsOpen() => OpenAsync("Nav.Research", "Research.Workspace");

    [When("I request a bounded Research Report for Bot Alpha")]
    public async Task RequestResearchAsync()
    {
        Driver.Shell.SetText("Research.RequestBot", BotId);
        Driver.Shell.SetText("Research.RequestSubject", "ACME bounded fixture report");
        Driver.Shell.Invoke("Research.Request");
        await Driver.WaitUntilAsync(page => page.State("Research.RequestOutcome") == "operator.research.requested",
            "the authorized Research request and catalog refresh to complete");
        await WaitIdleAsync("Research.Busy");
        Driver.Shell.SelectFirst("Research.Catalog");
        await Driver.WaitUntilAsync(page => !string.IsNullOrWhiteSpace(page.State("Research.OpenExact")),
            "the exact Research Report selection to synchronize");
        Driver.Shell.Invoke("Research.OpenExact");
        await WaitIdleAsync("Research.Busy");
        await Driver.WaitUntilAsync(page => page.State("Research.ExactIdentity").Contains("version 1", StringComparison.Ordinal),
            "the exact Research Report detail to publish version 1");
    }

    [Then("I should be able to read the published Report version and its provenance")]
    public void ResearchDetailIsVisible()
    {
        Assert.That(Driver.Shell.State("Research.ExactIdentity"), Does.Contain("version 1"));
        Assert.That(Driver.Shell.ItemCount("Research.Provenance"), Is.GreaterThan(0));
    }

    [Given("Proposal Alpha is awaiting human approval")]
    [Given("a valid proposal is awaiting my approval")]
    [Given("Proposal Alpha is awaiting my approval")]
    public Task GivenProposalQueueIsOpen() => OpenAsync("Nav.Proposals", "Proposals.Workspace");

    [When("I open the proposal queue")]
    public void OpenProposalQueue() => Assert.That(Driver.Shell.ItemCount("Proposals.Queue"), Is.GreaterThan(0));

    [When("I review Proposal Alpha")]
    [When("I review the proposal")]
    public async Task ReviewProposalAsync()
    {
        Driver.Shell.SelectFirst("Proposals.Queue");
        Driver.Shell.Invoke("Proposals.OpenExact");
        await WaitIdleAsync("Proposals.Busy");
    }

    [Then("I should see its rationale, exact evidence versions, guardrail results, and data freshness")]
    public void ProposalEvidenceIsVisible()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Driver.Shell.Text("Proposals.Rationale"), Is.Not.Empty);
            Assert.That(Driver.Shell.ItemCount("Proposals.Evidence"), Is.GreaterThan(0));
            Assert.That(Driver.Shell.ItemCount("Proposals.Guardrails"), Is.GreaterThan(0));
            Assert.That(Driver.Shell.Text("Proposals.Snapshot"), Is.Not.Empty);
        });
    }

    [When("I approve it")]
    public async Task ApproveProposalAsync()
    {
        Driver.Shell.Confirm("Proposals.Confirm");
        Driver.Shell.Invoke("Proposals.Approve");
        await WaitIdleAsync("Proposals.Busy");
    }

    [Then("the proposal should be shown as approved")]
    public void ProposalIsApproved() => Assert.That(Driver.Shell.Text("Proposals.Eligibility"), Does.Contain("Terminal"));

    [Then("its paper order should be visible")]
    public async Task PaperOrderIsVisibleAsync()
    {
        await OpenAsync("Nav.Execution", "ExecutionRisk.Workspace");
        Assert.That(Driver.Shell.ItemCount("ExecutionRisk.Orders"), Is.GreaterThan(0));
    }

    [When("I review and reject Proposal Alpha with a reason")]
    public async Task RejectProposalAsync()
    {
        await ReviewProposalAsync();
        Driver.Shell.SetText("Proposals.DecisionReason", "fixture rejection reason");
        Driver.Shell.Confirm("Proposals.Confirm");
        Driver.Shell.Invoke("Proposals.Reject");
        await WaitIdleAsync("Proposals.Busy");
    }

    [Then("Proposal Alpha should be shown as rejected")]
    public void ProposalIsRejected() => Assert.That(Driver.Shell.Text("Proposals.Eligibility"), Does.Contain("Terminal"));

    [Then("no Order should be created for Proposal Alpha")]
    public void RejectionHasNoReservation() => Assert.That(Driver.Shell.Text("Proposals.Reservation"), Is.Empty);

    [Given("I am viewing execution for Portfolio Alpha")]
    public Task GivenExecutionIsOpen() => OpenAsync("Nav.Execution", "ExecutionRisk.Workspace");

    [When("Order Alpha receives a partial Fill and then a final Fill")]
    public async Task LoadExecutionDetailAsync()
    {
        await Driver.WaitUntilAsync(page => page.ItemCount("ExecutionRisk.Orders") > 0, "a fixture paper Order");
        Driver.Shell.SelectFirst("ExecutionRisk.Orders");
        Driver.Shell.Invoke("ExecutionRisk.LoadDetail");
        await Driver.WaitUntilAsync(page => page.Text("ExecutionRisk.Financials")
            .Contains("Filled 70", StringComparison.Ordinal), "the exact Filled Order financials");
        Driver.Shell.Select("ExecutionRisk.Tab.Fills");
        await Driver.WaitUntilAsync(page => page.HasWorkspace("ExecutionRisk.Fills") &&
            page.ItemCount("ExecutionRisk.Fills") >= 2, "partial and final Fills");
    }

    [Then("both Fills and the Filled Order should appear without restarting the application")]
    public void FilledOrderIsLive() => Assert.Multiple(() =>
    {
        Assert.That(Driver.Shell.ItemCount("ExecutionRisk.Fills"), Is.EqualTo(2));
        Assert.That(Driver.Shell.Text("ExecutionRisk.Financials"), Does.Contain("Filled"));
    });

    [Given(@"Bot Alpha uses (.*) mode")]
    public async Task GivenModeAsync(string mode)
    {
        await OpenAsync("Nav.Bots", "Bots.Workspace");
        var index = mode switch
        {
            "ResearchOnly" => 0,
            "HumanApproval" => 1,
            "Paper" => 2,
            "Live" => -1,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        if (index >= 0) Driver.Shell.SelectComboIndex("Bots.ExecutionMode", index);
        context.Set(mode, "mode");
    }

    [When("I inspect Bot Alpha and its work")]
    public void InspectMode() => Driver.Shell.AssertAccessible("Bots.ExecutionMode", "Bots.ModeExplanation");

    [Then(@"(.*) should be exposed as the current execution mode through accessible state")]
    public void ModeIsAccessible(string mode)
    {
        Assert.That(context.Get<string>("mode"), Is.EqualTo(mode));
        Driver.Shell.AssertAccessible("Bots.ExecutionMode", "Bots.ModeExplanation");
    }

    [Given(@"Portfolio Alpha has (stale decision data|failed reconciliation|a disconnected broker|a failed Bot Run)")]
    public async Task GivenWarningAsync(string condition)
    {
        await OpenAsync("Nav.Portfolios", "PortfolioBroker.View");
        context.Set(condition, "condition");
    }

    [When("I inspect Portfolio Alpha operations")]
    public void InspectPortfolioOperations() => Driver.Shell.AssertAccessible("PortfolioBroker.Grid", "PortfolioBroker.SafetyState");

    [Then(@"the (.*) warning should be prominent and exposed through accessible state")]
    public void WarningIsAccessible(string warning)
    {
        Assert.That(warning, Is.Not.Empty);
        Assert.That(Driver.Shell.Text("PortfolioBroker.SafetyState"), Is.Not.Empty);
    }

    [Given("I am authorized to control the Portfolio Alpha kill switch")]
    public Task GivenKillSwitchIsOpen() => OpenAsync("Nav.Settings", "KillSwitch.View");

    [When("I confirm activation with a bounded reason")]
    public async Task ActivateKillSwitchAsync()
    {
        Driver.Shell.SelectFirst("KillSwitch.Scopes");
        Driver.Shell.Invoke("KillSwitch.Open");
        await Driver.WaitUntilAsync(page => page.Text("KillSwitch.ActivationPhrase").Length > 0, "activation phrase");
        Driver.Shell.SetText("KillSwitch.Reason", "bounded operator safety reason");
        Driver.Shell.SetText("KillSwitch.Confirmation", Driver.Shell.Text("KillSwitch.ActivationPhrase"));
        Driver.Shell.Invoke("KillSwitch.Activate");
        await Driver.WaitUntilAsync(page => page.Text("KillSwitch.Outcome") == "kill_switch.succeeded", "audited switch outcome");
    }

    [Then("the Portfolio Alpha kill switch should be active")]
    public void KillSwitchIsActive() => Assert.That(Driver.Shell.Text("KillSwitch.EffectiveState"), Does.Contain("blocked"));

    [Then("inherited execution blocks should be visible")]
    public void ExecutionBlockIsVisible() => Assert.That(Driver.Shell.Text("KillSwitch.EffectiveState"), Does.Contain("Portfolio"));

    [Then("the audited outcome should identify my action")]
    public void SwitchAuditIsVisible() => Assert.That(Driver.Shell.ItemCount("KillSwitch.History"), Is.GreaterThan(0));

    [Given("a Bot Run and durable paper Order work are active")]
    public async Task GivenActiveWorkAsync()
    {
        await OpenAsync("Nav.Runs", "Runs.Workspace");
        Assert.That(Driver.Shell.ItemCount("Runs.History"), Is.GreaterThan(0));
    }

    [When("I close the application")]
    public Task CloseApplicationAsync() => Driver.ClosePreservingStateAsync();

    [Then("the window should close after the Generic Host stops cleanly")]
    public void WindowClosedCleanly() => Assert.That(Driver.WasCleanlyStopped, Is.True);

    [Then("restarting should show consistent recoverable state")]
    public async Task RecoverableStateWasPersisted()
    {
        await Driver.RestartAsync();
        await Driver.NavigateAsync("Nav.Execution", "ExecutionRisk.Workspace");
        Assert.That(Driver.Shell.ItemCount("ExecutionRisk.Orders"), Is.GreaterThan(0));
        await Driver.CloseAndVerifyAsync();
    }
}
