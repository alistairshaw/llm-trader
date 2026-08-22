using Microsoft.EntityFrameworkCore;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Data.Tests.Queries;

[Category("OperatorPortfolioBroker")]
public sealed class OperatorPortfolioBrokerQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);
    private const string Bot = "01HF7YAT00S8K1M3Q5V7X9ZA01";
    private const string OtherBot = "01HF7YAT00S8K1M3Q5V7X9ZA02";
    private const string Account = "01HF7YAT00S8K1M3Q5V7X9ZA03";
    private const string OtherAccount = "01HF7YAT00S8K1M3Q5V7X9ZA04";
    private const string Connection = "01HF7YAT00S8K1M3Q5V7X9ZA05";
    private const string Portfolio = "01HF7YAT00S8K1M3Q5V7X9ZA06";
    private static readonly string[] ExpectedCapabilities = ["Cancel", "Submit"];

    [Test]
    public async Task QueryPreservesExactFactsAndPaperSafetyMetadata()
    {
        await using var database = await SeedAsync();
        var rows = await new OperatorPortfolioBrokerQueries(database.Context).GetAuthorizedAsync(
            new(TradingBotId.Parse(Bot), BrokerAccountId.Parse(Account)), new(0, 10), default);
        var row = rows.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.CapitalAllocation, Is.EqualTo(1234.56780000m));
            Assert.That(row.PositionQuantity, Is.EqualTo(3.75000000m));
            Assert.That(row.LedgerTotal, Is.EqualTo(30.37500000m));
            Assert.That(row.Environment, Is.EqualTo("Paper"));
            Assert.That(row.Capabilities, Is.EqualTo(ExpectedCapabilities));
            Assert.That(row.MappingCount, Is.EqualTo(1));
            Assert.That(row.ReconciliationStatus, Is.EqualTo("Pending"));
            Assert.That(database.Context.ChangeTracker.Entries(), Is.Empty);
        }
    }

    [Test]
    public async Task QueryRequiresExactBotAndAccountOwnershipBeforeFilteringOrPaging()
    {
        await using var database = await SeedAsync();
        var queries = new OperatorPortfolioBrokerQueries(database.Context);
        var wrongBot = await queries.GetAuthorizedAsync(new(TradingBotId.Parse(OtherBot), BrokerAccountId.Parse(Account)), new(0, 10), default);
        var wrongAccount = await queries.GetAuthorizedAsync(new(TradingBotId.Parse(Bot), BrokerAccountId.Parse(OtherAccount)), new(0, 10), default);
        var filtered = await queries.GetAuthorizedAsync(new(TradingBotId.Parse(Bot), BrokerAccountId.Parse(Account), "no-match"), new(0, 10), default);
        Assert.That(wrongBot.Concat(wrongAccount).Concat(filtered), Is.Empty);
    }

    private static async Task<TemporarySqliteDatabase> SeedAsync()
    {
        var database = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(database.Context).InitializeAsync();
        var now = Now.ToUnixTimeMilliseconds();
        await using var command = database.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $$"""
            INSERT INTO broker_connections VALUES ('{{Connection}}','fixture','Paper fixture','Paper','ref://paper','Enabled','["Submit","Cancel"]',{{now}},{{now}},1);
            INSERT INTO broker_accounts VALUES ('{{Account}}','{{Connection}}','paper-a','Paper A','Cash','USD','Active',{{now}},'["Submit","Cancel"]',{{now}},{{now}},1);
            INSERT INTO trading_bots (id,name,status,created_at,updated_at,version) VALUES ('{{Bot}}','Owner','Enabled',{{now}},{{now}},1);
            INSERT INTO portfolios VALUES ('{{Portfolio}}','Alpha','USD','{{Account}}','{{Bot}}','Active','1234.5678','{}',{{now}},{{now}},1);
            INSERT INTO instruments VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA07','Equity','AAA','AAA','USD','NYSE',4,8,'Active',{{now}},{{now}},1);
            INSERT INTO instruments VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA14','Equity','BBB','BBB','USD','NYSE',4,8,'Active',{{now}},{{now}},1);
            INSERT INTO instrument_broker_mappings VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA08','01HF7YAT00S8K1M3Q5V7X9ZA07','{{Connection}}','AAA-PAPER','AAA','NYSE',{{now}},NULL,'{}');
            INSERT INTO positions VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA09','{{Portfolio}}','01HF7YAT00S8K1M3Q5V7X9ZA07','share','1.25','100','USD','0','USD',{{now}},{{now}},NULL,1);
            INSERT INTO positions VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA10','{{Portfolio}}','01HF7YAT00S8K1M3Q5V7X9ZA14','share','2.5','100','USD','0','USD',{{now}},{{now}},NULL,1);
            INSERT INTO portfolio_ledger_entries VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA11','{{Portfolio}}','Deposit','10.25','USD',NULL,NULL,{{now}},{{now}},'BrokerEvent','one',NULL,NULL,NULL);
            INSERT INTO portfolio_ledger_entries VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA12','{{Portfolio}}','Settlement','20.125','USD',NULL,NULL,{{now}},{{now}},'BrokerEvent','two',NULL,NULL,NULL);
            INSERT INTO broker_reconciliations VALUES ('01HF7YAT00S8K1M3Q5V7X9ZA13','{{Account}}','Pending',{{now}},NULL,'{}','{}','{}','correlation-1','{{new string('a', 64)}}');
            """;
        await command.ExecuteNonQueryAsync();
        database.Context.ChangeTracker.Clear();
        return database;
    }
}
