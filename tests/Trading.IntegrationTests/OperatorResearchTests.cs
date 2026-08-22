using Trading.Core.Identifiers;
using Trading.Engine.Operators;

namespace Trading.IntegrationTests;

[TestFixture, Category("OperatorResearch")]
public sealed class OperatorResearchTests
{
    [Test]
    public async Task MissingAndUnauthorizedExactReportQueriesAreIndistinguishable()
    {
        var resource = new OperatorResource(OperatorResourceKind.ResearchReport, "01HF7YAT00S8K1M3Q5V7X9ZA01");
        var port = new CapturingWorkflow();
        var denied = new AuthorizedOperatorService(new Authorization(false), port);
        var allowed = new AuthorizedOperatorService(new Authorization(true), port);
        var principal = new OperatorPrincipal("auditor", [OperatorAuthority.ReadOperations]);

        var deniedResult = await denied.GetPageAsync<ResearchDetail>(principal, OperatorPageKind.Research, resource,
            new(Status: "exact:series-a:1"), new(0, 1), CancellationToken.None);
        var missingResult = await allowed.GetPageAsync<ResearchDetail>(principal, OperatorPageKind.Research, resource,
            new(Status: "exact:series-a:1"), new(0, 1), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deniedResult, Is.EqualTo(missingResult));
            Assert.That(deniedResult.Status, Is.EqualTo(OperatorResultStatus.Unavailable));
            Assert.That(port.QueryCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task ResearchRequestIsAuthorizedForPinnedBotAndCanonicalSubject()
    {
        var port = new CapturingWorkflow { CommandResult = new(OperatorResultStatus.Succeeded, "operator.research.requested") };
        var service = new AuthorizedOperatorService(new Authorization(true), port);
        var principal = new OperatorPrincipal("researcher", [OperatorAuthority.RequestResearch]);
        var botId = TradingBotId.Parse("01HF7YAT00S8K1M3Q5V7X9ZA02");

        var result = await service.RequestAsync(principal, botId, "  Assess durable cash flow  ", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
            Assert.That(port.LastCommand!.Kind, Is.EqualTo(OperatorCommandKind.RequestResearch));
            Assert.That(port.LastCommand.Resource, Is.EqualTo(new OperatorResource(OperatorResourceKind.TradingBot, botId.ToString())));
            Assert.That(port.LastCommand.Arguments["subject"], Is.EqualTo("Assess durable cash flow"));
        }
    }

    private sealed class Authorization(bool allowed) : IOperatorAuthorization
    {
        public Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission,
            OperatorResource resource, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class CapturingWorkflow : IOperatorWorkflowPort
    {
        public int QueryCount { get; private set; }
        public OperatorCommand? LastCommand { get; private set; }
        public OperatorCommandResult CommandResult { get; set; } = OperatorCommandResult.Unavailable();
        public Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
            OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken)
        {
            QueryCount++;
            return Task.FromResult(new OperatorQueryResult<T>(OperatorResultStatus.Unavailable, default));
        }
        public Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command,
            CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Task.FromResult(CommandResult);
        }
    }
}
