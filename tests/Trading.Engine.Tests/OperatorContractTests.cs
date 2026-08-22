using Trading.Core.Identifiers;
using Trading.Engine.Operators;

namespace Trading.Engine.Tests;

[TestFixture, Category("OperatorContracts")]
public sealed class OperatorContractTests
{
    private static readonly OperatorPrincipal Principal = new("operator-1", Enum.GetValues<OperatorAuthority>());
    private static readonly string[] ExpectedItems = ["one"];

    [Test]
    public void PrincipalAndPagesFreezeCallerOwnedCollectionsAndEnforceBounds()
    {
        var permissions = new List<OperatorAuthority> { OperatorAuthority.ManageBots, OperatorAuthority.ReadOperations };
        var principal = new OperatorPrincipal(" operator ", permissions);
        var items = new List<string> { "one" };
        var page = new OperatorPage<string>(items, 0, null);
        permissions.Clear();
        items.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(principal.ActorId, Is.EqualTo("operator"));
            Assert.That(principal.Permissions, Has.Count.EqualTo(2));
            Assert.That(page.Items, Is.EqualTo(ExpectedItems));
            Assert.That(() => new OperatorPageRequest(-1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new OperatorPageRequest(0, 201), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public async Task UnauthorizedReadIsUnavailableWithoutConsultingWorkflow()
    {
        var workflow = new RecordingWorkflow();
        var service = new AuthorizedOperatorService(new FixedAuthorization(false), workflow);
        var result = await service.GetPageAsync<BotSummary>(Principal, OperatorPageKind.Bots,
            new(OperatorResourceKind.TradingBot, "secret"), new(), new(0, 20), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new OperatorQueryResult<OperatorPage<BotSummary>>(OperatorResultStatus.Unavailable, default)));
            Assert.That(workflow.Calls, Is.Zero);
        });
    }

    [Test]
    public async Task UnauthorizedMutationIsNonDisclosingAndDoesNotReachWorkflow()
    {
        var workflow = new RecordingWorkflow();
        var service = new AuthorizedOperatorService(new FixedAuthorization(false), workflow);
        var result = await service.PauseAsync(Principal, TradingBotId.Parse("01EEEEEEEEEEEEEEEEEEEEEEE1"), 3,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(OperatorCommandResult.Unavailable()));
            Assert.That(workflow.Calls, Is.Zero);
        });
    }

    [Test]
    public async Task AuthorizedCommandsPreserveActorIntentVersionAndCancellation()
    {
        var workflow = new RecordingWorkflow();
        var service = new AuthorizedOperatorService(new FixedAuthorization(true), workflow);
        var id = TradingBotId.Parse("01EEEEEEEEEEEEEEEEEEEEEEE1");
        var result = await service.AssignAsync(Principal, id,
            PortfolioId.Parse("01FFFFFFFFFFFFFFFFFFFFFFF1"), 7, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OperatorResultStatus.Succeeded));
            Assert.That(workflow.Command!.Kind, Is.EqualTo(OperatorCommandKind.AssignPortfolio));
            Assert.That(workflow.Command.ExpectedVersion, Is.EqualTo(7));
            Assert.That(workflow.Principal, Is.SameAs(Principal));
        });
    }

    [Test]
    public void PreCancelledOperationDoesNotAuthorizeOrExecute()
    {
        var authorization = new FixedAuthorization(true);
        var workflow = new RecordingWorkflow();
        var service = new AuthorizedOperatorService(authorization, workflow);
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.That(async () => await service.GetOverviewAsync(Principal, source.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(authorization.Calls, Is.Zero);
            Assert.That(workflow.Calls, Is.Zero);
        });
    }

    private sealed class FixedAuthorization(bool allowed) : IOperatorAuthorization
    {
        public int Calls { get; private set; }
        public Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission,
            OperatorResource resource, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(allowed);
        }
    }

    private sealed class RecordingWorkflow : IOperatorWorkflowPort
    {
        public int Calls { get; private set; }
        public OperatorCommand? Command { get; private set; }
        public OperatorPrincipal? Principal { get; private set; }
        public Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
            OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new OperatorQueryResult<T>(OperatorResultStatus.Unavailable, default));
        }
        public Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command,
            CancellationToken cancellationToken)
        {
            Calls++;
            Principal = principal;
            Command = command;
            return Task.FromResult(new OperatorCommandResult(OperatorResultStatus.Succeeded, "operator.succeeded"));
        }
    }
}
