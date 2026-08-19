using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Portfolios;

namespace Trading.Data.Tests.Queries;

[Category("PortfolioReadModels")]
public sealed class PortfolioReadModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 20, 0, 0, TimeSpan.Zero);
    private const string Portfolio1 = "01HF7YAT00S8K1M3Q5V7X9ZA01";
    private const string Portfolio2 = "01HF7YAT00S8K1M3Q5V7X9ZA02";
    private const string Account = "01HF7YAT00S8K1M3Q5V7X9ZA03";
    private const string Connection = "01HF7YAT00S8K1M3Q5V7X9ZA04";
    private const string Bot = "01HF7YAT00S8K1M3Q5V7X9ZA05";
    private const string Configuration = "01HF7YAT00S8K1M3Q5V7X9ZA06";
    private const string Instrument1 = "01HF7YAT00S8K1M3Q5V7X9ZA07";
    private const string Instrument2 = "01HF7YAT00S8K1M3Q5V7X9ZA08";
    private static readonly decimal[] ExpectedQuantities = [1.25000000m, 2.50000000m];
    private static readonly PortfolioId[] ExpectedPortfolioIds = [PortfolioId.Parse(Portfolio1)];
    private static readonly string[] ExpectedLedgerSources = ["source-2"];

    [Test]
    public async Task ProjectionsExactlyRepresentPersistedValuesAndLeaveTrackerEmpty()
    {
        await using var database = await CreateSeededAsync();
        var queries = new PortfolioQueries(database.Context);

        var portfolio = await queries.GetSummaryAsync(PortfolioId.Parse(Portfolio1), default);
        var positions = await queries.GetPositionsAsync(new(PortfolioId.Parse(Portfolio1)), new(0, 10), default);
        var ledger = await queries.GetLedgerAsync(new(PortfolioId.Parse(Portfolio1)), new(0, 10), default);
        var association = await queries.GetBrokerAccountAssociationAsync(PortfolioId.Parse(Portfolio1), default);
        var snapshots = await queries.GetDecisionSnapshotsAsync(new(PortfolioId.Parse(Portfolio1)), new(0, 10), default);

        Assert.Multiple(() =>
        {
            Assert.That(portfolio, Is.EqualTo(new PortfolioSummary(PortfolioId.Parse(Portfolio1), "Alpha", new("USD"), PortfolioStatus.Active,
                new(1234.56780000m, new("USD")), BrokerAccountId.Parse(Account), TradingBotId.Parse(Bot), Now.AddHours(-2), Now, 7)));
            Assert.That(positions.Select(x => x.Quantity), Is.EqualTo(ExpectedQuantities));
            Assert.That(positions[1].AverageCost.Amount, Is.EqualTo(101.12500000m));
            Assert.That(ledger[0].Description, Is.EqualTo("newer"));
            Assert.That(ledger[0].RecordedAt, Is.EqualTo(Now.AddMinutes(2)));
            Assert.That(association, Is.EqualTo(new BrokerAccountAssociationView(PortfolioId.Parse(Portfolio1), BrokerAccountId.Parse(Account),
                BrokerConnectionId.Parse(Connection), "paper-1", "Primary", BrokerAccountStatus.Restricted, Now.AddMinutes(-5), 3)));
            Assert.That(snapshots[0].ContentHash, Is.EqualTo(new string('a', 64)));
            Assert.That(snapshots[0].CreatedAt, Is.EqualTo(Now.AddMinutes(3)));
            Assert.That(database.Context.ChangeTracker.Entries(), Is.Empty);
        });
    }

    [Test]
    public async Task FiltersSelectPortfolioBrokerBotInstrumentAndInclusiveTimeRange()
    {
        await using var database = await CreateSeededAsync();
        var queries = new PortfolioQueries(database.Context);
        var page = new PageRequest(0, 100);

        var portfolios = await queries.GetPortfoliosAsync(new(BrokerAccountId.Parse(Account), TradingBotId.Parse(Bot)), page, default);
        var positions = await queries.GetPositionsAsync(new(PortfolioId.Parse(Portfolio1), InstrumentId.Parse(Instrument1), Now, Now), page, default);
        var ledger = await queries.GetLedgerAsync(new(PortfolioId.Parse(Portfolio1), BrokerAccountId.Parse(Account), TradingBotId.Parse(Bot),
            InstrumentId.Parse(Instrument1), Now.AddMinutes(1), Now.AddMinutes(2)), page, default);
        var snapshots = await queries.GetDecisionSnapshotsAsync(new(PortfolioId.Parse(Portfolio1), TradingBotId.Parse(Bot), Now, Now), page, default);

        Assert.Multiple(() =>
        {
            Assert.That(portfolios.Select(x => x.Id), Is.EqualTo(ExpectedPortfolioIds));
            Assert.That(positions, Has.Count.EqualTo(1));
            Assert.That(ledger.Select(x => x.SourceId), Is.EqualTo(ExpectedLedgerSources));
            Assert.That(snapshots, Has.Count.EqualTo(2));
            Assert.That(database.Context.ChangeTracker.Entries(), Is.Empty);
        });
    }

    [Test]
    public async Task EqualTimestampsUseIdentityAsDeterministicTieBreakerAndPaginationDoesNotOverlap()
    {
        await using var database = await CreateSeededAsync();
        var queries = new PortfolioQueries(database.Context);
        var filter = new PortfolioDecisionSnapshotQueryFilter(PortfolioId.Parse(Portfolio1));
        var first = await queries.GetDecisionSnapshotsAsync(filter, new(0, 1), default);
        var second = await queries.GetDecisionSnapshotsAsync(filter, new(1, 1), default);

        Assert.Multiple(() =>
        {
            Assert.That(first.Single().Id.ToString(), Is.EqualTo("01HF7YAT00S8K1M3Q5V7X9ZA15"));
            Assert.That(second.Single().Id.ToString(), Is.EqualTo("01HF7YAT00S8K1M3Q5V7X9ZA16"));
            Assert.That(first.Single().Id, Is.Not.EqualTo(second.Single().Id));
            Assert.That(() => new PageRequest(0, 101), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PageRequest(-1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public async Task PrimaryQueriesUseTheDefinedSqliteIndexes()
    {
        await using var database = await CreateSeededAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(await PlanAsync(database, $"SELECT * FROM portfolios WHERE id = '{Portfolio1}'"), Does.Contain("sqlite_autoindex_portfolios_1"));
            Assert.That(await PlanAsync(database, $"SELECT * FROM positions WHERE portfolio_id = '{Portfolio1}' AND instrument_id = '{Instrument1}'"), Does.Contain("IX_positions_portfolio_id_instrument_id"));
            Assert.That(await PlanAsync(database, $"SELECT * FROM portfolio_ledger_entries WHERE portfolio_id = '{Portfolio1}' ORDER BY effective_at DESC"), Does.Contain("IX_portfolio_ledger_entries_portfolio_id_effective_at"));
            Assert.That(await PlanAsync(database, $"SELECT * FROM portfolio_decision_snapshots WHERE portfolio_id = '{Portfolio1}' ORDER BY as_of DESC"), Does.Contain("IX_portfolio_decision_snapshots_portfolio_id_as_of"));
        });
    }

    private static async Task<string> PlanAsync(TemporarySqliteDatabase database, string sql)
    {
        await using var command = database.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql}";
        await using var reader = await command.ExecuteReaderAsync();
        var details = new List<string>();
        while (await reader.ReadAsync()) details.Add(reader.GetString(3));
        return string.Join(Environment.NewLine, details);
    }

    private static async Task<TemporarySqliteDatabase> CreateSeededAsync()
    {
        var database = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(database.Context).InitializeAsync();
        var now = Now.ToUnixTimeMilliseconds();
        var earlier = Now.AddHours(-2).ToUnixTimeMilliseconds();
        var reconciled = Now.AddMinutes(-5).ToUnixTimeMilliseconds();
        await using var command = database.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $$"""
            INSERT INTO broker_connections VALUES ('{{Connection}}','sim','Sim','Paper','ref://paper','Enabled','{}',{{earlier}},{{now}},2);
            INSERT INTO broker_accounts VALUES ('{{Account}}','{{Connection}}','paper-1','Primary','Margin','USD','Restricted',{{reconciled}},'{}',{{earlier}},{{now}},3);
            INSERT INTO instruments VALUES ('{{Instrument1}}','Equity','AAA','AAA Corp','USD','NYSE',4,8,'Active',{{earlier}},{{now}},1);
            INSERT INTO instruments VALUES ('{{Instrument2}}','Equity','BBB','BBB Corp','USD','NYSE',4,8,'Active',{{earlier}},{{now}},1);
            INSERT INTO trading_bots (id,name,status,active_configuration_version_id,requested_next_run_at,accepted_next_run_at,last_completed_run_id,created_at,updated_at,version)
            VALUES ('{{Bot}}','Owner','Enabled',NULL,NULL,NULL,NULL,{{earlier}},{{now}},4);
            INSERT INTO trading_bot_configuration_versions VALUES ('{{Configuration}}','{{Bot}}',1,'{}','{}','{}','{}','{}','PaperTrading','{}','v1','{{new string('b', 64)}}',{{earlier}},{{earlier}},NULL);
            UPDATE trading_bots SET active_configuration_version_id = '{{Configuration}}' WHERE id = '{{Bot}}';
            INSERT INTO portfolios VALUES ('{{Portfolio1}}','Alpha','USD','{{Account}}','{{Bot}}','Active','1234.5678','{}',{{earlier}},{{now}},7);
            INSERT INTO portfolios VALUES ('{{Portfolio2}}','Beta','USD',NULL,NULL,'Paused','50','{}',{{earlier}},{{earlier}},1);
            INSERT INTO positions VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA10','{{Portfolio1}}','{{Instrument2}}','share','1.25','100.5','USD','2.25','USD',{{earlier}},{{now}},NULL,5);
            INSERT INTO positions VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA11','{{Portfolio1}}','{{Instrument1}}','share','2.5','101.125','USD','3.5','USD',{{earlier}},{{now}},NULL,6);
            INSERT INTO portfolio_ledger_entries VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA12','{{Portfolio1}}','Deposit','10.25','USD',NULL,NULL,{{now}},{{now}},'BrokerEvent','source-1',NULL,'older',NULL);
            INSERT INTO portfolio_ledger_entries VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA13','{{Portfolio1}}','Settlement','20.5','USD','{{Instrument1}}','2.5',{{Now.AddMinutes(2).ToUnixTimeMilliseconds()}},{{Now.AddMinutes(2).ToUnixTimeMilliseconds()}},'BrokerExecution','source-2',NULL,'newer',NULL);
            INSERT INTO portfolio_decision_snapshots VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA15','{{Portfolio1}}','{{Bot}}','{{Configuration}}',{{now}},'Reconciled','{}',1,'{}','{{new string('a', 64)}}',{{Now.AddMinutes(3).ToUnixTimeMilliseconds()}});
            INSERT INTO portfolio_decision_snapshots VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA16','{{Portfolio1}}','{{Bot}}','{{Configuration}}',{{now}},'Pending','{}',1,'{}','{{new string('c', 64)}}',{{Now.AddMinutes(4).ToUnixTimeMilliseconds()}});
            """;
        await command.ExecuteNonQueryAsync();
        database.Context.ChangeTracker.Clear();
        return database;
    }
}
