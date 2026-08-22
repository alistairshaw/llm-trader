using Microsoft.EntityFrameworkCore;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Data.Tests.Queries;

[TestFixture, Category("OrderProjections"), Category("ExecutionAudit")]
public sealed class OrderExecutionQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedFirstPage = ["01AREEEEEEEEEEEEEEEEEEEEE3", "01AREEEEEEEEEEEEEEEEEEEEE1"];

    [Test]
    public async Task AuthorizedFiltersRunBeforeStableBoundedPagination()
    {
        await using var fixture = await SeedAsync();
        await AddOrderAsync(fixture, "01AREEEEEEEEEEEEEEEEEEEEE2", Now.AddMinutes(2));
        await AddOrderAsync(fixture, "01AREEEEEEEEEEEEEEEEEEEEE1", Now.AddMinutes(2));
        await AddOrderAsync(fixture, "01AREEEEEEEEEEEEEEEEEEEEE3", Now.AddMinutes(3));
        fixture.Context.ChangeTracker.Clear();
        var queries = new OrderExecutionQueries(fixture.Context);
        var first = await queries.GetOrdersAsync(Principal(), new(PortfolioId: PortfolioId.Parse(Portfolio)), new(0, 2), default);
        var second = await queries.GetOrdersAsync(Principal(), new(), new(2, 2), default);
        Assert.Multiple(() =>
        {
            Assert.That(first.Select(x => x.Id.ToString()), Is.EqualTo(ExpectedFirstPage));
            Assert.That(second.Single().Id.ToString(), Is.EqualTo("01AREEEEEEEEEEEEEEEEEEEEE2"));
            Assert.That(() => new ExecutionPageRequest(0, 101), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(fixture.Context.ChangeTracker.Entries(), Is.Empty);
        });
    }

    [Test]
    public async Task EveryOwnershipGrantIsRequiredAndAdministratorStillRequiresConsistentOwnership()
    {
        await using var fixture = await SeedAsync(); var order = await AddOrderAsync(fixture, Order, Now);
        fixture.Context.ChangeTracker.Clear(); var queries = new OrderExecutionQueries(fixture.Context);
        Assert.Multiple(async () =>
        {
            Assert.That(await queries.GetOrderAsync(new("actor", false, [], [PortfolioId.Parse(Portfolio)], [BrokerAccountId.Parse(Account)]), order.Id, default), Is.Null);
            Assert.That(await queries.GetOrderAsync(new("actor", false, [TradingBotId.Parse(Bot)], [], [BrokerAccountId.Parse(Account)]), order.Id, default), Is.Null);
            Assert.That(await queries.GetOrderAsync(new("actor", false, [TradingBotId.Parse(Bot)], [PortfolioId.Parse(Portfolio)], []), order.Id, default), Is.Null);
            Assert.That(await queries.GetOrderAsync(Principal(), order.Id, default), Is.Not.Null);
        });
    }

    [Test]
    public async Task DetailReconstructsExactFillFinancialsAndChronologicalAuditWithoutTracking()
    {
        await using var fixture = await SeedAsync(); var order = await AddOrderAsync(fixture, Order, Now);
        var repository = new OrderRepository(fixture.Context); var envelope = new OrderPersistenceEnvelope(order, null, new("corr-" + Order));
        order.BeginSubmission(OrderTransitionId.Parse("01ATEEEEEEEEEEEEEEEEEEEEEE"), Now.AddSeconds(1));
        Assert.That(await repository.SaveAsync(envelope, 0, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Context.ChangeTracker.Clear();
        order.MarkSubmitted(OrderTransitionId.Parse("01ATEEEEEEEEEEEEEEEEEEEEEF"), Now.AddSeconds(2));
        Assert.That(await repository.SaveAsync(envelope, 1, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Context.ChangeTracker.Clear();
        order.Acknowledge(OrderTransitionId.Parse("01ATEEEEEEEEEEEEEEEEEEEEEH"), "broker-order", Now.AddSeconds(3));
        Assert.That(await repository.SaveAsync(envelope, 2, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Context.ChangeTracker.Clear();
        order.ApplyFill(FillId.Parse("01FAEEEEEEEEEEEEEEEEEEEEEE"), OrderTransitionId.Parse("01ATEEEEEEEEEEEEEEEEEEEEEG"),
            "execution-1", new Quantity(2, "shares"), new Price(12.5m, Currency.USD), new Money(.25m, Currency.USD), Now.AddSeconds(4), Now.AddSeconds(5));
        Assert.That(await repository.SaveAsync(envelope, 3, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Context.ChangeTracker.Clear();
        var detail = await new OrderExecutionQueries(fixture.Context).GetOrderAsync(Principal(), order.Id, default);
        Assert.Multiple(() =>
        {
            Assert.That(detail!.FilledQuantity, Is.EqualTo(2)); Assert.That(detail.GrossAmount, Is.EqualTo(25));
            Assert.That(detail.Fees, Is.EqualTo(.25m)); Assert.That(detail.Fills.Single().BrokerExecutionId, Is.EqualTo("execution-1"));
            Assert.That(detail.Audit.Select(x => x.At), Is.Ordered); Assert.That(detail.Audit.Any(x => x.Kind == "fill"), Is.True);
            Assert.That(fixture.Context.ChangeTracker.Entries(), Is.Empty);
        });
    }

    [Test]
    public async Task QueuePlanUsesThePortfolioStatusCreatedIndex()
    {
        await using var fixture = await SeedAsync();
        await using var command = fixture.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT id FROM orders WHERE portfolio_id=$portfolio AND status=$status ORDER BY created_at DESC LIMIT 10";
        var portfolio = command.CreateParameter(); portfolio.ParameterName = "$portfolio"; portfolio.Value = Portfolio; command.Parameters.Add(portfolio);
        var status = command.CreateParameter(); status.ParameterName = "$status"; status.Value = "Created"; command.Parameters.Add(status);
        await using var reader = await command.ExecuteReaderAsync(); var details = new List<string>();
        while (await reader.ReadAsync()) details.Add(reader.GetString(3));
        Assert.That(details.Any(x => x.Contains("IX_orders_portfolio_id_status_created_at", StringComparison.Ordinal)), Is.True);
    }

    private const string Bot = "01EEEEEEEEEEEEEEEEEEEEEEEE", Portfolio = "01PFEEEEEEEEEEEEEEEEEEEEEE";
    private const string Account = "01ACEEEEEEEEEEEEEEEEEEEEEE", Connection = "01CNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Proposal = "01PPEEEEEEEEEEEEEEEEEEEEE1", Instrument = "01MNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Order = "01AREEEEEEEEEEEEEEEEEEEEE1";
    private static ExecutionQueryPrincipal Principal() => new("actor", false, [TradingBotId.Parse(Bot)], [PortfolioId.Parse(Portfolio)], [BrokerAccountId.Parse(Account)]);

    private static async Task<Order> AddOrderAsync(TemporarySqliteDatabase fixture, string id, DateTimeOffset at)
    {
        var proposal = id.EndsWith('2') ? "01PPEEEEEEEEEEEEEEEEEEEEE2" : id.EndsWith('3') ? "01PPEEEEEEEEEEEEEEEEEEEEE3" : Proposal;
        var order = new Order(OrderId.Parse(id), "client-" + id, PortfolioId.Parse(Portfolio), BrokerAccountId.Parse(Account),
            TradeProposalId.Parse(proposal), InstrumentId.Parse(Instrument), OrderSide.Buy, new Quantity(10, "shares"),
            Currency.USD, OrderType.Market, null, TimeInForce.Day, at);
        Assert.That(await new OrderRepository(fixture.Context).AddAsync(new(order, null, new("corr-" + id)), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Context.ChangeTracker.Clear(); return order;
    }

    private static async Task<TemporarySqliteDatabase> SeedAsync()
    {
        var f = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(f.Context).InitializeAsync();
        var at = Now.ToUnixTimeMilliseconds(); var hash = new string('a', 64);
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO broker_connections VALUES ({Connection},'sim','Sim','Paper','ref','Enabled','{{}}',{at},{at},1)");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO broker_accounts VALUES ({Account},{Connection},'paper','Paper','Margin','USD','Active',{at},'{{}}',{at},{at},1)");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bots (id,name,status,created_at,updated_at,version) VALUES ({Bot},'Bot','Enabled',{at},{at},1)");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trading_bot_configuration_versions VALUES ('01CFEEEEEEEEEEEEEEEEEEEEEE',{Bot},1,'{{}}','{{}}','{{}}','{{}}','{{}}','HumanApproval','{{}}','p',{hash},{at},{at},NULL)");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"UPDATE trading_bots SET active_configuration_version_id='01CFEEEEEEEEEEEEEEEEEEEEEE' WHERE id={Bot}");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO instruments VALUES ({Instrument},'Equity','AAPL','Apple','USD','NASDAQ',8,8,'Active',{at},{at},1)");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolios VALUES ({Portfolio},'P','USD',{Account},{Bot},'Active','1000','{{}}',{at},{at},1)");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portfolio_decision_snapshots VALUES ('01PSEEEEEEEEEEEEEEEEEEEEEE',{Portfolio},{Bot},'01CFEEEEEEEEEEEEEEEEEEEEEE',{at},'Reconciled','{{}}',1,'{{}}',{hash},{at})");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO bot_runs VALUES ('01BREEEEEEEEEEEEEEEEEEEEEE',{Bot},'01CFEEEEEEEEEEEEEEEEEEEEEE','01PSEEEEEEEEEEEEEEEEEEEEEE','Completed',NULL,NULL,{at},{at},'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,{hash})");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trade_proposals VALUES ({Proposal},{Bot},'01BREEEEEEEEEEEEEEEEEEEEEE',{Portfolio},'01PSEEEEEEEEEEEEEEEEEEEEEE','01CFEEEEEEEEEEEEEEEEEEEEEE',{Instrument},'DirectTrade','{{\"executionMode\":\"PaperTrading\"}}','r',NULL,'ConvertedToOrder',{at},{Now.AddHours(1).ToUnixTimeMilliseconds()},'key',1)");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trade_proposals SELECT '01PPEEEEEEEEEEEEEEEEEEEEE2',trading_bot_id,bot_run_id,portfolio_id,portfolio_snapshot_id,configuration_version_id,instrument_id,proposal_type,requested_action_json,rationale,hypothesis_version_id,status,created_at,valid_until,'key2',version FROM trade_proposals WHERE id={Proposal}");
        await f.Context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO trade_proposals SELECT '01PPEEEEEEEEEEEEEEEEEEEEE3',trading_bot_id,bot_run_id,portfolio_id,portfolio_snapshot_id,configuration_version_id,instrument_id,proposal_type,requested_action_json,rationale,hypothesis_version_id,status,created_at,valid_until,'key3',version FROM trade_proposals WHERE id={Proposal}");
        f.Context.ChangeTracker.Clear(); return f;
    }
}
