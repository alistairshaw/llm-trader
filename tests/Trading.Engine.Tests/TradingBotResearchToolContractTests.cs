using System.Text.Json;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;
using Trading.Core.Research;
using Trading.Engine.Runtime;
using Trading.Research;
using Trading.Research.Contracts;

namespace Trading.Engine.Tests;

[Category("ResearchTools")]
public sealed class TradingBotResearchToolContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 16, 0, 0, TimeSpan.Zero);

    [Test]
    public void RegistryAddsExactlyThreeVersionOneResearchToolsAlongsideStageThreeTools()
    {
        Assert.That(TradingBotResearchToolDispatcher.Definitions.Select(x => (x.Name, x.SchemaVersion)), Is.EqualTo(new[]
        {
            (StageThreeTools.GetPortfolioSnapshot, 1), (StageThreeTools.Finish, 1),
            (StageFourTradingTools.RequestResearch, 1), (StageFourTradingTools.ListReports, 1),
            (StageFourTradingTools.GetReport, 1),
        }));
    }

    [Test]
    public void EveryResearchToolPublishesAStrictCanonicalObjectSchema()
    {
        foreach (var definition in TradingBotResearchToolDispatcher.Definitions.Skip(2))
        {
            using var document = JsonDocument.Parse(definition.CanonicalSchema);
            Assert.Multiple(() =>
            {
                Assert.That(document.RootElement.GetProperty("type").GetString(), Is.EqualTo("object"));
                Assert.That(document.RootElement.GetProperty("additionalProperties").GetBoolean(), Is.False);
                Assert.That(document.RootElement.GetProperty("required").GetArrayLength(), Is.GreaterThan(0));
            });
        }
    }

    [Test]
    public void ResearchToolsExposeNoReportMutationOrTradingAuthority()
    {
        var names = TradingBotResearchToolDispatcher.Definitions.Select(x => x.Name).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Not.Contain("PublishReport"));
            Assert.That(names, Does.Not.Contain("UpdatePolicy"));
            Assert.That(names, Does.Not.Contain("SubmitOrder"));
            Assert.That(names, Does.Not.Contain("CallBroker"));
        });
    }

    [Test]
    public async Task RequestResearchUsesSharedAsynchronousServiceAndAuditsBudgetUsage()
    {
        var fixture = Fixture.Create();
        var result = await fixture.Dispatch(StageFourTradingTools.RequestResearch,
            "{\"asOf\":\"2026-08-20T16:00:00.000Z\",\"desiredSections\":[\"outlook\"],\"maximumAgeDays\":7,\"question\":\"assess durable cash flow\",\"requiredSourceTypes\":[\"approved-fixtures\"],\"subject\":\"US:ACME\",\"visibility\":\"Shared\"}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Result.Outcome, Is.EqualTo(ToolExecutionOutcome.Succeeded));
            Assert.That(result.Result.CanonicalResult, Does.Contain("research.request.queued"));
            Assert.That(fixture.Run.Usage.ResearchRequests, Is.EqualTo(1));
            Assert.That(fixture.Store.Calls, Is.EqualTo(1));
            Assert.That(fixture.Run.ToolInvocations.Single().Status, Is.EqualTo(ToolInvocationStatus.Completed));
        });
    }

    [Test]
    public async Task PinnedResearchBudgetAndSourcePolicyAreEnforced()
    {
        var noBudget = Fixture.Create(researchLimit: 0);
        var denied = await noBudget.Dispatch(StageFourTradingTools.RequestResearch,
            "{\"asOf\":\"2026-08-20T16:00:00.000Z\",\"desiredSections\":[\"outlook\"],\"maximumAgeDays\":7,\"question\":\"assess durable cash flow\",\"requiredSourceTypes\":[\"approved-fixtures\"],\"subject\":\"US:ACME\",\"visibility\":\"Shared\"}");
        var wrongSource = Fixture.Create();
        var rejected = await wrongSource.Dispatch(StageFourTradingTools.RequestResearch,
            "{\"asOf\":\"2026-08-20T16:00:00.000Z\",\"desiredSections\":[\"outlook\"],\"maximumAgeDays\":7,\"question\":\"assess durable cash flow\",\"requiredSourceTypes\":[\"public-web\"],\"subject\":\"US:ACME\",\"visibility\":\"Shared\"}");

        Assert.Multiple(() =>
        {
            Assert.That(denied.Authorization.Reason, Is.EqualTo("research_request_budget_exceeded"));
            Assert.That(noBudget.Store.Calls, Is.Zero);
            Assert.That(rejected.Authorization.Reason, Is.EqualTo("research_access_denied"));
            Assert.That(wrongSource.Store.Calls, Is.Zero);
        });
    }

    private sealed record Fixture(BotRun Run, DecisionStore Store, TradingBotResearchToolDispatcher Dispatcher)
    {
        public static Fixture Create(int researchLimit = 1)
        {
            var botId = TradingBotId.New();
            var configurationId = TradingBotConfigurationVersionId.New();
            var portfolioId = PortfolioId.New();
            var snapshot = new PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId.New(), portfolioId, botId, configurationId, Now,
                ReconciliationStatus.Reconciled, new Money(1, Currency.USD), new Money(2, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [],
                new DataFreshness(Now, Now, TimeSpan.FromMinutes(1)), Now);
            var bot = new TradingBot(botId, "bot", Now.AddDays(-1));
            bot.AddConfiguration(configurationId, new InvestmentMandate("research", TimeSpan.FromDays(30),
                    new UniverseDefinition(["Equity"], ["US"], [Currency.USD])), new RiskPolicy([]),
                new ToolPolicy([new ToolAllowance(StageFourTradingTools.RequestResearch, 2), new ToolAllowance(StageFourTradingTools.ListReports, 2),
                    new ToolAllowance(StageFourTradingTools.GetReport, 2), new ToolAllowance(StageThreeTools.Finish, 1)]),
                new RunBudget(TimeSpan.FromMinutes(1), 1000, new Money(1, Currency.USD), 7, researchLimit, 0),
                new SchedulingPolicy(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1), TimeSpan.FromDays(2)), ExecutionMode.ResearchOnly,
                new ModelConfiguration("scripted", "v1", 0, 1000), "v1", Now.AddDays(-1));
            var run = new BotRun(BotRunId.New(), botId, configurationId, snapshot.Id,
                new Usage(TimeSpan.Zero, 0, Money.Zero(Currency.USD), 0, 0, 0));
            run.BeginLeaseAcquisition(Now.AddMinutes(-1));
            run.LeaseAcquired("host", Now.AddMinutes(5));
            run.BeginReasoning();
            run.WaitForTool();
            var runs = new RunStore(run);
            var bots = new BotStore(bot);
            var input = new Input(snapshot);
            var clock = new Clock();
            var store = new DecisionStore();
            var service = new ResearchRequestService(store, new Identifiers(), clock);
            var stageThree = new StageThreeToolDispatcher(runs, bots, input, clock);
            return new(run, store, new(stageThree, runs, bots, service, new Catalog(), clock));
        }

        public Task<ToolDispatchResult> Dispatch(string name, string arguments) => Dispatcher.DispatchAsync(
            new(Run.Id, Run.TradingBotId, Run.PortfolioSnapshotId), new(ToolInvocationId.New(), name, 1, arguments), default);
    }

    private sealed class Clock : IUtcClock, IResearchClock
    {
        private int ticks;
        public DateTimeOffset UtcNow => Now.AddMilliseconds(ticks++);
    }

    private sealed class Identifiers : IResearchIdentifierSource
    {
        public ResearchRequestId NewRequestId() => ResearchRequestId.New();
        public ResearchRunAttemptId NewAttemptId() => ResearchRunAttemptId.New();
        public ResearchReportId NewReportId() => ResearchReportId.New();
        public ResearchSubscriptionId NewSubscriptionId() => ResearchSubscriptionId.New();
    }

    private sealed class DecisionStore : IResearchRequestDecisionRepository
    {
        public int Calls { get; private set; }
        public Task<ResearchRequestPersistenceDecision> DecideAsync(AuthorizedResearchRequest candidate, ResearchPrincipal principal,
            DateTimeOffset now, CancellationToken token)
        {
            Calls++;
            return Task.FromResult<ResearchRequestPersistenceDecision>(new ResearchRequestPersistenceDecision.Queued(
                candidate.Request.Id, candidate.SubscriptionId));
        }
    }

    private sealed class Catalog : IResearchReportCatalogQueries
    {
        public Task<IReadOnlyList<ResearchReportSummary>> SearchAsync(ResearchReportSearch search, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ResearchReportSummary>>([]);
        public Task<ResearchReport?> GetAuthorizedAsync(ResearchPrincipal principal, ResearchReportId id, CancellationToken token) => Task.FromResult<ResearchReport?>(null);
        public Task<ResearchReport?> GetAuthorizedVersionAsync(ResearchPrincipal principal, string seriesId, int version, CancellationToken token) => Task.FromResult<ResearchReport?>(null);
    }

    private sealed class RunStore(BotRun run) : IBotRunRepository
    {
        public Task<BotRun?> GetAsync(BotRunId id, CancellationToken token) => Task.FromResult<BotRun?>(id == run.Id ? run : null);
        public Task<PersistenceWriteResult> SaveAsync(BotRun value, long version, CancellationToken token) => Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded());
        public Task<BotRunLeaseResult> TryClaimAsync(BotRunClaim claim, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> RenewLeaseAsync(BotRunId id, string owner, DateTimeOffset expiry, long version, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<BotRunId>> GetExpiredLeaseRunIdsAsync(DateTimeOffset now, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class BotStore(TradingBot bot) : ITradingBotRepository
    {
        public Task<TradingBot?> GetAsync(TradingBotId id, CancellationToken token) => Task.FromResult<TradingBot?>(id == bot.Id ? bot : null);
        public Task<PersistenceWriteResult> AddAsync(TradingBot value, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> UpdateAsync(TradingBot value, long version, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class Input(PortfolioDecisionSnapshot snapshot) : IBotRunInputService
    {
        public Task<PinnedPortfolioSnapshot> GetPortfolioSnapshotAsync(BotRunId id, CancellationToken token) =>
            Task.FromResult(new PinnedPortfolioSnapshot(snapshot.CanonicalContent, snapshot.ContentHash, snapshot.SnapshotSchemaVersion, snapshot));
        public Task<DeterministicBotRunInput> PrepareAsync(BotRunId id, CancellationToken token) => throw new NotSupportedException();
    }
}
