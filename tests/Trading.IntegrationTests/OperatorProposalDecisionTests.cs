using Trading.Core.Identifiers;
using Trading.Core.Proposals;
using Trading.Engine.Operators;

namespace Trading.IntegrationTests;

[Category("OperatorProposalDecision")]
public sealed class OperatorProposalDecisionTests
{
    private static readonly TradeProposalId ProposalId = TradeProposalId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA10");

    [Test]
    public async Task AuthorizedApprovalCarriesActorExactVersionAndTrimmedReasonToEngineBoundary()
    {
        var workflow = new Workflow(); var service = new AuthorizedOperatorService(new Authorization(true), workflow);
        var principal = new OperatorPrincipal("reviewer-a", [OperatorAuthority.DecideProposals]);
        var result = await service.ApproveAsync(principal, ProposalId, 12, "  reviewed evidence  ", default);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
            Assert.That(workflow.Principal, Is.SameAs(principal));
            Assert.That(workflow.Command!.Kind, Is.EqualTo(OperatorCommandKind.ApproveProposal));
            Assert.That(workflow.Command.Resource, Is.EqualTo(new OperatorResource(OperatorResourceKind.TradeProposal, ProposalId.ToString())));
            Assert.That(workflow.Command.ExpectedVersion, Is.EqualTo(12));
            Assert.That(workflow.Command.Arguments["reason"], Is.EqualTo("reviewed evidence"));
        }
    }

    [Test]
    public async Task UnauthorizedProposalIsIndistinguishableFromMissingAndNeverReachesWorkflow()
    {
        var workflow = new Workflow(); var service = new AuthorizedOperatorService(new Authorization(false), workflow);
        var result = await service.RejectAsync(new("outsider", []), ProposalId, 4, "policy mismatch", default);
        Assert.Multiple(() => { Assert.That(result, Is.EqualTo(OperatorCommandResult.Unavailable())); Assert.That(workflow.Command, Is.Null); });
    }

    [Test]
    public async Task WorkflowPreservesStableStaleExpiredChangedTerminalAndConcurrencyOutcomes()
    {
        foreach (var expected in new[] { "proposal.stale", "proposal.expired", "proposal.content_changed", "proposal.terminal", "operator.conflict" })
        {
            var workflow = new Workflow { Result = new(OperatorResultStatus.Conflict, expected) };
            var service = new AuthorizedOperatorService(new Authorization(true), workflow);
            var result = await service.ApproveAsync(new("reviewer", []), ProposalId, 1, null, default);
            Assert.That(result.Code, Is.EqualTo(expected));
        }
    }

    private sealed class Authorization(bool allowed) : IOperatorAuthorization
    {
        public Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission, OperatorResource resource, CancellationToken token)
        { Assert.That(permission, Is.EqualTo(OperatorAuthority.DecideProposals)); return Task.FromResult(allowed); }
    }
    private sealed class Workflow : IOperatorWorkflowPort
    {
        public OperatorPrincipal? Principal { get; private set; }
        public OperatorCommand? Command { get; private set; }
        public OperatorCommandResult Result { get; set; } = new(OperatorResultStatus.Succeeded, "proposal.decided");
        public Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command, CancellationToken token)
        { Principal = principal; Command = command; return Task.FromResult(Result); }
        public Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page, OperatorResource resource, OperatorFilter filter, OperatorPageRequest request, CancellationToken token) => throw new NotSupportedException();
    }
}
