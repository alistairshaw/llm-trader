using Trading.Engine.Operators;

namespace Trading.IntegrationTests;

[TestFixture, Category("OperatorKillSwitch")]
public sealed class OperatorKillSwitchTests
{
    [Test]
    public async Task AuthorizedChangeCarriesFreshVersionReasonAndExactConfirmation()
    {
        var workflow = new Workflow();
        var service = new AuthorizedOperatorService(new Authorization(true), workflow);
        var principal = new OperatorPrincipal("operator-1", [OperatorAuthority.ManageKillSwitches]);
        var scope = new OperatorResource(OperatorResourceKind.Portfolio, "portfolio-7");

        await service.ActivateAsync(principal, scope, 12, " volatility limit ",
            "ACTIVATE Portfolio portfolio-7", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workflow.Command!.Kind, Is.EqualTo(OperatorCommandKind.ActivateKillSwitch));
            Assert.That(workflow.Command.Resource, Is.EqualTo(scope));
            Assert.That(workflow.Command.ExpectedVersion, Is.EqualTo(12));
            Assert.That(workflow.Command.Arguments["reason"], Is.EqualTo("volatility limit"));
            Assert.That(workflow.Command.Arguments["confirmation"], Is.EqualTo("ACTIVATE Portfolio portfolio-7"));
        }
    }

    [Test]
    public async Task UnauthorizedScopeReturnsUnavailableWithoutCallingWorkflow()
    {
        var workflow = new Workflow();
        var service = new AuthorizedOperatorService(new Authorization(false), workflow);
        var result = await service.ClearAsync(new("operator-1", []),
            new(OperatorResourceKind.BrokerAccount, "secret-account"), 4, "resolved",
            "CLEAR BrokerAccount secret-account", CancellationToken.None);

        Assert.That(result, Is.EqualTo(OperatorCommandResult.Unavailable()));
        Assert.That(workflow.Command, Is.Null);
    }

    private sealed class Authorization(bool allowed) : IOperatorAuthorization
    {
        public Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission,
            OperatorResource resource, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class Workflow : IOperatorWorkflowPort
    {
        public OperatorCommand? Command { get; private set; }
        public Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
            OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command,
            CancellationToken cancellationToken)
        {
            Command = command;
            return Task.FromResult(new OperatorCommandResult(OperatorResultStatus.Succeeded, "changed"));
        }
    }
}
