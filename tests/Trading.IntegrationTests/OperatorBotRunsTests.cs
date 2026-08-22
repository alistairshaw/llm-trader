using Trading.Core.Identifiers;
using Trading.Engine.Operators;

namespace Trading.IntegrationTests;

[TestFixture]
[Category("OperatorBotRuns")]
public sealed class OperatorBotRunsTests
{
    private static readonly TradingBotId BotId = TradingBotId.Parse("01J5QH8M000000000000000821");

    [Test]
    public async Task AuthorizedManualActionCreatesExactlyOneDurableTriggerIntent()
    {
        var workflow = new RecordingWorkflow();
        var service = new AuthorizedOperatorService(new PermissionAuthorization(), workflow);
        var principal = new OperatorPrincipal("operator", [OperatorAuthority.TriggerRuns]);

        var result = await service.TriggerAsync(principal, BotId, "portfolio review", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
            Assert.That(result.Code, Is.EqualTo("bot_run.trigger_accepted"));
            Assert.That(workflow.Commands, Has.Count.EqualTo(1));
            Assert.That(workflow.Commands[0].Kind, Is.EqualTo(OperatorCommandKind.TriggerManualRun));
            Assert.That(workflow.Commands[0].Resource, Is.EqualTo(new OperatorResource(OperatorResourceKind.TradingBot, BotId.ToString())));
            Assert.That(workflow.Commands[0].Arguments["reason"], Is.EqualTo("portfolio review"));
        }
    }

    [Test]
    public async Task UnauthorizedManualActionCannotDiscloseOrCreateTrigger()
    {
        var workflow = new RecordingWorkflow();
        var service = new AuthorizedOperatorService(new PermissionAuthorization(), workflow);

        var result = await service.TriggerAsync(new OperatorPrincipal("reader", [OperatorAuthority.ReadOperations]),
            BotId, "portfolio review", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(OperatorCommandResult.Unavailable()));
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
            Commands.Add(command);
            return Task.FromResult(new OperatorCommandResult(OperatorResultStatus.Succeeded,
                "bot_run.trigger_accepted", "01J5QH8M000000000000000822"));
        }
    }
}
