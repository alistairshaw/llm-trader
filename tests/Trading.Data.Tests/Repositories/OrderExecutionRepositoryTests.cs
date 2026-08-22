using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Orders;
using Trading.Core.Persistence;
using Trading.Core.Proposals;

namespace Trading.Data.Tests.Repositories;

[Category("OrderRepositories"), Category("DurableBrokerWork"), Category("BrokerInboxOutbox")]
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

    [Test, Category("BrokerOrderEvents")]
    public async Task BrokerAcknowledgementAtomicallyTransitionsOrderAndCompletesClaimedInbox()
    {
        await using var fixture = await SeedAsync();
        var orderRepository = new OrderRepository(fixture.Database.Context);
        var order = NewOrder(fixture.Ids);
        var envelope = new OrderPersistenceEnvelope(order, null, new("corr-order-event"));
        Assert.That(await orderRepository.AddAsync(envelope, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        order.BeginSubmission(OrderTransitionId.New(), Now.AddSeconds(1));
        Assert.That(await orderRepository.SaveAsync(envelope, 0, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Database.Context.ChangeTracker.Clear();
        order.MarkSubmitted(OrderTransitionId.New(), Now.AddSeconds(2));
        Assert.That(await orderRepository.SaveAsync(envelope, 1, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Database.Context.ChangeTracker.Clear();

        var inboxRepository = new BrokerInboxRepository(fixture.Database.Context);
        var message = new BrokerInboxEnvelope(BrokerMessageId.New(), "ack:" + order.ClientOrderId,
            "{\"event\":\"ack\"}", new("corr-order-event"), Now.AddSeconds(4));
        Assert.That(await inboxRepository.ReceiveAsync(message, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var claimed = await inboxRepository.ClaimAsync(1, Now.AddSeconds(4),
            new("worker", Now.AddMinutes(1)), default);
        Assert.That(claimed, Has.Count.EqualTo(1));

        var result = await new BrokerOrderEventRepository(fixture.Database.Context).ApplyAsync(
            new(claimed[0], "worker", fixture.Ids.Account, "Paper",
                new ClientOrderIdentity(order.ClientOrderId), "broker-order-1",
                BrokerOrderEventKind.Acknowledged, "broker.acknowledged", Now.AddSeconds(3),
                Now.AddSeconds(4)), default);

        fixture.Database.Context.ChangeTracker.Clear();
        var loaded = await orderRepository.FindByClientOrderIdAsync(
            new ClientOrderIdentity(order.ClientOrderId), fixture.Ids.Account, default);
        Assert.Multiple(() =>
        {
            Assert.That(result.Disposition, Is.EqualTo(BrokerOrderEventWriteDisposition.Applied));
            Assert.That(loaded!.Status, Is.EqualTo(OrderStatus.Acknowledged));
            Assert.That(loaded.BrokerOrderId, Is.EqualTo("broker-order-1"));
            Assert.That(loaded.Transitions[^1].Reason, Is.EqualTo("broker.acknowledged"));
        });
        Assert.That(await inboxRepository.ClaimAsync(1, Now.AddMinutes(2),
            new("other", Now.AddMinutes(3)), default), Is.Empty);
    }

    [Test, Category("BrokerOrderEvents")]
    public async Task BrokerRejectionReleasesTheActiveReservationExactlyOnce()
    {
        await using var fixture = await SeedAsync();
        var reservationId = CapitalReservationId.New();
        await fixture.Database.Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO capital_reservations (id,portfolio_id,trade_proposal_id,order_id,amount,currency,status,created_at,expires_at,consumed_at,released_at,version) VALUES ({0},{1},{2},NULL,'100','USD','Active',{3},{4},NULL,NULL,1)",
            reservationId.ToString(), fixture.Ids.Portfolio.ToString(), fixture.Ids.Proposal.ToString(),
            Now.ToUnixTimeMilliseconds(), Now.AddHours(1).ToUnixTimeMilliseconds());
        var order = NewOrder(fixture.Ids);
        var orderRepository = new OrderRepository(fixture.Database.Context);
        var envelope = new OrderPersistenceEnvelope(order, reservationId, new("corr-rejection"));
        Assert.That(await orderRepository.AddAsync(envelope, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        order.BeginSubmission(OrderTransitionId.New(), Now.AddSeconds(1));
        Assert.That(await orderRepository.SaveAsync(envelope, 0, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Database.Context.ChangeTracker.Clear();
        order.MarkSubmitted(OrderTransitionId.New(), Now.AddSeconds(2));
        Assert.That(await orderRepository.SaveAsync(envelope, 1, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        fixture.Database.Context.ChangeTracker.Clear();

        var inbox = new BrokerInboxRepository(fixture.Database.Context);
        var message = new BrokerInboxEnvelope(BrokerMessageId.New(), "reject:" + order.ClientOrderId,
            "{\"event\":\"reject\"}", new("corr-rejection"), Now.AddSeconds(4));
        Assert.That(await inbox.ReceiveAsync(message, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var claimed = await inbox.ClaimAsync(1, Now.AddSeconds(4), new("worker", Now.AddMinutes(1)), default);
        var repository = new BrokerOrderEventRepository(fixture.Database.Context);
        Assert.That((await repository.ApplyAsync(new(claimed[0], "worker", fixture.Ids.Account, "Paper",
            new ClientOrderIdentity(order.ClientOrderId), null, BrokerOrderEventKind.Rejected,
            "broker.rejected", Now.AddSeconds(3), Now.AddSeconds(4)), default)).Disposition,
            Is.EqualTo(BrokerOrderEventWriteDisposition.Applied));

        fixture.Database.Context.ChangeTracker.Clear();
        var reservation = await new CapitalReservationRepository(fixture.Database.Context)
            .GetAsync(reservationId, default);
        var loaded = await orderRepository.FindByClientOrderIdAsync(
            new ClientOrderIdentity(order.ClientOrderId), fixture.Ids.Account, default);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Status, Is.EqualTo(OrderStatus.Rejected));
            Assert.That(reservation!.Status, Is.EqualTo(CapitalReservationStatus.Released));
            Assert.That(reservation.ReleasedAt, Is.EqualTo(Now.AddSeconds(4)));
        });
    }

    [Test, Category("AtomicFillApplication")]
    public async Task PartialAndFinalBuyFillsAtomicallyProduceGoldenFinancialState()
    {
        await using var fixture = await SeedAsync();
        var reservationId = CapitalReservationId.New();
        await fixture.Database.Context.Database.ExecuteSqlRawAsync(
            "INSERT INTO capital_reservations (id,portfolio_id,trade_proposal_id,order_id,amount,currency,status,created_at,expires_at,consumed_at,released_at,version) VALUES ({0},{1},{2},NULL,'700','USD','Active',{3},{4},NULL,NULL,1)",
            reservationId.ToString(), fixture.Ids.Portfolio.ToString(), fixture.Ids.Proposal.ToString(), Now.ToUnixTimeMilliseconds(), Now.AddHours(1).ToUnixTimeMilliseconds());
        var order = NewOrder(fixture.Ids);
        var orders = new OrderRepository(fixture.Database.Context);
        var envelope = new OrderPersistenceEnvelope(order, reservationId, new("corr-fill"));
        await orders.AddAsync(envelope, default);
        order.BeginSubmission(OrderTransitionId.New(), Now.AddSeconds(1)); await orders.SaveAsync(envelope, 0, default); fixture.Database.Context.ChangeTracker.Clear();
        order.MarkSubmitted(OrderTransitionId.New(), Now.AddSeconds(2)); await orders.SaveAsync(envelope, 1, default); fixture.Database.Context.ChangeTracker.Clear();
        order.Acknowledge(OrderTransitionId.New(), "broker-fill", Now.AddSeconds(3)); await orders.SaveAsync(envelope, 2, default); fixture.Database.Context.ChangeTracker.Clear();

        var first = await ApplyFillAsync(fixture, order, "execution-alpha", 4m, 69.5m, 1.25m, 4);
        fixture.Database.Context.ChangeTracker.Clear();
        Assert.Multiple(() =>
        {
            Assert.That(first.Disposition, Is.EqualTo(FillAccountingWriteDisposition.Applied));
            Assert.That(fixture.Database.Context.Orders.Single().Status, Is.EqualTo(OrderStatus.PartiallyFilled));
            Assert.That(fixture.Database.Context.Positions.Single().Quantity, Is.EqualTo("4"));
            Assert.That(fixture.Database.Context.Positions.Single().AverageCostAmount, Is.EqualTo("69.5"));
            Assert.That(fixture.Database.Context.CapitalReservations.Single().Status, Is.EqualTo("Active"));
            Assert.That(fixture.Database.Context.PortfolioLedgerEntries.AsEnumerable().Sum(x => decimal.Parse(x.Amount!, System.Globalization.CultureInfo.InvariantCulture)), Is.EqualTo(-279.25m));
        });

        var second = await ApplyFillAsync(fixture, order, "execution-beta", 6m, 69.75m, 1.5m, 5);
        fixture.Database.Context.ChangeTracker.Clear();
        Assert.Multiple(() =>
        {
            Assert.That(second.Disposition, Is.EqualTo(FillAccountingWriteDisposition.Applied));
            Assert.That(fixture.Database.Context.Orders.Single().Status, Is.EqualTo(OrderStatus.Filled));
            Assert.That(fixture.Database.Context.Positions.Single().Quantity, Is.EqualTo("10"));
            Assert.That(fixture.Database.Context.Positions.Single().AverageCostAmount, Is.EqualTo("69.65"));
            Assert.That(fixture.Database.Context.Fills.Count(), Is.EqualTo(2));
            Assert.That(fixture.Database.Context.PositionAppliedFills.Count(), Is.EqualTo(2));
            Assert.That(fixture.Database.Context.PortfolioLedgerEntries.Count(), Is.EqualTo(4));
            Assert.That(fixture.Database.Context.CapitalReservations.Single().Status, Is.EqualTo("Consumed"));
        });
    }

    [Test, Category("AtomicFillApplication")]
    public async Task DuplicateAndOverfillNeverRepeatFinancialEffects()
    {
        await using var fixture = await SeedAsync();
        var order = NewOrder(fixture.Ids); var orders = new OrderRepository(fixture.Database.Context);
        var envelope = new OrderPersistenceEnvelope(order, null, new("corr-fill-safety")); await orders.AddAsync(envelope, default);
        order.BeginSubmission(OrderTransitionId.New(), Now.AddSeconds(1)); await orders.SaveAsync(envelope, 0, default); fixture.Database.Context.ChangeTracker.Clear();
        order.MarkSubmitted(OrderTransitionId.New(), Now.AddSeconds(2)); await orders.SaveAsync(envelope, 1, default); fixture.Database.Context.ChangeTracker.Clear();
        order.Acknowledge(OrderTransitionId.New(), "broker-fill", Now.AddSeconds(3)); await orders.SaveAsync(envelope, 2, default); fixture.Database.Context.ChangeTracker.Clear();
        await ApplyFillAsync(fixture, order, "execution-alpha", 4, 10, 1, 4); fixture.Database.Context.ChangeTracker.Clear();
        var duplicate = await ApplyFillAsync(fixture, order, "execution-alpha", 4, 10, 1, 5); fixture.Database.Context.ChangeTracker.Clear();
        var overfill = await ApplyFillAsync(fixture, order, "execution-beta", 7, 10, 0, 6); fixture.Database.Context.ChangeTracker.Clear();
        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Disposition, Is.EqualTo(FillAccountingWriteDisposition.Duplicate));
            Assert.That(overfill.Code, Is.EqualTo(FillAccountingCodes.Overfill));
            Assert.That(fixture.Database.Context.Fills.Count(), Is.EqualTo(1));
            Assert.That(fixture.Database.Context.Positions.Single().Quantity, Is.EqualTo("4"));
            Assert.That(fixture.Database.Context.PortfolioLedgerEntries.Count(), Is.EqualTo(2));
        });
    }

    [Test, Category("AtomicFillApplication")]
    public async Task LedgerFailpointRollsBackEveryFinancialEffectAndInboxCompletion()
    {
        await using var fixture = await SeedAsync();
        var order = NewOrder(fixture.Ids); var orders = new OrderRepository(fixture.Database.Context);
        var envelope = new OrderPersistenceEnvelope(order, null, new("corr-fill-rollback")); await orders.AddAsync(envelope, default);
        order.BeginSubmission(OrderTransitionId.New(), Now.AddSeconds(1)); await orders.SaveAsync(envelope, 0, default); fixture.Database.Context.ChangeTracker.Clear();
        order.MarkSubmitted(OrderTransitionId.New(), Now.AddSeconds(2)); await orders.SaveAsync(envelope, 1, default); fixture.Database.Context.ChangeTracker.Clear();
        order.Acknowledge(OrderTransitionId.New(), "broker-fill", Now.AddSeconds(3)); await orders.SaveAsync(envelope, 2, default); fixture.Database.Context.ChangeTracker.Clear();
        fixture.Database.Context.PortfolioLedgerEntries.Add(new()
        {
            Id = PortfolioLedgerEntryId.New().ToString(),
            PortfolioId = fixture.Ids.Portfolio.ToString(),
            EntryType = "Settlement",
            Amount = "0",
            Currency = "USD",
            InstrumentId = fixture.Ids.Instrument.ToString(),
            Quantity = "0",
            EffectiveAt = Now.ToUnixTimeMilliseconds(),
            RecordedAt = Now.ToUnixTimeMilliseconds(),
            SourceType = "BrokerExecution",
            SourceId = "execution-alpha:trade"
        });
        await fixture.Database.Context.SaveChangesAsync(); fixture.Database.Context.ChangeTracker.Clear();

        Assert.That(async () => await ApplyFillAsync(fixture, order, "execution-alpha", 4, 10, 1, 4), Throws.TypeOf<DbUpdateException>());
        fixture.Database.Context.ChangeTracker.Clear();
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Database.Context.Orders.Single().Status, Is.EqualTo(OrderStatus.Acknowledged));
            Assert.That(fixture.Database.Context.Positions.Count(), Is.Zero);
            Assert.That(fixture.Database.Context.Fills.Count(), Is.Zero);
            Assert.That(fixture.Database.Context.PositionAppliedFills.Count(), Is.Zero);
            Assert.That(fixture.Database.Context.PortfolioLedgerEntries.Count(), Is.EqualTo(1));
            Assert.That(fixture.Database.Context.InboxMessages.Single().Status, Is.EqualTo("Claimed"));
        });
    }

    private static async Task<FillAccountingWriteResult> ApplyFillAsync(SeedFixture fixture, Order order,
        string executionId, decimal quantity, decimal price, decimal fee, int second)
    {
        var inbox = new BrokerInboxRepository(fixture.Database.Context);
        var message = new BrokerInboxEnvelope(BrokerMessageId.New(), $"fill:{executionId}:{second}", "{\"event\":\"fill\"}",
            new($"corr-fill-{second}"), Now.AddSeconds(second));
        await inbox.ReceiveAsync(message, default);
        var claimed = (await inbox.ClaimAsync(1, Now.AddSeconds(second), new("fill-worker", Now.AddMinutes(1)), default)).Single();
        return await new FillAccountingRepository(fixture.Database.Context).ApplyAsync(new(claimed, "fill-worker",
            fixture.Ids.Account, "Paper", new(order.ClientOrderId), "broker-fill",
            new(executionId, new(quantity, "shares"), new(price, Currency.USD), new(fee, Currency.USD),
                Now.AddSeconds(executionId == "execution-alpha" ? 4 : second)),
            Now.AddSeconds(second)), default);
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
