using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;
using Trading.Engine.Runtime;

namespace Trading.Host;

internal static partial class ProposalSmoke
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 23, 0, 0, TimeSpan.Zero);
    private static readonly TradeProposalId ValidId = TradeProposalId.Parse("01J5QH8M000000000000000401");
    private static readonly TradeProposalId CompetingId = TradeProposalId.Parse("01J5QH8M000000000000000402");
    private static readonly TradeProposalId InvalidId = TradeProposalId.Parse("01J5QH8M000000000000000403");
    private static readonly TradeProposalId ResearchOnlyId = TradeProposalId.Parse("01J5QH8M000000000000000404");

    public static async Task RunAsync(IServiceProvider services, IReadOnlyList<BotRunExecutionResult> runs,
        ILogger logger, CancellationToken token)
    {
        var repository = services.GetRequiredService<ITradeProposalRepository>();
        var state = services.GetRequiredService<ProposalSmokeState>();
        var humanRun = await RequiredRunAsync(services, runs, SmokeFixture.BotTwoId, token);
        var researchRun = await RequiredRunAsync(services, runs, SmokeFixture.BotId, token);
        await state.SeedFreshSnapshotsAsync(services, token);

        await RecordAsync(repository, Proposal(ValidId, humanRun, SmokeFixture.PortfolioTwoId,
            SmokeFixture.SnapshotTwoId, ExecutionMode.HumanApproval,
            new DirectTradeAction(TradeSide.Buy, new Quantity(70, "shares"), ProposedOrderType.Limit,
                new Price(10, Currency.USD), ProposedTimeInForce.Day), 'a', "valid direct trade"), token);
        await RecordAsync(repository, Proposal(CompetingId, humanRun, SmokeFixture.PortfolioTwoId,
            SmokeFixture.SnapshotTwoId, ExecutionMode.HumanApproval,
            new TargetAllocationAction(new Percentage(50)), 'b', "competing target allocation"), token);
        await RecordAsync(repository, Proposal(InvalidId, humanRun, SmokeFixture.PortfolioTwoId,
            SmokeFixture.SnapshotTwoId, ExecutionMode.HumanApproval,
            new DirectTradeAction(TradeSide.Buy, new Quantity(200, "shares"), ProposedOrderType.Limit,
                new Price(10, Currency.USD), ProposedTimeInForce.Day), 'c', "invalid oversized trade"), token);
        await RecordAsync(repository, Proposal(ResearchOnlyId, researchRun, SmokeFixture.PortfolioId,
            SmokeFixture.SnapshotId, ExecutionMode.ResearchOnly,
            new DirectTradeAction(TradeSide.Buy, new Quantity(1, "shares"), ProposedOrderType.Limit,
                new Price(10, Currency.USD), ProposedTimeInForce.Day), 'd', "research-only proposal"), token);

        var orchestrator = services.GetRequiredService<IProposalGovernanceOrchestrator>();
        var valid = await orchestrator.ValidateAsync(ValidId, token);
        var invalid = await orchestrator.ValidateAsync(InvalidId, token);
        var researchOnly = await orchestrator.ValidateAsync(ResearchOnlyId, token);
        var reserved = await orchestrator.DecideAndReserveAsync(Approve(valid), TimeSpan.FromMinutes(20), token);
        var competing = await orchestrator.ValidateAsync(CompetingId, token);
        var denied = await orchestrator.DecideAndReserveAsync(Approve(competing), TimeSpan.FromMinutes(20), token);

        var queries = services.GetRequiredService<IProposalQueries>();
        var detail = await queries.GetDetailAsync(new("smoke-operator", true), ValidId, Now, token)
            ?? throw new InvalidOperationException("The reserved proposal projection was not available.");
        var queue = await queries.GetQueueAsync(new("smoke-operator", true),
            new(Status: null, IncludeExpired: true), new(0, 20), Now, token);
        var active = await services.GetRequiredService<ICapitalReservationRepository>()
            .GetActiveForPortfolioAsync(SmokeFixture.PortfolioTwoId, Now, token);

        if (valid.Outcome != ProposalOrchestrationOutcome.AwaitingHumanApproval ||
            invalid.Outcome != ProposalOrchestrationOutcome.Rejected ||
            researchOnly.Outcome != ProposalOrchestrationOutcome.ResearchOnly ||
            reserved.Outcome != ProposalOrchestrationOutcome.Reserved ||
            denied is not
            {
                Outcome: ProposalOrchestrationOutcome.Rejected,
                Code: ProposalGovernanceCodes.InsufficientCapital
            } ||
            detail.Evaluations.Count != 2 || detail.Decisions.Count != 1 || active.Count != 1 ||
            queue.Count != 4)
            throw new InvalidOperationException("The Stage 5 proposal-governance smoke produced an unexpected durable outcome.");

        Result(logger, ValidId.ToString(), detail.ContentVersion.ContentHash, detail.Evaluations[0].ContentHash,
            detail.Evaluations[1].ContentHash, detail.Evaluations[1].RuleResults.Count,
            reserved.Reservation!.Id.ToString(), reserved.Reservation.Amount.ToString(), denied.Code,
            InvalidId.ToString(), invalid.Code, ResearchOnlyId.ToString(), researchOnly.Code, queue.Count,
            active.Sum(x => x.Amount.Amount), brokerSubmissions: 0, recoverable: true);
    }

    private static HumanProposalDecisionCommand Approve(ProposalOrchestrationResult validated)
    {
        var proposal = validated.Proposal ?? throw new InvalidOperationException("Validated proposal is missing.");
        var evaluation = validated.Evaluation ?? throw new InvalidOperationException("Validated evaluation is missing.");
        return new(proposal.Id, proposal.ContentVersion, proposal.ConfigurationVersionId, evaluation.FreshState!,
            evaluation.Id, evaluation.ContentHash!, ApprovalDecision.Approved, "fixture review",
            new(ApprovalActorType.User, "smoke-operator"), new HashSet<string> { "proposal.approve" });
    }

    private static TradeProposal Proposal(TradeProposalId id, BotRun run, PortfolioId portfolioId,
        PortfolioDecisionSnapshotId snapshotId, ExecutionMode mode, RequestedAction action, char hash, string rationale) =>
        new(id, run.TradingBotId, run.Id, portfolioId, run.ConfigurationVersionId, snapshotId,
            SmokeFixture.InstrumentId, action, rationale, new(1, new string(hash, 64)), null, [], Now,
            Now.AddHours(1), mode);

    private static async Task RecordAsync(ITradeProposalRepository repository, TradeProposal proposal,
        CancellationToken token) => _ = await repository.RecordAsync(proposal, $"smoke:{proposal.Id}", token);

    private static async Task<BotRun> RequiredRunAsync(IServiceProvider services,
        IEnumerable<BotRunExecutionResult> results, TradingBotId botId, CancellationToken token)
    {
        foreach (var id in results.Where(x => x.RunId is not null).Select(x => x.RunId!))
        {
            var run = await services.GetRequiredService<IBotRunRepository>().GetAsync(id, token);
            if (run?.TradingBotId == botId) return run;
        }
        throw new InvalidOperationException($"The smoke Bot Run for '{botId}' was not completed.");
    }

    [LoggerMessage(20, LogLevel.Information,
        "Stage5 Proposal={Proposal} ProposalHash={ProposalHash} InitialEvaluationHash={InitialEvaluationHash} FreshEvaluationHash={FreshEvaluationHash} RuleResults={RuleResults} Reservation={Reservation} Reserved={Reserved} CompetingOutcome={CompetingOutcome} InvalidProposal={InvalidProposal} InvalidOutcome={InvalidOutcome} ResearchOnlyProposal={ResearchOnlyProposal} ResearchOnlyOutcome={ResearchOnlyOutcome} ProjectionCount={ProjectionCount} ActiveReservedTotal={ActiveReservedTotal} BrokerSubmissions={BrokerSubmissions} Recoverable={Recoverable}")]
    private static partial void Result(ILogger logger, string proposal, string proposalHash,
        string initialEvaluationHash, string freshEvaluationHash, int ruleResults, string reservation,
        string reserved, string competingOutcome, string invalidProposal, string invalidOutcome,
        string researchOnlyProposal, string researchOnlyOutcome, int projectionCount,
        decimal activeReservedTotal, int brokerSubmissions, bool recoverable);
}

internal sealed class ProposalSmokeState : IProposalGovernanceClock, IProposalGovernanceIdentifierSource,
    IFreshProposalStateProvider, IProposalGovernanceContextProvider
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 23, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<TradeProposalId, int> acquisitions = [];
    private readonly Dictionary<TradingBotId, (PortfolioDecisionSnapshot Initial, PortfolioDecisionSnapshot Fresh)> snapshots = [];
    private int evaluationIds;
    private int approvalIds;
    private int reservationIds;

    public DateTimeOffset UtcNow => Now;
    public TradeProposalId NewProposalId() => TradeProposalId.Parse("01J5QH8M000000000000000499");
    public GuardrailEvaluationId NewEvaluationId() => GuardrailEvaluationId.Parse($"01J5QH8M0000000000000005{++evaluationIds:00}");
    public ProposalApprovalId NewApprovalId() => ProposalApprovalId.Parse($"01J5QH8M0000000000000006{++approvalIds:00}");
    public CapitalReservationId NewReservationId() => CapitalReservationId.Parse($"01J5QH8M0000000000000007{++reservationIds:00}");

    public async Task SeedFreshSnapshotsAsync(IServiceProvider services, CancellationToken token)
    {
        await SeedAsync(services, SmokeFixture.BotId, SmokeFixture.PortfolioId, SmokeFixture.SnapshotId,
            PortfolioDecisionSnapshotId.Parse("01J5QH8M000000000000000405"), token);
        await SeedAsync(services, SmokeFixture.BotTwoId, SmokeFixture.PortfolioTwoId, SmokeFixture.SnapshotTwoId,
            PortfolioDecisionSnapshotId.Parse("01J5QH8M000000000000000406"), token);
    }

    public Task<FreshProposalState> AcquireAsync(TradeProposal proposal, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var pair = snapshots[proposal.TradingBotId];
        var count = acquisitions.TryGetValue(proposal.Id, out var prior) ? prior : 0;
        acquisitions[proposal.Id] = count + 1;
        var snapshot = count == 0 ? pair.Initial : pair.Fresh;
        return Task.FromResult(new FreshProposalState(new(snapshot.Id, snapshot.AsOf, snapshot.ContentHash),
            snapshot.BuyingPower, snapshot.ReservedCapital, snapshot.DataFreshness.SourceAsOf));
    }

    public Task<ProposalGovernanceEvaluationContext> GetAsync(TradeProposal proposal, FreshProposalState freshState,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        GuardrailPolicy Policy(GuardrailPolicyLevel level, decimal limit) => new(
            new(level, $"smoke-{level.ToString().ToLowerInvariant()}", "v1"), true,
            [SmokeFixture.InstrumentId], new Money(limit, Currency.USD), new Percentage(90),
            Money.Zero(Currency.USD), TimeSpan.FromMinutes(10), new Money(10000, Currency.USD), true);
        var definitions = new HierarchicalGuardrailPolicySet(
            Policy(GuardrailPolicyLevel.Platform, 1500), Policy(GuardrailPolicyLevel.Account, 1200),
            Policy(GuardrailPolicyLevel.Portfolio, 1000), Policy(GuardrailPolicyLevel.TradingBot, 900));
        var refs = definitions.InEvaluationOrder.Select(x => x.Reference).ToArray();
        var notional = proposal.RequestedAction switch
        {
            DirectTradeAction { LimitPrice: not null } direct => direct.LimitPrice * direct.Quantity,
            TargetAllocationAction target => new Money(freshState.AvailableCapital.Amount * target.TargetPercentage.Value / 100m, Currency.USD),
            _ => Money.Zero(Currency.USD),
        };
        var state = new GuardrailState(Now, true, true, notional, notional,
            new Percentage(Math.Min(100, notional.Amount / 10)), freshState.AvailableCapital,
            Now.AddMinutes(-1), new Money(100000, Currency.USD), true);
        return Task.FromResult(new ProposalGovernanceEvaluationContext(
            new(refs[0], refs[1], refs[2], refs[3]), definitions, state));
    }

    private async Task SeedAsync(IServiceProvider services, TradingBotId botId, PortfolioId portfolioId,
        PortfolioDecisionSnapshotId initialId, PortfolioDecisionSnapshotId freshId, CancellationToken token)
    {
        var repository = services.GetRequiredService<IPortfolioDecisionSnapshotRepository>();
        var initial = await repository.GetAsync(initialId, token)
            ?? throw new InvalidOperationException("The initial smoke snapshot was not persisted.");
        var fresh = new PortfolioDecisionSnapshot(freshId, portfolioId, botId, initial.ConfigurationVersionId,
            Now, ReconciliationStatus.Reconciled, new Money(1000, Currency.USD), new Money(1000, Currency.USD),
            Money.Zero(Currency.USD), [], [], 0, [], new DataFreshness(Now, Now, TimeSpan.FromMinutes(5)), Now);
        _ = await repository.PublishAsync(fresh, token);
        snapshots[botId] = (initial, fresh);
    }
}

internal sealed class SmokeProposalDecisionAuthorizer : IProposalDecisionAuthorizer
{
    public Task<ProposalDecisionAuthorizationResult> AuthorizeAsync(ProposalDecisionAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var allowed = request.Actor == new DecisionActor(ApprovalActorType.User, "smoke-operator") &&
            request.Roles.Contains("proposal.approve");
        return Task.FromResult(new ProposalDecisionAuthorizationResult(allowed,
            allowed ? "proposal_decision.authorized" : ProposalGovernanceCodes.UnauthorizedActor));
    }
}
