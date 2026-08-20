using System.Text.Json;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Core.Proposals;
using Trading.Core.Research;
using Trading.Engine.Runtime;
using Trading.Research;
using Trading.Research.Contracts;

namespace Trading.Engine.Tests;

[Category("ProposalToolDispatch")]
public sealed class ProposalToolDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public void RegistryAddsExactlyTwoClosedVersionOneProposalToolsAndNoExecutionTools()
    {
        Assert.That(ProposalToolDispatcher.Definitions.TakeLast(2).Select(x => (x.Name, x.SchemaVersion)), Is.EqualTo(new[]
        { (StageFiveTradingTools.ProposeTrade, 1), (StageFiveTradingTools.ProposeTargetAllocation, 1) }));
        foreach (var definition in ProposalToolDispatcher.Definitions.TakeLast(2))
        {
            using var schema = JsonDocument.Parse(definition.CanonicalSchema);
            Assert.That(schema.RootElement.GetProperty("additionalProperties").GetBoolean(), Is.False);
        }
        Assert.That(ProposalToolDispatcher.Definitions.Select(x => x.Name), Has.None.Matches<string>(x =>
            x.Contains("Order", StringComparison.Ordinal) || x.Contains("Broker", StringComparison.Ordinal) ||
            x.Contains("Approve", StringComparison.Ordinal) || x.Contains("Reserve", StringComparison.Ordinal)));
    }

    [TestCase("{\"extra\":1}", ProposalToolCodes.UnknownProperty)]
    [TestCase("{}", ProposalToolCodes.MissingRequiredProperty)]
    public async Task ClosedSchemaFailuresAreDurablyAudited(string arguments, string code)
    {
        var fixture = Fixture.Create(); var result = await fixture.Dispatch(StageFiveTradingTools.ProposeTrade, arguments);
        Assert.Multiple(() => { Assert.That(result.Authorization.Reason, Is.EqualTo(code)); Assert.That(fixture.Proposals.Values, Is.Empty); Assert.That(fixture.Run.ToolInvocations.Single().Error, Is.EqualTo(code)); });
    }

    [Test]
    public async Task ValidDirectTradeRecordsOnlyAnImmutableProposalAndExactPinnedContext()
    {
        var fixture = Fixture.Create(); var proposalId = TradeProposalId.New();
        var result = await fixture.Dispatch(StageFiveTradingTools.ProposeTrade, fixture.TradeJson(proposalId));
        var recorded = fixture.Proposals.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Authorization.Reason, Is.EqualTo(ProposalToolCodes.Recorded));
            Assert.That(recorded.Id, Is.EqualTo(proposalId)); Assert.That(recorded.TradingBotId, Is.EqualTo(fixture.Run.TradingBotId));
            Assert.That(recorded.BotRunId, Is.EqualTo(fixture.Run.Id)); Assert.That(recorded.PortfolioSnapshotId, Is.EqualTo(fixture.Run.PortfolioSnapshotId));
            Assert.That(recorded.ExecutionMode, Is.EqualTo(ExecutionMode.ResearchOnly));
            Assert.That(recorded.Status, Is.EqualTo(ProposalStatus.Recorded)); Assert.That(recorded.ApprovalHistory, Is.Empty);
            Assert.That(fixture.Run.Usage.Proposals, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TargetAllocationRecordsExactDecimalWithoutConstructingTrades()
    {
        var fixture = Fixture.Create(); var result = await fixture.Dispatch(StageFiveTradingTools.ProposeTargetAllocation, fixture.AllocationJson(25m));
        Assert.Multiple(() => { Assert.That(result.Result.Outcome, Is.EqualTo(ToolExecutionOutcome.Succeeded)); Assert.That(((TargetAllocationAction)fixture.Proposals.Values.Single().RequestedAction).TargetPercentage.Value, Is.EqualTo(25m)); Assert.That(fixture.Proposals.Values.Single().ExecutionMode, Is.EqualTo(ExecutionMode.ResearchOnly)); });
    }

    [TestCase("0", ProposalToolCodes.InvalidQuantity)]
    [TestCase("01", ProposalToolCodes.InvalidDecimal)]
    public async Task QuantityMustBePositiveAndCanonical(string quantity, string code)
    {
        var fixture = Fixture.Create(); var result = await fixture.Dispatch(StageFiveTradingTools.ProposeTrade, fixture.TradeJson(TradeProposalId.New(), quantity));
        Assert.That(result.Authorization.Reason, Is.EqualTo(code)); Assert.That(fixture.Proposals.Values, Is.Empty);
    }

    [Test]
    public async Task AllocationAndPortfolioAuthorityAreEnforced()
    {
        var allocation = Fixture.Create(); var invalid = await allocation.Dispatch(StageFiveTradingTools.ProposeTargetAllocation, allocation.AllocationJson(110m));
        var ownership = Fixture.Create(); var json = ownership.TradeJson(TradeProposalId.New()).Replace(ownership.Snapshot.PortfolioId.ToString(), PortfolioId.New().ToString(), StringComparison.Ordinal);
        var wrong = await ownership.Dispatch(StageFiveTradingTools.ProposeTrade, json);
        Assert.Multiple(() => { Assert.That(invalid.Authorization.Reason, Is.EqualTo(ProposalToolCodes.InvalidAllocationTotal)); Assert.That(wrong.Authorization.Reason, Is.EqualTo(ProposalToolCodes.PortfolioNotAssigned)); });
    }

    [Test]
    public async Task ProposalPolicyBudgetCancellationAndExpirationAreEnforced()
    {
        var noBudget = Fixture.Create(proposalLimit: 0); var budget = await noBudget.Dispatch(StageFiveTradingTools.ProposeTrade, noBudget.TradeJson(TradeProposalId.New()));
        var expired = Fixture.Create(); var expiration = await expired.Dispatch(StageFiveTradingTools.ProposeTrade, expired.TradeJson(TradeProposalId.New()).Replace("2026-08-20T19:00:00.000Z", "2026-08-20T17:00:00.000Z", StringComparison.Ordinal));
        var cancelled = Fixture.Create(); using var source = new CancellationTokenSource(); source.Cancel(); var cancellation = await cancelled.Dispatch(StageFiveTradingTools.ProposeTrade, cancelled.TradeJson(TradeProposalId.New()), source.Token);
        Assert.Multiple(() => { Assert.That(budget.Authorization.Reason, Is.EqualTo(ProposalToolCodes.ProposalBudgetExceeded)); Assert.That(expiration.Authorization.Reason, Is.EqualTo(ProposalToolCodes.InvalidExpiration)); Assert.That(cancellation.Authorization.Reason, Is.EqualTo(ToolDispatchCodes.Cancelled)); });
    }

    private sealed record Fixture(BotRun Run, PortfolioDecisionSnapshot Snapshot, ProposalStore Proposals, ProposalToolDispatcher Dispatcher)
    {
        public static Fixture Create(int proposalLimit = 2)
        {
            var botId = TradingBotId.New(); var configId = TradingBotConfigurationVersionId.New(); var portfolioId = PortfolioId.New();
            var snapshot = new PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId.New(), portfolioId, botId, configId, Now, ReconciliationStatus.Reconciled,
                new Money(10000, Currency.USD), new Money(10000, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [], new DataFreshness(Now, Now, TimeSpan.FromMinutes(1)), Now);
            var bot = new TradingBot(botId, "bot", Now.AddDays(-1)); bot.AddConfiguration(configId, new InvestmentMandate("proposal", TimeSpan.FromDays(30), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])), new RiskPolicy([]),
                new ToolPolicy([new(StageFiveTradingTools.ProposeTrade, 2), new(StageFiveTradingTools.ProposeTargetAllocation, 2)]), new RunBudget(TimeSpan.FromMinutes(1), 1000, new Money(1, Currency.USD), 4, 0, proposalLimit),
                new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1), TimeSpan.FromDays(2)), ExecutionMode.ResearchOnly, new ModelConfiguration("scripted", "v1", 0, 1000), "v1", Now.AddDays(-1));
            var run = new BotRun(BotRunId.New(), botId, configId, snapshot.Id, new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0)); run.BeginLeaseAcquisition(Now.AddMinutes(-1)); run.LeaseAcquired("host", Now.AddMinutes(5)); run.BeginReasoning(); run.WaitForTool();
            var runs = new RunStore(run); var bots = new BotStore(bot); var clock = new Clock(); var stage3 = new StageThreeToolDispatcher(runs, bots, new Input(snapshot), clock);
            var research = new TradingBotResearchToolDispatcher(stage3, runs, bots, new ResearchRequestService(new Decisions(), new ResearchIds(), clock), new Catalog(), clock);
            var proposals = new ProposalStore(); return new(run, snapshot, proposals, new(research, runs, bots, new SnapshotStore(snapshot), proposals, new Hypotheses(), new Catalog(), clock));
        }
        public string TradeJson(TradeProposalId id, string quantity = "10") => $"{{\"evidenceReports\":[],\"hypothesisVersionId\":null,\"instrumentId\":\"{InstrumentId.New()}\",\"limitCurrency\":\"USD\",\"limitPrice\":\"20\",\"orderType\":\"Limit\",\"portfolioId\":\"{Snapshot.PortfolioId}\",\"portfolioSnapshotId\":\"{Snapshot.Id}\",\"proposalId\":\"{id}\",\"quantity\":\"{quantity}\",\"quantityUnit\":\"shares\",\"rationale\":\"bounded rationale\",\"side\":\"Buy\",\"timeInForce\":\"Day\",\"validUntil\":\"2026-08-20T19:00:00.000Z\"}}";
        public string AllocationJson(decimal target) => $"{{\"evidenceReports\":[],\"hypothesisVersionId\":null,\"instrumentId\":\"{InstrumentId.New()}\",\"portfolioId\":\"{Snapshot.PortfolioId}\",\"portfolioSnapshotId\":\"{Snapshot.Id}\",\"proposalId\":\"{TradeProposalId.New()}\",\"rationale\":\"bounded rationale\",\"targetPercentage\":\"{target:0.############################}\",\"validUntil\":\"2026-08-20T19:00:00.000Z\"}}";
        public Task<ToolDispatchResult> Dispatch(string name, string json, CancellationToken token = default) => Dispatcher.DispatchAsync(new(Run.Id, Run.TradingBotId, Run.PortfolioSnapshotId), new(ToolInvocationId.New(), name, 1, json), token);
    }
    private sealed class Clock : IUtcClock, IResearchClock { private int tick; public DateTimeOffset UtcNow => Now.AddMilliseconds(tick++); }
    private sealed class RunStore(BotRun value) : IBotRunRepository { public Task<BotRun?> GetAsync(BotRunId id, CancellationToken t) => Task.FromResult<BotRun?>(id == value.Id ? value : null); public Task<PersistenceWriteResult> SaveAsync(BotRun r, long v, CancellationToken t) => Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); public Task<BotRunLeaseResult> TryClaimAsync(BotRunClaim c, CancellationToken t) => throw new NotSupportedException(); public Task<bool> RenewLeaseAsync(BotRunId i, string o, DateTimeOffset e, long v, CancellationToken t) => throw new NotSupportedException(); public Task<IReadOnlyList<BotRunId>> GetExpiredLeaseRunIdsAsync(DateTimeOffset n, CancellationToken t) => throw new NotSupportedException(); }
    private sealed class BotStore(TradingBot value) : ITradingBotRepository { public Task<TradingBot?> GetAsync(TradingBotId id, CancellationToken t) => Task.FromResult<TradingBot?>(id == value.Id ? value : null); public Task<PersistenceWriteResult> AddAsync(TradingBot v, CancellationToken t) => throw new NotSupportedException(); public Task<PersistenceWriteResult> UpdateAsync(TradingBot v, long e, CancellationToken t) => throw new NotSupportedException(); }
    private sealed class SnapshotStore(PortfolioDecisionSnapshot value) : IPortfolioDecisionSnapshotRepository { public Task<PortfolioDecisionSnapshot?> GetAsync(PortfolioDecisionSnapshotId id, CancellationToken t) => Task.FromResult<PortfolioDecisionSnapshot?>(id == value.Id ? value : null); public Task<PersistenceWriteResult> PublishAsync(PortfolioDecisionSnapshot s, CancellationToken t) => throw new NotSupportedException(); }
    private sealed class ProposalStore : ITradeProposalRepository { public List<TradeProposal> Values { get; } = []; public Task<TradeProposal?> GetAsync(TradeProposalId id, CancellationToken t) => Task.FromResult(Values.SingleOrDefault(x => x.Id == id)); public Task<ProposalRecordResult> RecordAsync(TradeProposal p, string k, CancellationToken t) { Values.Add(p); return Task.FromResult<ProposalRecordResult>(new ProposalRecordResult.Recorded(p)); } public Task<PersistenceWriteResult> SaveAsync(TradeProposal p, long v, CancellationToken t) => throw new NotSupportedException(); }
    private sealed class Hypotheses : IHypothesisRepository { public Task<Hypothesis?> GetAsync(HypothesisId id, CancellationToken t) => Task.FromResult<Hypothesis?>(null); public Task<HypothesisVersion?> GetVersionAsync(HypothesisVersionId id, CancellationToken t) => Task.FromResult<HypothesisVersion?>(null); public Task<PersistenceWriteResult> AddAsync(Hypothesis h, CancellationToken t) => throw new NotSupportedException(); public Task<PersistenceWriteResult> SaveAsync(Hypothesis h, long v, CancellationToken t) => throw new NotSupportedException(); }
    private sealed class Catalog : IResearchReportCatalogQueries { public Task<IReadOnlyList<ResearchReportSummary>> SearchAsync(ResearchReportSearch s, CancellationToken t) => Task.FromResult<IReadOnlyList<ResearchReportSummary>>([]); public Task<ResearchReport?> GetAuthorizedAsync(ResearchPrincipal p, ResearchReportId id, CancellationToken t) => Task.FromResult<ResearchReport?>(null); public Task<ResearchReport?> GetAuthorizedVersionAsync(ResearchPrincipal p, string s, int v, CancellationToken t) => Task.FromResult<ResearchReport?>(null); }
    private sealed class Input(PortfolioDecisionSnapshot value) : IBotRunInputService { public Task<PinnedPortfolioSnapshot> GetPortfolioSnapshotAsync(BotRunId id, CancellationToken t) => Task.FromResult(new PinnedPortfolioSnapshot(value.CanonicalContent, value.ContentHash, 1, value)); public Task<DeterministicBotRunInput> PrepareAsync(BotRunId id, CancellationToken t) => throw new NotSupportedException(); }
    private sealed class Decisions : IResearchRequestDecisionRepository { public Task<ResearchRequestPersistenceDecision> DecideAsync(AuthorizedResearchRequest c, ResearchPrincipal p, DateTimeOffset n, CancellationToken t) => throw new NotSupportedException(); }
    private sealed class ResearchIds : IResearchIdentifierSource { public ResearchRequestId NewRequestId() => ResearchRequestId.New(); public ResearchRunAttemptId NewAttemptId() => ResearchRunAttemptId.New(); public ResearchReportId NewReportId() => ResearchReportId.New(); public ResearchSubscriptionId NewSubscriptionId() => ResearchSubscriptionId.New(); }
}
