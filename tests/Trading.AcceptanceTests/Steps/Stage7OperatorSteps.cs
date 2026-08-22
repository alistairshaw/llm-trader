using Reqnroll;
using Trading.AcceptanceTests.Support;

namespace Trading.AcceptanceTests.Steps;

[Binding, Scope(Tag = "stage7")]
public sealed class Stage7OperatorSteps(Stage7OperatorDriver driver)
{
    [Given("Operator Alice is not authorized to approve proposals")]
    public Task GivenAliceCannotApprove() => driver.StartUnauthorizedApprovalAsync();

    [Given("Proposal Alpha is awaiting human approval")]
    public void GivenAwaitingProposal() => driver.ArrangeAwaitingProposal();

    [When("Operator Alice requests approval of Proposal Alpha")]
    public Task WhenAliceApproves() => driver.RequestApprovalAsync();

    [Then("the approval request should be denied")]
    public void ThenApprovalDenied() => driver.AssertApprovalDenied();

    [Then("Proposal Alpha should remain awaiting human approval")]
    public Task ThenProposalRemainsPending() => driver.AssertProposalAwaitingAsync();

    [Then("the denial should be audited without exposing sensitive detail")]
    public Task ThenDenialAudited() => driver.AssertDenialAuditAsync();

    [Given("Bot Alpha is assigned to Portfolio Alpha on Account Alpha")]
    public Task GivenAssignedBot() => driver.StartAssignedBotAsync();

    [When("an authorized operator activates the platform kill switch")]
    public Task WhenPlatformSwitchActivated() => driver.ActivatePlatformSwitchAsync();

    [Then("new work for Bot Alpha should be blocked by the inherited platform switch")]
    public Task ThenBotWorkBlocked() => driver.AssertInheritedSwitchAsync();

    [Then("the switch change should identify the operator, reason, scope, and UTC time")]
    public Task ThenSwitchAudited() => driver.AssertSwitchAuditAsync();

    [Then("existing durable work should remain recoverable")]
    public void ThenDurableWorkRecoverable() => driver.AssertDurableWorkRecoverable();

    [Given("an operator observes the paper workflow for Portfolio Alpha")]
    public Task GivenObservedPortfolio() => driver.StartUpdateObservationAsync();

    [When("Order Alpha receives a partial Fill and then a final Fill")]
    public Task WhenOrderFills() => driver.DeliverFillsAsync();

    [Then("the operator should receive bounded ordered updates for both Fills")]
    public Task ThenOrderedFillUpdates() => driver.AssertOrderedFillUpdatesAsync();

    [Then("the final observed Order status should be Filled")]
    public Task ThenOrderFilled() => driver.AssertFinalOrderStatusAsync();

    [Given("the operator host has an active Bot Run and durable pending work")]
    public Task GivenActiveHost() => driver.StartActiveHostAsync();

    [When("application shutdown is requested")]
    public Task WhenShutdownRequested() => driver.StopAsync();

    [Then("new operator commands should stop being accepted")]
    public Task ThenCommandsStop() => driver.AssertCommandsStoppedAsync();

    [Then("the Generic Host should stop within its bounded shutdown period")]
    public void ThenHostStopped() => driver.AssertBoundedStop();

    [Then("active state should remain consistent and recoverable")]
    public void ThenStateRecoverable() => driver.AssertShutdownState();
}
