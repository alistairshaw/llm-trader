using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;

namespace Trading.Data.Tests.Transactions;

[Category("ConcurrencyOrTransactions")]
public sealed class ConcurrencyTransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 20, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task TwoContextsCannotCommitTheSameExpectedPortfolioVersion()
    {
        await using var database = await CreateDatabaseAsync();
        var portfolio = NewPortfolio("Concurrent");
        await new PortfolioRepository(database.Context).AddAsync(portfolio, default);
        database.Context.ChangeTracker.Clear();
        await using var secondContext = CreateContext(database.DatabasePath);

        var first = (await new PortfolioRepository(database.Context).GetAsync(portfolio.Id, default))!;
        var second = (await new PortfolioRepository(secondContext).GetAsync(portfolio.Id, default))!;
        first.Pause(); second.Close();

        Assert.That(await new PortfolioRepository(database.Context).UpdateAsync(first, 0, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var staleResult = await new PortfolioRepository(secondContext).UpdateAsync(second, 0, default);

        Assert.That(staleResult, Is.EqualTo(new PersistenceWriteResult.ConcurrencyConflict(0, 1)));
        secondContext.ChangeTracker.Clear();
        var committed = await new PortfolioRepository(secondContext).GetAsync(portfolio.Id, default);
        Assert.Multiple(() =>
        {
            Assert.That(committed!.Status, Is.EqualTo(PortfolioStatus.Paused));
            Assert.That(committed.Version, Is.EqualTo(1));
        });
    }

    [TestCase(nameof(TransactionFailpoint.AfterMaterialWrite))]
    [TestCase(nameof(TransactionFailpoint.BeforeCommit))]
    public async Task LedgerFailpointsRollBackTheAppend(string failpointName)
    {
        var failpoint = Enum.Parse<TransactionFailpoint>(failpointName);
        await using var database = await CreateDatabaseAsync();
        var portfolio = await AddPortfolioAsync(database.Context);
        var entry = new PortfolioLedgerEntry(PortfolioLedgerEntryId.New(), portfolio.Id, PortfolioLedgerEntryType.Deposit,
            new Money(25, Currency.USD), null, null, Now, LedgerSourceType.BrokerEvent, "rollback-ledger");
        var operations = FailingOperations(database.Context, failpoint);

        Assert.That(async () => await operations.AppendLedgerEntryAsync(entry, default), Throws.TypeOf<TestFailpointException>());
        Assert.That(await CountAsync(database.Context, "portfolio_ledger_entries"), Is.Zero);
    }

    [TestCase(nameof(TransactionFailpoint.AfterMaterialWrite))]
    [TestCase(nameof(TransactionFailpoint.BeforeCommit))]
    public async Task PositionFailpointsRollBackPositionAndAppliedFill(string failpointName)
    {
        var failpoint = Enum.Parse<TransactionFailpoint>(failpointName);
        await using var database = await CreateDatabaseAsync();
        var portfolio = await AddPortfolioAsync(database.Context);
        var instrument = await AddInstrumentAsync(database.Context);
        var position = new Position(PositionId.New(), portfolio.Id, instrument.Id, "share", Currency.USD, Now);
        await new PositionRepository(database.Context).AddAsync(position, default);
        database.Context.ChangeTracker.Clear();
        var changed = (await new PositionRepository(database.Context).GetAsync(position.Id, default))!;
        changed.ApplyChange(2, new Money(10, Currency.USD), Money.Zero(Currency.USD), PositionChangeSource.Execution, "fill-rollback", Now.AddMinutes(1));

        Assert.That(async () => await FailingOperations(database.Context, failpoint).ApplyPositionFillAsync(changed, 0, default), Throws.TypeOf<TestFailpointException>());
        database.Context.ChangeTracker.Clear();
        var stored = await new PositionRepository(database.Context).GetAsync(position.Id, default);
        Assert.Multiple(() =>
        {
            Assert.That(stored!.Quantity, Is.Zero);
            Assert.That(stored.Version, Is.Zero);
            Assert.That(stored.AppliedSources, Is.Empty);
        });
    }

    [TestCase(nameof(TransactionFailpoint.AfterMaterialWrite))]
    [TestCase(nameof(TransactionFailpoint.BeforeCommit))]
    public async Task OwnershipFailpointsRollBackPortfolioAssignment(string failpointName)
    {
        var failpoint = Enum.Parse<TransactionFailpoint>(failpointName);
        await using var database = await CreateDatabaseAsync();
        var portfolio = await AddPortfolioAsync(database.Context);
        var bot = NewBot("Owner");
        await new TradingBotRepository(database.Context).AddAsync(bot, default);
        database.Context.ChangeTracker.Clear();
        var changed = (await new PortfolioRepository(database.Context).GetAsync(portfolio.Id, default))!;
        changed.AssignTradingBot(bot.Id);

        Assert.That(async () => await FailingOperations(database.Context, failpoint).AssignPortfolioOwnershipAsync(changed, 0, default), Throws.TypeOf<TestFailpointException>());
        database.Context.ChangeTracker.Clear();
        var stored = await new PortfolioRepository(database.Context).GetAsync(portfolio.Id, default);
        Assert.Multiple(() => { Assert.That(stored!.AssignedTradingBotId, Is.Null); Assert.That(stored.Version, Is.Zero); });
    }

    [TestCase(nameof(TransactionFailpoint.AfterMaterialWrite))]
    [TestCase(nameof(TransactionFailpoint.BeforeCommit))]
    public async Task SnapshotFailpointsRollBackTheCompleteSnapshot(string failpointName)
    {
        var failpoint = Enum.Parse<TransactionFailpoint>(failpointName);
        await using var database = await CreateDatabaseAsync();
        var portfolio = await AddPortfolioAsync(database.Context);
        var bot = NewBot("Snapshot owner");
        await new TradingBotRepository(database.Context).AddAsync(bot, default);
        portfolio.AssignTradingBot(bot.Id);
        await new PortfolioRepository(database.Context).UpdateAsync(portfolio, 0, default);
        database.Context.ChangeTracker.Clear();
        var snapshot = new PortfolioDecisionSnapshot(PortfolioDecisionSnapshotId.New(), portfolio.Id, bot.Id,
            bot.ActiveConfigurationVersionId!, Now, ReconciliationStatus.Reconciled, new Money(100, Currency.USD),
            new Money(100, Currency.USD), Money.Zero(Currency.USD), [], [], 0, [],
            new DataFreshness(Now, Now, TimeSpan.FromMinutes(5)), Now);

        Assert.That(async () => await FailingOperations(database.Context, failpoint).CreateDecisionSnapshotAsync(snapshot, default), Throws.TypeOf<TestFailpointException>());
        Assert.That(await CountAsync(database.Context, "portfolio_decision_snapshots"), Is.Zero);
    }

    [TestCase(nameof(TransactionFailpoint.AfterMaterialWrite))]
    [TestCase(nameof(TransactionFailpoint.BeforeCommit))]
    public async Task InitialBotFailpointsRollBackBotAndConfiguration(string failpointName)
    {
        var failpoint = Enum.Parse<TransactionFailpoint>(failpointName);
        await using var database = await CreateDatabaseAsync();
        Assert.That(async () => await FailingOperations(database.Context, failpoint).CreateBotAsync(NewBot("Rollback bot"), default), Throws.TypeOf<TestFailpointException>());
        Assert.Multiple(() =>
        {
            Assert.That(CountAsync(database.Context, "trading_bots").Result, Is.Zero);
            Assert.That(CountAsync(database.Context, "trading_bot_configuration_versions").Result, Is.Zero);
        });
    }

    [Test]
    public async Task SuccessfulMutableWritesIncrementExactlyOnceAndUniquenessIsTranslated()
    {
        await using var database = await CreateDatabaseAsync();
        var portfolio = await AddPortfolioAsync(database.Context);
        portfolio.Pause();
        Assert.That(await new Stage2TransactionOperations(database.Context).AssignPortfolioOwnershipAsync(portfolio, 0, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear();
        Assert.That((await new PortfolioRepository(database.Context).GetAsync(portfolio.Id, default))!.Version, Is.EqualTo(1));

        var duplicate = NewPortfolio("Duplicate ownership");
        var account = await AddAccountAsync(database.Context);
        portfolio = (await new PortfolioRepository(database.Context).GetAsync(portfolio.Id, default))!; portfolio.Activate(); portfolio.AssociateBrokerAccount(account.Id);
        await new PortfolioRepository(database.Context).UpdateAsync(portfolio, 1, default);
        duplicate.AssociateBrokerAccount(account.Id);
        var result = await new PortfolioRepository(database.Context).AddAsync(duplicate, default);
        Assert.That(result, Is.EqualTo(new PersistenceWriteResult.UniquenessConflict("active_portfolio_ownership")));
    }

    private static Stage2TransactionOperations FailingOperations(TradingDbContext context, TransactionFailpoint selected) =>
        new(context, point => { if (point == selected) throw new TestFailpointException(); });

    private static TradingDbContext CreateContext(string databasePath) => new(TradingDbContextFactory.CreateOptions(
        new DatabaseOptions { DatabasePath = databasePath }, TestContext.CurrentContext.TestDirectory));
    private static async Task<TemporarySqliteDatabase> CreateDatabaseAsync() { var db = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(db.Context).InitializeAsync(); return db; }
    private static Portfolio NewPortfolio(string name) => new(PortfolioId.New(), name, Currency.USD, new Money(1000, Currency.USD), 10, Now);
    private static async Task<Portfolio> AddPortfolioAsync(TradingDbContext context) { var value = NewPortfolio(Guid.NewGuid().ToString("N")); await new PortfolioRepository(context).AddAsync(value, default); return value; }
    private static async Task<Instrument> AddInstrumentAsync(TradingDbContext context) { var value = new Instrument(InstrumentId.New(), InstrumentType.Equity, Guid.NewGuid().ToString("N"), "Instrument", Currency.USD, "NYSE", 4, 8, Now); await new InstrumentRepository(context).AddAsync(value, default); return value; }
    private static async Task<BrokerAccount> AddAccountAsync(TradingDbContext context) { var connection = new BrokerConnection(BrokerConnectionId.New(), "sim", "Sim", BrokerEnvironment.Paper, "ref://sim", [], Now); await new BrokerConnectionRepository(context).AddAsync(connection, default); var account = new BrokerAccount(BrokerAccountId.New(), connection.Id, Guid.NewGuid().ToString("N"), "Account", "Cash", Currency.USD); await new BrokerAccountRepository(context).AddAsync(account, default); return account; }
    private static TradingBot NewBot(string name)
    {
        var bot = new TradingBot(TradingBotId.New(), name, Now);
        var version = bot.AddConfiguration(TradingBotConfigurationVersionId.New(), new InvestmentMandate("growth", TimeSpan.FromDays(365), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])),
            new RiskPolicy([new RiskLimit("position", 10, "percent")]), new ToolPolicy([new ToolAllowance("market-data", 4)]),
            new RunBudget(TimeSpan.FromMinutes(5), 1000, new Money(5, Currency.USD), 4, 2, 1),
            new SchedulingPolicy(TimeSpan.FromHours(1), TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)), ExecutionMode.PaperTrading,
            new ModelConfiguration("openai", "model", 0.2m, 2000), "prompt-v1", Now);
        bot.ActivateConfiguration(version.Id, Now);
        return bot;
    }
    private static async Task<long> CountAsync(TradingDbContext context, string table) => table switch
    {
        "portfolio_ledger_entries" => await context.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM portfolio_ledger_entries").SingleAsync(),
        "portfolio_decision_snapshots" => await context.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM portfolio_decision_snapshots").SingleAsync(),
        "trading_bots" => await context.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM trading_bots").SingleAsync(),
        "trading_bot_configuration_versions" => await context.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM trading_bot_configuration_versions").SingleAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(table))
    };

    private sealed class TestFailpointException : Exception;
}
