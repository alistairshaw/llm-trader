using Trading.Engine.Operators;

namespace Trading.IntegrationTests;

[TestFixture, Category("OperatorContracts")]
public sealed class OperatorAuthorizationBoundaryTests
{
    [Test]
    public async Task DeniedPlatformOverviewReturnsSameUnavailableResultAsMissingResource()
    {
        var port = new MissingWorkflow();
        var denied = new AuthorizedOperatorService(new Authorization(false), port);
        var allowed = new AuthorizedOperatorService(new Authorization(true), port);
        var principal = new OperatorPrincipal("auditor", [OperatorAuthority.ReadOperations]);

        var deniedResult = await denied.GetOverviewAsync(principal, CancellationToken.None);
        var missingResult = await allowed.GetOverviewAsync(principal, CancellationToken.None);

        Assert.That(deniedResult, Is.EqualTo(missingResult));
    }

    private sealed class Authorization(bool value) : IOperatorAuthorization
    {
        public Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission,
            OperatorResource resource, CancellationToken cancellationToken) => Task.FromResult(value);
    }

    private sealed class MissingWorkflow : IOperatorWorkflowPort
    {
        public Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
            OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken) => Task.FromResult(new OperatorQueryResult<T>(OperatorResultStatus.Unavailable, default));
        public Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command,
            CancellationToken cancellationToken) => Task.FromResult(OperatorCommandResult.Unavailable());
    }
}
