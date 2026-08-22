using Trading.Core.Bots;
using Trading.Core.Identifiers;
using Trading.Engine.Operators;

namespace Trading.IntegrationTests;

[TestFixture]
[Category("OperatorBotManagement")]
public sealed class OperatorBotManagementTests
{
    private static readonly TradingBotId BotId = TradingBotId.Parse("01J5QH8M000000000000000711");
    private static readonly PortfolioId PortfolioId = PortfolioId.Parse("01J5QH8M000000000000000712");

    [Test]
    public async Task AuthorizedBotCommandsCarryExactVersionConfigurationPortfolioAndLifecycleIntent()
    {
        var workflow = new RecordingWorkflow();
        var service = new AuthorizedOperatorService(new PermissionAuthorization(), workflow);
        var principal = new OperatorPrincipal("operator", [OperatorAuthority.ManageBots]);
        var configuration = new BotConfigurationInput("income", "risk-v2", "tools-v3", "schedule-v4",
            ExecutionMode.PaperTrading, "model-v5", "prompt-v6");

        _ = await service.CreateAsync(principal, "Income", CancellationToken.None);
        _ = await service.ConfigureAsync(principal, BotId, 4, configuration, CancellationToken.None);
        _ = await service.AssignAsync(principal, BotId, PortfolioId, 5, CancellationToken.None);
        _ = await service.PauseAsync(principal, BotId, 6, CancellationToken.None);
        _ = await service.ResumeAsync(principal, BotId, 7, CancellationToken.None);
        _ = await service.RetireAsync(principal, BotId, 8, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workflow.Commands.Select(x => x.Kind), Is.EqualTo(new[]
            {
                OperatorCommandKind.CreateBot, OperatorCommandKind.ConfigureBot, OperatorCommandKind.AssignPortfolio,
                OperatorCommandKind.PauseBot, OperatorCommandKind.ResumeBot, OperatorCommandKind.RetireBot,
            }));
            Assert.That(workflow.Commands[1].ExpectedVersion, Is.EqualTo(4));
            Assert.That(workflow.Commands[1].Arguments["executionMode"], Is.EqualTo("PaperTrading"));
            Assert.That(workflow.Commands[1].Arguments["promptVersion"], Is.EqualTo("prompt-v6"));
            Assert.That(workflow.Commands[2].Arguments["portfolioId"], Is.EqualTo(PortfolioId.ToString()));
            Assert.That(workflow.Commands[5].ExpectedVersion, Is.EqualTo(8));
        }
    }

    [Test]
    public async Task UnauthorizedBotMutationIsNonDisclosingAndNeverReachesWorkflow()
    {
        var workflow = new RecordingWorkflow();
        var service = new AuthorizedOperatorService(new PermissionAuthorization(), workflow);
        var principal = new OperatorPrincipal("reader", [OperatorAuthority.ReadOperations]);

        var result = await service.RetireAsync(principal, BotId, 2, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(OperatorResultStatus.Unavailable));
            Assert.That(result.Code, Is.EqualTo("operator.unavailable"));
            Assert.That(workflow.Commands, Is.Empty);
        }
    }

    private sealed class PermissionAuthorization : IOperatorAuthorization
    {
        public Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission,
            OperatorResource resource, CancellationToken cancellationToken) =>
            Task.FromResult(principal.Permissions.Contains(permission));
    }

    private sealed class RecordingWorkflow : IOperatorWorkflowPort
    {
        public List<OperatorCommand> Commands { get; } = [];
        public Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
            OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(new OperatorCommandResult(OperatorResultStatus.Succeeded, "operator.succeeded",
                command.Resource.Id, (command.ExpectedVersion ?? -1) + 1));
        }
    }
}
