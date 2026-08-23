using System.Collections.Immutable;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Core.Research;
using Trading.Engine.Operators;

namespace Trading.Host;

public sealed class ProductionOperatorAuthorization(TradingHostOptions options) : IOperatorAuthorization
{
    private static readonly OperatorResource[] ProfileResources =
    [
        OperatorResource.Platform,
        new(OperatorResourceKind.TradingBot, SmokeFixture.BotId.ToString()),
        new(OperatorResourceKind.TradingBot, SmokeFixture.BotTwoId.ToString()),
        new(OperatorResourceKind.Portfolio, SmokeFixture.PortfolioId.ToString()),
        new(OperatorResourceKind.Portfolio, SmokeFixture.PortfolioTwoId.ToString()),
        new(OperatorResourceKind.BrokerAccount, SmokeFixture.AccountId.ToString()),
        new(OperatorResourceKind.BrokerAccount, SmokeFixture.AccountTwoId.ToString()),
        new(OperatorResourceKind.TradeProposal, ProposalSmoke.ValidId.ToString()),
        new(OperatorResourceKind.TradeProposal, ProposalSmoke.UiReviewId.ToString()),
        new(OperatorResourceKind.ResearchReport, "01J5QH8M000000000000000701"),
        new(OperatorResourceKind.ResearchReport, "fixture-series"),
    ];

    public Task<bool> IsAuthorizedAsync(OperatorPrincipal principal, OperatorAuthority permission,
        OperatorResource resource, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var permitted = principal.Permissions.Contains(permission);
        var scoped = resource == OperatorResource.Platform || options.WpfTestProfile && ProfileResources.Contains(resource);
        return Task.FromResult(permitted && scoped);
    }

    public static OperatorPrincipal CreatePrincipal(TradingHostOptions options) => options.OperatorMode
        ? new("local-operator", Enum.GetValues<OperatorAuthority>())
        : new("headless-observer", [OperatorAuthority.ReadOperations]);
}

public sealed class ProductionOperatorWorkflowPort(TradingHostOptions options) : IOperatorWorkflowPort
{
    private readonly object gate = new();
    private bool platformSwitch;
    private long switchVersion;
    private ProposalStatus proposalStatus = ProposalStatus.AwaitingHumanApproval;

    public Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
        OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.WpfTestProfile)
            return Task.FromResult(new OperatorQueryResult<T>(OperatorResultStatus.Unavailable, default));
        var value = Query(typeof(T), page, resource, filter, pageRequest);
        return Task.FromResult(value is T typed
            ? new OperatorQueryResult<T>(OperatorResultStatus.Succeeded, typed)
            : new OperatorQueryResult<T>(OperatorResultStatus.Unavailable, default));
    }

    public Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.WpfTestProfile) return Task.FromResult(OperatorCommandResult.Unavailable());
        lock (gate)
        {
            if (command.Kind is OperatorCommandKind.ActivateKillSwitch or OperatorCommandKind.ClearKillSwitch)
            {
                if (command.ExpectedVersion != switchVersion)
                    return Task.FromResult(new OperatorCommandResult(OperatorResultStatus.Conflict, "operator.conflict"));
                platformSwitch = command.Kind == OperatorCommandKind.ActivateKillSwitch;
                switchVersion++;
            }
            if (command.Kind == OperatorCommandKind.ApproveProposal) proposalStatus = ProposalStatus.Approved;
            if (command.Kind == OperatorCommandKind.RejectProposal) proposalStatus = ProposalStatus.Rejected;
            return Task.FromResult(new OperatorCommandResult(OperatorResultStatus.Succeeded,
                $"operator.{command.Kind.ToString().ToLowerInvariant()}.succeeded", command.Resource.Id,
                command.Kind is OperatorCommandKind.ActivateKillSwitch or OperatorCommandKind.ClearKillSwitch
                    ? switchVersion : command.ExpectedVersion));
        }
    }

    private object? Query(Type type, OperatorPageKind page, OperatorResource resource, OperatorFilter filter,
        OperatorPageRequest request)
    {
        var now = new DateTimeOffset(2026, 8, 20, 23, 0, 0, TimeSpan.Zero);
        if (type == typeof(OperatorOverview))
            return new OperatorOverview(2, 0, 1, 1,
                [new("paper.recovery_active", "Paper recovery work is active.", OperatorWarningSeverity.Warning)]);
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(OperatorPage<>)) return null;
        var itemType = type.GenericTypeArguments[0];
        object[] items = itemType == typeof(BotSummary) ?
            [Bot(SmokeFixture.BotId, "research-only", ExecutionMode.ResearchOnly), Bot(SmokeFixture.BotTwoId, "human-approval", ExecutionMode.HumanApproval)] :
            itemType == typeof(BotDetail) ? [new BotDetail(Bot(resource.Id == SmokeFixture.BotTwoId.ToString() ? SmokeFixture.BotTwoId : SmokeFixture.BotId, "fixture", ExecutionMode.PaperTrading), ExecutionMode.PaperTrading, now.AddHours(4), [new("mode.paper", "Paper trading only.", OperatorWarningSeverity.Information)])] :
            itemType == typeof(PortfolioSummary) ? [new PortfolioSummary(SmokeFixture.PortfolioId, SmokeFixture.BotId, SmokeFixture.AccountId, "USD", 1000m, 100m, 1)] :
            itemType == typeof(RunSummary) ? [new RunSummary(BotRunId.Parse("01J5QH8M000000000000000601"), SmokeFixture.BotId, BotRunStatus.Completed, now.AddMinutes(-5), now, 2, 0.01m)] :
            itemType == typeof(RunDetail) ? [RunDetail(now)] :
            itemType == typeof(QueuedRunTriggerSummary) ? [] :
            itemType == typeof(ResearchSummary) ? [new ResearchSummary(ResearchReportId.Parse("01J5QH8M000000000000000701"), "fixture-series", 1, "ACME fixture research", ResearchReportStatus.Published, now.AddHours(-1), now.AddHours(-2), now.AddDays(1), ResearchVisibility.Shared, true)] :
            itemType == typeof(ResearchDetail) ? [ResearchDetail(now)] :
            itemType == typeof(ProposalSummary) ? [Proposal(now)] :
            itemType == typeof(ProposalDetail) ? [ProposalDetail(now)] :
            itemType == typeof(KillSwitchSummary) ? [Switch(now)] :
            itemType == typeof(KillSwitchDetail) ? [new KillSwitchDetail(Switch(now), platformSwitch ? Switch(now) : null,
                [new(platformSwitch, "deterministic fixture", "local-operator", platformSwitch ? "ACTIVATE Platform platform" : "CLEAR Platform platform", now, switchVersion)])] : [];
        var array = Array.CreateInstance(itemType, items.Length);
        for (var i = 0; i < items.Length; i++) array.SetValue(items[i], i);
        return Activator.CreateInstance(type, array, request.Offset, null);
    }

    private static BotSummary Bot(TradingBotId id, string name, ExecutionMode mode) => new(id, name,
        TradingBotStatus.Enabled, id == SmokeFixture.BotId ? SmokeFixture.PortfolioId : SmokeFixture.PortfolioTwoId,
        TradingBotConfigurationVersionId.Parse(id == SmokeFixture.BotId ? "01J5QH8M000000000000000102" : "01J5QH8M000000000000000202"), 1);

    private static RunDetail RunDetail(DateTimeOffset now) => new(
        new(BotRunId.Parse("01J5QH8M000000000000000601"), SmokeFixture.BotId, BotRunStatus.Completed,
            now.AddMinutes(-5), now, 2, 0.01m),
        TradingBotConfigurationVersionId.Parse("01J5QH8M000000000000000102"), SmokeFixture.SnapshotId,
        [new(BotRunTriggerId.Parse("01J5QH8M000000000000000602"), BotRunTriggerType.Manual,
            "operator", now.AddMinutes(-5), "manual-ui")], [new("tool-calls", "5", "2")],
        new(TimeSpan.FromMinutes(5), 100, new Money(0.01m, Currency.USD), 2, 1, 0),
        new(FinishStatus.Completed, "fixture run complete"), null, now.AddHours(4), null, false);

    private static ResearchDetail ResearchDetail(DateTimeOffset now)
    {
        var summary = new ResearchSummary(ResearchReportId.Parse("01J5QH8M000000000000000701"), "fixture-series",
            1, "ACME fixture research", ResearchReportStatus.Published, now.AddHours(-1), now.AddHours(-2),
            now.AddDays(1), ResearchVisibility.Shared, true);
        return new(summary, "What is the bounded ACME outlook?", new string('a', 64),
            "Deterministic fixture research content.", new("fixture", "scripted", "prompt-v1", "tools-v1", "1"),
            [new("approved-fixtures", "fixture://regulatory/acme", now.AddDays(-1), now.AddHours(-2), new string('b', 64))]);
    }

    private ProposalSummary Proposal(DateTimeOffset now) => new(ProposalSmoke.UiReviewId, SmokeFixture.BotTwoId,
        SmokeFixture.PortfolioTwoId, proposalStatus, now.AddDays(1), proposalStatus == ProposalStatus.AwaitingHumanApproval ? 0 : 1);

    private ProposalDetail ProposalDetail(DateTimeOffset now)
    {
        var proposal = Proposal(now);
        var decisions = proposalStatus == ProposalStatus.AwaitingHumanApproval
            ? ImmutableArray<OperatorProposalDecision>.Empty
            : [new(proposalStatus == ProposalStatus.Approved ? "Approved" : "Rejected", "local-operator", now,
                proposalStatus == ProposalStatus.Rejected ? "fixture rejection reason" : null, 0)];
        return new(proposal, "Fixture-backed rationale grounded in an exact report version.", new string('c', 64),
            new("01J5QH8M000000000000000202", SmokeFixture.SnapshotTwoId.ToString(), new string('d', 64)),
            [new("ResearchReport", "01J5QH8M000000000000000701", 1, new string('a', 64), "Shared")],
            [new("portfolio-capital", "Passed", "guardrail.passed", "risk-v1")], decisions,
            proposalStatus == ProposalStatus.Approved
                ? new("01J5QH8M000000000000000405", "Reserved", "100 USD", now.AddHours(1))
                : null, []);
    }

    private KillSwitchSummary Switch(DateTimeOffset now) => new(
        new OperatorResource(OperatorResourceKind.Portfolio, SmokeFixture.PortfolioId.ToString()), platformSwitch,
        platformSwitch ? "deterministic fixture" : "clear", "local-operator", now, switchVersion);
}
