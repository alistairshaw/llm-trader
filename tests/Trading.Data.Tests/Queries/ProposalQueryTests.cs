using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Proposals;

namespace Trading.Data.Tests.Queries;

[TestFixture, Category("ProposalProjections")]
public sealed class ProposalQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 23, 0, 0, TimeSpan.Zero);
    private const string Bot = "01EEEEEEEEEEEEEEEEEEEEEEEE";
    private const string Portfolio = "01PFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Account = "01ACEEEEEEEEEEEEEEEEEEEEEE";
    private const string Connection = "01CNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Configuration = "01CFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Snapshot = "01PSEEEEEEEEEEEEEEEEEEEEEE";
    private const string Run = "01BREEEEEEEEEEEEEEEEEEEEEE";
    private const string Instrument = "01MNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Request = "01RQEEEEEEEEEEEEEEEEEEEEEE";
    private const string ResearchRun = "01RNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Report = "01RPEEEEEEEEEEEEEEEEEEEEEE";
    private static readonly string[] ExpectedProposalOrder =
        ["01PPEEEEEEEEEEEEEEEEEEEEE3", "01PPEEEEEEEEEEEEEEEEEEEEE1", "01PPEEEEEEEEEEEEEEEEEEEEE2"];
    private static string Hash(char value) => new(value, 64);

    [Test]
    public async Task QueueFiltersOrdersPagesAndLeavesTrackerEmpty()
    {
        await using var database = await CreateAsync();
        await AddAwaitingAsync(database, "01PPEEEEEEEEEEEEEEEEEEEEE1", Now.AddMinutes(30), "first");
        await AddAwaitingAsync(database, "01PPEEEEEEEEEEEEEEEEEEEEE2", Now.AddMinutes(30), "second");
        await AddAwaitingAsync(database, "01PPEEEEEEEEEEEEEEEEEEEEE3", Now.AddMinutes(1), "expired");
        database.Context.ChangeTracker.Clear();
        var queries = new ProposalQueries(database.Context);

        var first = await queries.GetQueueAsync(Principal(), new(PortfolioId: PortfolioId.Parse(Portfolio), BrokerAccountId: BrokerAccountId.Parse(Account)),
            new(0, 1), Now.AddMinutes(2), default);
        var second = await queries.GetQueueAsync(Principal(), new(PortfolioId: PortfolioId.Parse(Portfolio)),
            new(1, 1), Now.AddMinutes(2), default);
        var withExpired = await queries.GetQueueAsync(Principal(), new(IncludeExpired: true), new(0, 10), Now.AddMinutes(2), default);

        Assert.Multiple(() =>
        {
            Assert.That(first.Single().Id.ToString(), Is.EqualTo("01PPEEEEEEEEEEEEEEEEEEEEE1"));
            Assert.That(second.Single().Id.ToString(), Is.EqualTo("01PPEEEEEEEEEEEEEEEEEEEEE2"));
            Assert.That(withExpired.Select(x => x.Id.ToString()), Is.EqualTo(ExpectedProposalOrder));
            Assert.That(database.Context.ChangeTracker.Entries(), Is.Empty);
            Assert.That(() => new ProposalPageRequest(0, 101), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public async Task DetailReconstructsExactActionEvidenceEvaluationDecisionAndReservation()
    {
        await using var database = await CreateAsync("BotPrivate");
        var proposal = Proposal("01PPEEEEEEEEEEEEEEEEEEEEED", Now.AddHours(1), "detail");
        var repository = new TradeProposalRepository(database.Context);
        await repository.RecordAsync(proposal, "detail", default);
        proposal.StartValidation(Now.AddMinutes(1));
        proposal.RecordEvaluation(GuardrailEvaluationId.Parse("01GVEEEEEEEEEEEEEEEEEEEEEE"),
            [new GuardrailPolicyReference(GuardrailPolicyLevel.Platform, "platform", "policy-v1")],
            GuardrailOutcome.Passed,
            [new GuardrailRuleResult("notional", GuardrailOutcome.Passed, "within limit", GuardrailPolicyLevel.Platform,
                "policy-v1", "250.00", "500.00", "guardrail.passed")], Now.AddMinutes(2),
            new(PortfolioDecisionSnapshotId.Parse(Snapshot), Now, Hash('s')), Hash('e'), "guardrail.passed");
        proposal.RequireHumanApproval(Now.AddMinutes(2));
        proposal.Approve(ProposalApprovalId.Parse("01APEEEEEEEEEEEEEEEEEEEEEE"),
            new DecisionActor(ApprovalActorType.User, "reviewer"), "reviewed", Now.AddMinutes(3),
            proposal.ContentVersion, new(PortfolioDecisionSnapshotId.Parse(Snapshot), Now, Hash('s')));
        await repository.SaveAsync(proposal, 1, default);
        var reservation = new CapitalReservation(CapitalReservationId.Parse("01CAEEEEEEEEEEEEEEEEEEEEEE"), proposal,
            new Money(250, Currency.USD), Now.AddMinutes(4), Now.AddMinutes(20));
        await new CapitalReservationRepository(database.Context).AddAsync(reservation, default);
        database.Context.ChangeTracker.Clear();

        var detail = await new ProposalQueries(database.Context).GetDetailAsync(Principal(), proposal.Id, Now.AddMinutes(5), default);

        Assert.Multiple(() =>
        {
            Assert.That(detail, Is.Not.Null);
            Assert.That(detail!.RequestedAction, Is.EqualTo(proposal.RequestedAction));
            Assert.That(detail.ContentVersion, Is.EqualTo(new ProposalContentVersion(1, Hash('p'))));
            Assert.That(detail.ReportEvidence.Single(), Is.EqualTo(new ReportEvidenceReference(ResearchReportId.Parse(Report), "series", 1, Hash('a'))));
            Assert.That(detail.Evaluations.Single().Policies.Single().Version, Is.EqualTo("policy-v1"));
            Assert.That(detail.Evaluations.Single().RuleResults.Single().ObservedValue, Is.EqualTo("250.00"));
            Assert.That(detail.Decisions.Single().Actor.Id, Is.EqualTo("reviewer"));
            Assert.That(detail.Reservation!.Amount, Is.EqualTo(new Money(250, Currency.USD)));
            Assert.That(detail.Reservation.IsExpired, Is.False);
            Assert.That(database.Context.ChangeTracker.Entries(), Is.Empty);
        });
    }

    [Test]
    public async Task EveryIdentityScopeAndEvidenceVisibilityMustAuthorizeBeforeDisclosure()
    {
        await using var database = await CreateAsync();
        var proposal = await AddAwaitingAsync(database, "01PPEEEEEEEEEEEEEEEEEEEEEA", Now.AddHours(1), "private");
        database.Context.ChangeTracker.Clear();
        var queries = new ProposalQueries(database.Context);

        var missingBot = new ProposalQueryPrincipal("actor", false, [], [PortfolioId.Parse(Portfolio)], [BrokerAccountId.Parse(Account)]);
        var missingPortfolio = new ProposalQueryPrincipal("actor", false, [TradingBotId.Parse(Bot)], [], [BrokerAccountId.Parse(Account)]);
        var missingAccount = new ProposalQueryPrincipal("actor", false, [TradingBotId.Parse(Bot)], [PortfolioId.Parse(Portfolio)], []);
        var otherBot = new ProposalQueryPrincipal("actor", false, [TradingBotId.Parse("01EEEEEEEEEEEEEEEEEEEEEEE2")],
            [PortfolioId.Parse(Portfolio)], [BrokerAccountId.Parse(Account)]);

        Assert.Multiple(async () =>
        {
            Assert.That(await queries.GetDetailAsync(missingBot, proposal.Id, Now, default), Is.Null);
            Assert.That(await queries.GetDetailAsync(missingPortfolio, proposal.Id, Now, default), Is.Null);
            Assert.That(await queries.GetDetailAsync(missingAccount, proposal.Id, Now, default), Is.Null);
            Assert.That(await queries.GetDetailAsync(otherBot, proposal.Id, Now, default), Is.Null);
            Assert.That(await queries.GetQueueAsync(otherBot, new(), new(0, 10), Now, default), Is.Empty);
            Assert.That(await queries.GetDetailAsync(Principal(), proposal.Id, Now, default), Is.Not.Null);
        });
    }

    [Test]
    public async Task QueuePlanUsesPortfolioStatusCreationIndex()
    {
        await using var database = await CreateAsync();
        await using var command = database.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN SELECT * FROM trade_proposals WHERE portfolio_id='{Portfolio}' AND status='AwaitingHumanApproval' ORDER BY created_at";
        await using var reader = await command.ExecuteReaderAsync();
        var plan = new List<string>(); while (await reader.ReadAsync()) plan.Add(reader.GetString(3));
        Assert.That(string.Join(Environment.NewLine, plan), Does.Contain("IX_trade_proposals_portfolio_id_status_created_at"));
    }

    private static ProposalQueryPrincipal Principal() => new("actor", false, [TradingBotId.Parse(Bot)],
        [PortfolioId.Parse(Portfolio)], [BrokerAccountId.Parse(Account)]);

    private static async Task<TradeProposal> AddAwaitingAsync(TemporarySqliteDatabase database, string id,
        DateTimeOffset validUntil, string key)
    {
        var proposal = Proposal(id, validUntil, key); var repository = new TradeProposalRepository(database.Context);
        await repository.RecordAsync(proposal, key, default); proposal.StartValidation(Now);
        proposal.RecordEvaluation(GuardrailEvaluationId.New(), "Initial", "policy-v1", GuardrailOutcome.Passed,
            [new GuardrailRuleResult("notional", GuardrailOutcome.Passed, $"within limit {key}")], Now,
            PortfolioDecisionSnapshotId.Parse(Snapshot)); proposal.RequireHumanApproval(Now);
        await repository.SaveAsync(proposal, 1, default); database.Context.ChangeTracker.Clear(); return proposal;
    }

    private static TradeProposal Proposal(string id, DateTimeOffset validUntil, string rationale) => new(
        TradeProposalId.Parse(id), TradingBotId.Parse(Bot), BotRunId.Parse(Run), PortfolioId.Parse(Portfolio),
        TradingBotConfigurationVersionId.Parse(Configuration), PortfolioDecisionSnapshotId.Parse(Snapshot),
        InstrumentId.Parse(Instrument), new DirectTradeAction(TradeSide.Buy, new Quantity(2, "shares"),
            ProposedOrderType.Limit, new Price(125, Currency.USD), ProposedTimeInForce.Day), rationale,
        new(1, Hash('p')), null, [new(ResearchReportId.Parse(Report), "series", 1, Hash('a'))], Now, validUntil);

    private static async Task<TemporarySqliteDatabase> CreateAsync(string reportVisibility = "Shared")
    {
        var database = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(database.Context).InitializeAsync();
        var c = database.Context; var at = Now.ToUnixTimeMilliseconds();
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO broker_connections VALUES ({Connection},'sim','Sim','Paper','ref://paper','Enabled','{{}}',{at},{at},1)");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO broker_accounts VALUES ({Account},{Connection},'paper','Paper','Margin','USD','Active',{at},'{{}}',{at},{at},1)");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bots (id,name,status,created_at,updated_at,version) VALUES ({Bot},'Bot','Enabled',{at},{at},1)");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bot_configuration_versions VALUES ({Configuration},{Bot},1,'{{}}','{{}}','{{}}','{{}}','{{}}','HumanApproval','{{}}','p',{Hash('c')},{at},{at},NULL)");
        await c.Database.ExecuteSqlInterpolatedAsync($"UPDATE trading_bots SET active_configuration_version_id={Configuration} WHERE id={Bot}");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO instruments VALUES ({Instrument},'Equity','AAPL','Apple','USD','NASDAQ',8,8,'Active',{at},{at},1)");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolios VALUES ({Portfolio},'P','USD',{Account},{Bot},'Active','1000','{{}}',{at},{at},1)");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolio_decision_snapshots VALUES ({Snapshot},{Portfolio},{Bot},{Configuration},{at},'Reconciled','{{}}',1,'{{}}',{Hash('s')},{at})");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO bot_runs VALUES ({Run},{Bot},{Configuration},{Snapshot},'Completed',NULL,NULL,{at},{at},'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,{Hash('r')})");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO research_requests VALUES ({Request},'Instrument','US:AAPL','q','key',{at},'Completed','Shared',{Bot},'{{}}','{{}}',{at},{at},NULL,{at},1)");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO research_runs VALUES ({ResearchRun},{Request},1,'Completed','{{}}','p','t','r',{at},{at},NULL,'{{}}',1)");
        await c.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO research_reports VALUES ({Report},'series',1,{Request},{ResearchRun},'Instrument','US:AAPL','q',{reportVisibility},{at},{at},{Now.AddDays(1).ToUnixTimeMilliseconds()},'Published',NULL,'v1','{{}}',NULL,{Hash('a')},'{{}}')");
        await c.Database.ExecuteSqlInterpolatedAsync($"UPDATE research_requests SET result_report_id={Report} WHERE id={Request}");
        c.ChangeTracker.Clear(); return database;
    }
}
