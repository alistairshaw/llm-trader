using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Proposals;
using Trading.Core.Research;

namespace Trading.Data.Tests.Repositories;

[TestFixture, Category("ProposalRepositories")]
internal sealed class ProposalGovernanceRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 21, 0, 0, TimeSpan.Zero);
    private const string Bot = "01EEEEEEEEEEEEEEEEEEEEEEEE";
    private const string Run = "01BREEEEEEEEEEEEEEEEEEEEEE";
    private const string Portfolio = "01PFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Snapshot = "01PSEEEEEEEEEEEEEEEEEEEEEE";
    private const string Configuration = "01CFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Instrument = "01MNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Request = "01RQEEEEEEEEEEEEEEEEEEEEEE";
    private const string ResearchRun = "01RNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Report = "01RPEEEEEEEEEEEEEEEEEEEEEE";
    private static string Hash(char value) => new(value, 64);

    [Test]
    public async Task HypothesisRoundTripsVersionsEvidenceFreezeAndConcurrency()
    {
        await using var database = await CreateAsync();
        var repository = new HypothesisRepository(database.Context);
        var hypothesis = new Hypothesis(HypothesisId.New(), "Quality compounds", Now);
        var version = hypothesis.AddVersion(HypothesisVersionId.New(), "Returns on capital persist",
            new UniverseDefinition(["Equity"], ["US"], [Currency.USD]), "filings", "rank roc", "walk-forward",
            "beats benchmark", "underperforms", [ResearchReportId.Parse(Report)], Now.AddMinutes(1));
        Assert.That(await repository.AddAsync(hypothesis, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        hypothesis.FreezeCurrent(Now.AddMinutes(2));
        Assert.That(await repository.SaveAsync(hypothesis, 1, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear();

        var loaded = await repository.GetAsync(hypothesis.Id, default);
        var exact = await repository.GetVersionAsync(version.Id, default);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Status, Is.EqualTo(HypothesisStatus.Frozen));
            Assert.That(loaded.Versions.Single().EvidenceReportIds.Single().ToString(), Is.EqualTo(Report));
            Assert.That(exact!.Claim, Is.EqualTo("Returns on capital persist"));
            Assert.That(exact.UniverseDefinition, Is.EqualTo(version.UniverseDefinition));
            Assert.That(exact.FrozenAt, Is.EqualTo(Now.AddMinutes(2)));
            Assert.That(database.Context.ChangeTracker.Entries(), Is.Empty);
        });
        Assert.That(await repository.SaveAsync(hypothesis, 1, default),
            Is.EqualTo(new PersistenceWriteResult.ConcurrencyConflict(1, 2)));
    }

    [Test]
    public async Task ProposalRecordIsIdempotentAndReconstructsExactEvidenceAndAction()
    {
        await using var database = await CreateAsync(); var repository = new TradeProposalRepository(database.Context);
        var proposal = Proposal();
        Assert.That(await repository.RecordAsync(proposal, "run:1:proposal:1", default), Is.TypeOf<ProposalRecordResult.Recorded>());
        Assert.That(await repository.RecordAsync(proposal, "run:1:proposal:1", default), Is.TypeOf<ProposalRecordResult.AlreadyRecorded>());
        Assert.That(await repository.RecordAsync(Proposal(), "run:1:proposal:1", default), Is.TypeOf<ProposalRecordResult.IdempotencyConflict>());
        database.Context.ChangeTracker.Clear(); var loaded = await repository.GetAsync(proposal.Id, default);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.ContentVersion, Is.EqualTo(proposal.ContentVersion));
            Assert.That(loaded.ReportEvidence.Single().ReportSeriesId, Is.EqualTo("series"));
            Assert.That(loaded.ReportEvidence.Single().VersionNumber, Is.EqualTo(1));
            Assert.That(loaded.ReportEvidence.Single().ContentHash, Is.EqualTo(Hash('a')));
            Assert.That(loaded.RequestedAction, Is.EqualTo(proposal.RequestedAction));
            Assert.That(database.Context.ChangeTracker.Entries(), Is.Empty);
        });
    }

    [Test]
    public async Task DecisionEvaluationAndReservationCommitAtomicallyAndInDeterministicOrder()
    {
        await using var database = await CreateAsync(); var proposals = new TradeProposalRepository(database.Context);
        var proposal = Proposal(); await proposals.RecordAsync(proposal, "atomic", default);
        proposal.StartValidation(Now.AddMinutes(1));
        proposal.RecordEvaluation(GuardrailEvaluationId.New(), "Initial", "policy-v1", GuardrailOutcome.Passed,
            [new GuardrailRuleResult("notional", GuardrailOutcome.Passed, "within limit")], Now.AddMinutes(2), PortfolioDecisionSnapshotId.Parse(Snapshot));
        proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.User, "operator-1", "reviewed",
            Now.AddMinutes(3), 1, PortfolioDecisionSnapshotId.Parse(Snapshot));
        var reservation = new CapitalReservation(CapitalReservationId.New(), proposal, new Money(500, Currency.USD),
            Now.AddMinutes(3), Now.AddMinutes(30));
        var transaction = new ProposalGovernanceTransactionRepository(database.Context);
        Assert.That(await transaction.SaveDecisionAndReservationAsync(proposal, 1, reservation, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear(); var loaded = await proposals.GetAsync(proposal.Id, default);
        var active = await new CapitalReservationRepository(database.Context).GetActiveAsync(proposal.Id, default);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Status, Is.EqualTo(ProposalStatus.Approved));
            Assert.That(loaded.GuardrailEvaluations.Single().Sequence, Is.EqualTo(1));
            Assert.That(loaded.GuardrailEvaluations.Single().RuleResults.Single().Rule, Is.EqualTo("notional"));
            Assert.That(loaded.ApprovalHistory.Single().ActorId, Is.EqualTo("operator-1"));
            Assert.That(active!.Amount, Is.EqualTo(new Money(500, Currency.USD)));
        });
    }

    [Test]
    public async Task ReservationUniquenessFailureRollsBackProposalDecision()
    {
        await using var database = await CreateAsync(); var proposals = new TradeProposalRepository(database.Context);
        var proposal = Proposal(); await proposals.RecordAsync(proposal, "rollback", default);
        proposal.StartValidation(Now.AddMinutes(1)); proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.User,
            "operator-1", null, Now.AddMinutes(2), 1, PortfolioDecisionSnapshotId.Parse(Snapshot));
        var first = new CapitalReservation(CapitalReservationId.New(), proposal, new Money(100, Currency.USD), Now.AddMinutes(2), Now.AddHours(1));
        await new CapitalReservationRepository(database.Context).AddAsync(first, default);
        var second = new CapitalReservation(CapitalReservationId.New(), proposal, new Money(200, Currency.USD), Now.AddMinutes(2), Now.AddHours(1));
        Assert.That(await new ProposalGovernanceTransactionRepository(database.Context).SaveDecisionAndReservationAsync(proposal, 1, second, default), Is.TypeOf<PersistenceWriteResult.UniquenessConflict>());
        database.Context.ChangeTracker.Clear();
        Assert.That((await proposals.GetAsync(proposal.Id, default))!.Status, Is.EqualTo(ProposalStatus.Recorded));
        Assert.That(await database.Context.ProposalApprovals.AsNoTracking().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task ReservationLifecycleQueriesAreOrderedVersionAwareAndUsePortfolioIndex()
    {
        await using var database = await CreateAsync(); var proposals = new TradeProposalRepository(database.Context);
        var proposal = Proposal(); await proposals.RecordAsync(proposal, "reservation", default);
        proposal.StartValidation(Now.AddMinutes(1)); proposal.Approve(ProposalApprovalId.New(), ApprovalActorType.User,
            "operator-1", null, Now.AddMinutes(2), 1, PortfolioDecisionSnapshotId.Parse(Snapshot));
        await proposals.SaveAsync(proposal, 1, default);
        var reservation = new CapitalReservation(CapitalReservationId.New(), proposal, new Money(50, Currency.USD), Now.AddMinutes(2), Now.AddMinutes(10));
        var repository = new CapitalReservationRepository(database.Context);
        Assert.That(await repository.AddAsync(reservation, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That((await repository.GetActiveForPortfolioAsync(PortfolioId.Parse(Portfolio), Now.AddMinutes(3), default)).Single().Id, Is.EqualTo(reservation.Id));
        Assert.That(await repository.ExpireAsync(PortfolioId.Parse(Portfolio), Now.AddMinutes(11), default), Is.EqualTo(1));
        database.Context.ChangeTracker.Clear(); var expired = await repository.GetAsync(reservation.Id, default);
        Assert.That(expired!.Status, Is.EqualTo(CapitalReservationStatus.Expired));
        Assert.That(await repository.SaveAsync(expired, 1, default), Is.EqualTo(new PersistenceWriteResult.ConcurrencyConflict(1, 2)));
        await using var command = database.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT * FROM capital_reservations WHERE portfolio_id=$portfolio AND status='Active' AND expires_at>$at ORDER BY expires_at,id";
        command.Parameters.Add(new SqliteParameter("$portfolio", Portfolio)); command.Parameters.Add(new SqliteParameter("$at", Now.ToUnixTimeMilliseconds()));
        await using var reader = await command.ExecuteReaderAsync(); var plan = new List<string>(); while (await reader.ReadAsync()) plan.Add(reader.GetString(3));
        Assert.That(string.Join(' ', plan), Does.Contain("IX_capital_reservations_portfolio_id_status_expires_at"));
    }

    private static TradeProposal Proposal() => new(TradeProposalId.New(), TradingBotId.Parse(Bot), BotRunId.Parse(Run),
        PortfolioId.Parse(Portfolio), TradingBotConfigurationVersionId.Parse(Configuration),
        PortfolioDecisionSnapshotId.Parse(Snapshot), InstrumentId.Parse(Instrument),
        new DirectTradeAction(TradeSide.Buy, new Quantity(2, "shares"), ProposedOrderType.Limit,
            new Price(125, Currency.USD), ProposedTimeInForce.Day), "durable cash flow",
        new ProposalContentVersion(1, Hash('p')), null,
        [new ReportEvidenceReference(ResearchReportId.Parse(Report), "series", 1, Hash('a'))], Now, Now.AddHours(2));

    private static async Task<TemporarySqliteDatabase> CreateAsync()
    {
        var database = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(database.Context).InitializeAsync();
        await SeedParentsAsync(database.Context); database.Context.ChangeTracker.Clear(); return database;
    }

    private static async Task SeedParentsAsync(TradingDbContext context)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bots (id,name,status,created_at,updated_at,version) VALUES ({Bot},'Bot','Enabled',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bot_configuration_versions VALUES ({Configuration},{Bot},1,'{{}}','{{}}','{{}}','{{}}','{{}}','PaperTrading','{{}}','p',{Hash('c')},{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},NULL)");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE trading_bots SET active_configuration_version_id={Configuration} WHERE id={Bot}");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO instruments VALUES ({Instrument},'Equity','AAPL','Apple','USD','NASDAQ',8,8,'Active',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolios VALUES ({Portfolio},'P','USD',NULL,{Bot},'Active','1000','{{}}',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolio_decision_snapshots VALUES ({Snapshot},{Portfolio},{Bot},{Configuration},{Now.ToUnixTimeMilliseconds()},'Reconciled','{{}}',1,'{{}}',{Hash('s')},{Now.ToUnixTimeMilliseconds()})");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO bot_runs VALUES ({Run},{Bot},{Configuration},{Snapshot},'Completed',NULL,NULL,{Now.ToUnixTimeMilliseconds()},{Now.AddMinutes(1).ToUnixTimeMilliseconds()},'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,{Hash('r')})");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO research_requests VALUES ({Request},'Instrument','US:AAPL','q','key',{Now.ToUnixTimeMilliseconds()},'Completed','Shared',{Bot},'{{}}','{{}}',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},NULL,{Now.ToUnixTimeMilliseconds()},1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO research_runs VALUES ({ResearchRun},{Request},1,'Completed','{{}}','p','t','r',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},NULL,'{{}}',1)");
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO research_reports VALUES ({Report},'series',1,{Request},{ResearchRun},'Instrument','US:AAPL','q','Shared',{Now.ToUnixTimeMilliseconds()},{Now.ToUnixTimeMilliseconds()},{Now.AddDays(1).ToUnixTimeMilliseconds()},'Published',NULL,'v1','{{}}',NULL,{Hash('a')},'{{}}')");
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE research_requests SET result_report_id={Report} WHERE id={Request}");
    }
}
