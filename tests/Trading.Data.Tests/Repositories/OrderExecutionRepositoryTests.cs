using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;

namespace Trading.Data.Tests.Repositories;

[Category("OrderRepositories"), Category("DurableBrokerWork")]
public sealed class OrderExecutionRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly int[] ExpectedTransitionSequence = [1, 2, 3, 4];

    [Test]
    public async Task OrderRoundTripsTransitionsAndFillsWithScopedLookupsAndConcurrency()
    {
        await using var fixture = await SeedAsync(); var ids = fixture.Ids; var repository = new OrderRepository(fixture.Database.Context);
        var order = NewOrder(ids); var envelope = new OrderPersistenceEnvelope(order, null, new CorrelationIdentity("corr-order"));
        Assert.That(await repository.AddAsync(envelope, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Database.Context.ChangeTracker.Clear(); var initial = await repository.GetAsync(order.Id, ids.Account, ids.Portfolio, default);
        Assert.Multiple(() => { Assert.That(initial!.Version, Is.Zero); Assert.That(initial.Currency, Is.EqualTo(Currency.USD)); Assert.That(initial.Quantity.Unit, Is.EqualTo("shares")); });
        Assert.That(await repository.GetAsync(order.Id, BrokerAccountId.New(), ids.Portfolio, default), Is.Null);
        order.BeginSubmission(OrderTransitionId.New(), Now.AddSeconds(1)); Assert.That(await repository.SaveAsync(envelope, 0, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Database.Context.ChangeTracker.Clear(); order.MarkSubmitted(OrderTransitionId.New(), Now.AddSeconds(2)); Assert.That(await repository.SaveAsync(envelope, 1, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Database.Context.ChangeTracker.Clear(); order.Acknowledge(OrderTransitionId.New(), "broker-1", Now.AddSeconds(3)); Assert.That(await repository.SaveAsync(envelope, 2, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        order.ApplyFill(FillId.New(), OrderTransitionId.New(), "execution-1", new Quantity(2, "shares"), new Price(10, Currency.USD), new Money(.1m, Currency.USD), Now.AddSeconds(3), Now.AddSeconds(4));
        Assert.That(await repository.SaveAsync(envelope, 3, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await repository.SaveAsync(envelope, 0, default), Is.TypeOf<PersistenceWriteResult.ConcurrencyConflict>());
        fixture.Database.Context.ChangeTracker.Clear(); var loaded = await repository.FindByClientOrderIdAsync(new ClientOrderIdentity(order.ClientOrderId), ids.Account, default);
        Assert.Multiple(() => { Assert.That(loaded!.Status, Is.EqualTo(OrderStatus.PartiallyFilled)); Assert.That(loaded.Transitions.Select(x => x.Sequence), Is.EqualTo(ExpectedTransitionSequence)); Assert.That(loaded.Fills.Single().BrokerExecutionId, Is.EqualTo("execution-1")); });
        Assert.That((await repository.FindFillAsync("execution-1", ids.Account, order.Id, default))!.Quantity.Unit, Is.EqualTo("shares"));
    }

    [Test]
    public async Task WorkClaimsAreExclusiveRetryableAndRecoverExpiredLeases()
    {
        await using var fixture = await SeedAsync(); var order = NewOrder(fixture.Ids); await new OrderRepository(fixture.Database.Context).AddAsync(new(order, null, new("corr-order")), default);
        var repository = new OrderWorkRepository(fixture.Database.Context); var work = new OrderWorkEnvelope(OrderWorkItemId.New(), order.Id, OrderWorkKind.Submit, "submit-1", "{\"a\":1}", new("corr-work"), 0, Now, Now);
        Assert.That(await repository.EnqueueAsync(work, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await repository.EnqueueAsync(work with { Id = OrderWorkItemId.New() }, default), Is.TypeOf<PersistenceWriteResult.UniquenessConflict>());
        var first = await repository.ClaimAsync(1, Now, new("owner-a", Now.AddMinutes(1)), default);
        Assert.That(first, Has.Count.EqualTo(1)); Assert.That(await repository.ClaimAsync(1, Now, new("owner-b", Now.AddMinutes(1)), default), Is.Empty);
        var recovered = await repository.ClaimAsync(1, Now.AddMinutes(2), new("owner-b", Now.AddMinutes(3)), default);
        Assert.That(recovered.Single().Attempt, Is.EqualTo(2));
        Assert.That(await repository.RenewAsync(work.Id, "owner-b", Now.AddMinutes(4), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await repository.RetryAsync(work.Id, "owner-b", "broker.retryable", Now.AddMinutes(5), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await repository.ClaimAsync(1, Now.AddMinutes(4), new("owner-c", Now.AddMinutes(6)), default), Is.Empty);
        Assert.That(await repository.ClaimAsync(1, Now.AddMinutes(5), new("owner-c", Now.AddMinutes(6)), default), Has.Count.EqualTo(1));
        Assert.That(await repository.FailAsync(work.Id, "owner-c", "broker_work.retry_exhausted", Now.AddMinutes(6), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await repository.ClaimAsync(1, Now.AddMinutes(7), new("owner-d", Now.AddMinutes(8)), default), Is.Empty);
    }

    [Test]
    public async Task InboxDeduplicatesAndReconciliationHistoryIsAccountIsolatedAndOrdered()
    {
        await using var fixture = await SeedAsync(); var inbox = new BrokerInboxRepository(fixture.Database.Context); var message = new BrokerInboxEnvelope(BrokerMessageId.New(), "event-1", "{\"a\":1}", new("corr-event"), Now);
        Assert.That(await inbox.ReceiveAsync(message, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await inbox.ReceiveAsync(message with { Id = BrokerMessageId.New() }, default), Is.TypeOf<PersistenceWriteResult.UniquenessConflict>());
        var claimed = await inbox.ClaimAsync(1, Now, new("owner", Now.AddMinutes(1)), default);
        Assert.That(claimed, Has.Count.EqualTo(1));
        Assert.That(claimed.Single().Attempt, Is.EqualTo(1));
        Assert.That(await inbox.RenewAsync(message.Id, "owner", Now.AddMinutes(2), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await inbox.CompleteAsync(message.Id, "owner", "broker.accepted", Now.AddSeconds(1), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var reconciliations = new BrokerReconciliationRepository(fixture.Database.Context);
        foreach (var minute in new[] { 2, 1 }) Assert.That(await reconciliations.AppendAsync(new(Guid.NewGuid().ToString("N"), fixture.Ids.Account, "Matched", Now.AddMinutes(minute), Now.AddMinutes(minute), "{}", "{}", "{}", new($"corr-{minute}"), new string((char)('a' + minute), 64)), default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That((await reconciliations.ListAsync(fixture.Ids.Account, default)).Select(x => x.StartedAt), Is.Ordered);
        Assert.That(await reconciliations.ListAsync(BrokerAccountId.New(), default), Is.Empty);
    }

    private static Order NewOrder(SeedIds x) => new(OrderId.New(), "client-" + Guid.NewGuid().ToString("N"), x.Portfolio, x.Account, x.Proposal, x.Instrument, OrderSide.Buy, new Quantity(10, "shares"), Currency.USD, OrderType.Market, null, TimeInForce.Day, Now);
    private static async Task<SeedFixture> SeedAsync()
    {
        var database = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(database.Context).InitializeAsync();
        var ids = new SeedIds(BrokerConnectionId.New(), BrokerAccountId.New(), TradingBotId.New(), TradingBotConfigurationVersionId.New(), InstrumentId.New(), PortfolioId.New(), PortfolioDecisionSnapshotId.New(), BotRunId.New(), TradeProposalId.New());
        async Task Sql(string sql) { await using var command = database.Context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
        await Sql($"INSERT INTO broker_connections VALUES ('{ids.Connection}','Simulated','Paper','Paper','secret-ref','Enabled','{{}}',1,1,1)");
        await Sql($"INSERT INTO broker_accounts VALUES ('{ids.Account}','{ids.Connection}','paper','Paper','Cash','USD','Active',NULL,'{{}}',1,1,1)");
        await Sql($"INSERT INTO trading_bots (id,name,status,active_configuration_version_id,created_at,updated_at,version) VALUES ('{ids.Bot}','Bot','Enabled',NULL,1,1,1)");
        await Sql($"INSERT INTO trading_bot_configuration_versions VALUES ('{ids.Configuration}','{ids.Bot}',1,'{{}}','{{}}','{{}}','{{}}','{{}}','PaperTrading','{{}}','p','{new string('a', 64)}',1,1,NULL)"); await Sql($"UPDATE trading_bots SET active_configuration_version_id='{ids.Configuration}' WHERE id='{ids.Bot}'");
        await Sql($"INSERT INTO instruments VALUES ('{ids.Instrument}','Equity','AAPL','Apple','USD','NASDAQ',8,8,'Active',1,1,1)");
        await Sql($"INSERT INTO portfolios VALUES ('{ids.Portfolio}','P','USD','{ids.Account}','{ids.Bot}','Active','1000','{{}}',1,1,1)");
        await Sql($"INSERT INTO portfolio_decision_snapshots VALUES ('{ids.Snapshot}','{ids.Portfolio}','{ids.Bot}','{ids.Configuration}',1,'Reconciled','{{}}',1,'{{}}','{new string('b', 64)}',1)");
        await Sql($"INSERT INTO bot_runs VALUES ('{ids.Run}','{ids.Bot}','{ids.Configuration}','{ids.Snapshot}','Completed',NULL,NULL,1,2,'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,'{new string('c', 64)}')");
        await Sql($"INSERT INTO trade_proposals VALUES ('{ids.Proposal}','{ids.Bot}','{ids.Run}','{ids.Portfolio}','{ids.Snapshot}','{ids.Configuration}','{ids.Instrument}','DirectTrade','{{}}','r',NULL,'Approved',1,99,'idem',1)");
        database.Context.ChangeTracker.Clear(); return new(database, ids);
    }
    private sealed record SeedFixture(TemporarySqliteDatabase Database, SeedIds Ids) : IAsyncDisposable { public ValueTask DisposeAsync() => Database.DisposeAsync(); }
    private sealed record SeedIds(BrokerConnectionId Connection, BrokerAccountId Account, TradingBotId Bot, TradingBotConfigurationVersionId Configuration, InstrumentId Instrument, PortfolioId Portfolio, PortfolioDecisionSnapshotId Snapshot, BotRunId Run, TradeProposalId Proposal);
}
