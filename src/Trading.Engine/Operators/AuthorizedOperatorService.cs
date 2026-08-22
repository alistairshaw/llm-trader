using Trading.Core.Identifiers;

namespace Trading.Engine.Operators;

public sealed class AuthorizedOperatorService(IOperatorAuthorization authorization, IOperatorWorkflowPort workflows) :
    IOperatorQueries, IBotOperatorService, IRunOperatorService, IResearchOperatorService, IProposalOperatorService
    , IKillSwitchOperatorService
{
    public async Task<OperatorQueryResult<OperatorOverview>> GetOverviewAsync(OperatorPrincipal principal,
        CancellationToken cancellationToken) => await QueryAsync<OperatorOverview>(principal, OperatorPageKind.Overview,
        OperatorResource.Platform, new(), new(0, 1), cancellationToken).ConfigureAwait(false);

    public async Task<OperatorQueryResult<OperatorPage<T>>> GetPageAsync<T>(OperatorPrincipal principal,
        OperatorPageKind page, OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest,
        CancellationToken cancellationToken) => await QueryAsync<OperatorPage<T>>(principal, page, resource, filter,
        pageRequest, cancellationToken).ConfigureAwait(false);

    public Task<OperatorCommandResult> CreateAsync(OperatorPrincipal principal, string name, CancellationToken cancellationToken) =>
        ExecuteAsync(principal, OperatorAuthority.ManageBots, OperatorCommand.Create(OperatorCommandKind.CreateBot,
            OperatorResource.Platform, null, [new("name", Required(name, nameof(name)))]), cancellationToken);

    public Task<OperatorCommandResult> ConfigureAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion,
        BotConfigurationInput configuration, CancellationToken cancellationToken) => ExecuteAsync(principal,
        OperatorAuthority.ManageBots, OperatorCommand.Create(OperatorCommandKind.ConfigureBot, Bot(id), expectedVersion,
        [new("mandate", configuration.Mandate), new("riskPolicyVersion", configuration.RiskPolicyVersion),
         new("toolPolicyVersion", configuration.ToolPolicyVersion), new("schedulingPolicyVersion", configuration.SchedulingPolicyVersion),
         new("executionMode", configuration.ExecutionMode.ToString()), new("model", configuration.Model),
         new("promptVersion", configuration.PromptVersion)]), cancellationToken);

    public Task<OperatorCommandResult> AssignAsync(OperatorPrincipal principal, TradingBotId id, PortfolioId portfolioId,
        long expectedVersion, CancellationToken cancellationToken) => ExecuteAsync(principal, OperatorAuthority.ManageBots,
        OperatorCommand.Create(OperatorCommandKind.AssignPortfolio, Bot(id), expectedVersion,
            [new("portfolioId", portfolioId.ToString())]), cancellationToken);

    public Task<OperatorCommandResult> PauseAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion,
        CancellationToken cancellationToken) => BotLifecycle(principal, id, expectedVersion, OperatorCommandKind.PauseBot, cancellationToken);
    public Task<OperatorCommandResult> ResumeAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion,
        CancellationToken cancellationToken) => BotLifecycle(principal, id, expectedVersion, OperatorCommandKind.ResumeBot, cancellationToken);
    public Task<OperatorCommandResult> RetireAsync(OperatorPrincipal principal, TradingBotId id, long expectedVersion,
        CancellationToken cancellationToken) => BotLifecycle(principal, id, expectedVersion, OperatorCommandKind.RetireBot, cancellationToken);

    public Task<OperatorCommandResult> TriggerAsync(OperatorPrincipal principal, TradingBotId id, string reason,
        CancellationToken cancellationToken) => ExecuteAsync(principal, OperatorAuthority.TriggerRuns,
        OperatorCommand.Create(OperatorCommandKind.TriggerManualRun, Bot(id), null,
            [new("reason", Required(reason, nameof(reason)))]), cancellationToken);

    public Task<OperatorCommandResult> RequestAsync(OperatorPrincipal principal, TradingBotId id, string subject,
        CancellationToken cancellationToken) => ExecuteAsync(principal, OperatorAuthority.RequestResearch,
        OperatorCommand.Create(OperatorCommandKind.RequestResearch, Bot(id), null,
            [new("subject", Required(subject, nameof(subject)))]), cancellationToken);

    public Task<OperatorCommandResult> ApproveAsync(OperatorPrincipal principal, TradeProposalId id, long expectedVersion,
        string? reason, CancellationToken cancellationToken) => ProposalDecision(principal, id, expectedVersion,
        OperatorCommandKind.ApproveProposal, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), cancellationToken);
    public Task<OperatorCommandResult> RejectAsync(OperatorPrincipal principal, TradeProposalId id, long expectedVersion,
        string reason, CancellationToken cancellationToken) => ProposalDecision(principal, id, expectedVersion,
        OperatorCommandKind.RejectProposal, Required(reason, nameof(reason)), cancellationToken);

    public Task<OperatorCommandResult> ActivateAsync(OperatorPrincipal principal, OperatorResource scope,
        long expectedVersion, string reason, CancellationToken cancellationToken) => Switch(principal, scope,
        expectedVersion, OperatorCommandKind.ActivateKillSwitch, reason, cancellationToken);

    public Task<OperatorCommandResult> ClearAsync(OperatorPrincipal principal, OperatorResource scope,
        long expectedVersion, string reason, CancellationToken cancellationToken) => Switch(principal, scope,
        expectedVersion, OperatorCommandKind.ClearKillSwitch, reason, cancellationToken);

    private async Task<OperatorQueryResult<T>> QueryAsync<T>(OperatorPrincipal principal, OperatorPageKind page,
        OperatorResource resource, OperatorFilter filter, OperatorPageRequest pageRequest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await authorization.IsAuthorizedAsync(principal, OperatorAuthority.ReadOperations, resource,
                cancellationToken).ConfigureAwait(false))
            return new OperatorQueryResult<T>(OperatorResultStatus.Unavailable, default);
        return await workflows.QueryAsync<T>(principal, page, resource, filter, pageRequest, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OperatorCommandResult> ExecuteAsync(OperatorPrincipal principal, OperatorAuthority permission,
        OperatorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await authorization.IsAuthorizedAsync(principal, permission, command.Resource, cancellationToken)
                .ConfigureAwait(false))
            return OperatorCommandResult.Unavailable();
        return await workflows.ExecuteAsync(principal, command, cancellationToken).ConfigureAwait(false);
    }

    private Task<OperatorCommandResult> BotLifecycle(OperatorPrincipal principal, TradingBotId id, long expectedVersion,
        OperatorCommandKind kind, CancellationToken cancellationToken) => ExecuteAsync(principal,
        OperatorAuthority.ManageBots, OperatorCommand.Create(kind, Bot(id), expectedVersion), cancellationToken);

    private Task<OperatorCommandResult> ProposalDecision(OperatorPrincipal principal, TradeProposalId id,
        long expectedVersion, OperatorCommandKind kind, string? reason, CancellationToken cancellationToken) =>
        ExecuteAsync(principal, OperatorAuthority.DecideProposals,
            OperatorCommand.Create(kind, new(OperatorResourceKind.TradeProposal, id.ToString()), expectedVersion,
                reason is null ? null : [new("reason", reason)]), cancellationToken);

    private Task<OperatorCommandResult> Switch(OperatorPrincipal principal, OperatorResource scope,
        long expectedVersion, OperatorCommandKind kind, string reason, CancellationToken cancellationToken) =>
        ExecuteAsync(principal, OperatorAuthority.ManageKillSwitches,
            OperatorCommand.Create(kind, scope, expectedVersion, [new("reason", Required(reason, nameof(reason)))]),
            cancellationToken);

    private static OperatorResource Bot(TradingBotId id) =>
        new(OperatorResourceKind.TradingBot, (id ?? throw new ArgumentNullException(nameof(id))).ToString());

    private static string Required(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}
