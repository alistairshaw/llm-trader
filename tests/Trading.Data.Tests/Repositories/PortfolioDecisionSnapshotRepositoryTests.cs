using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;
using Trading.Core.Portfolios;

namespace Trading.Data.Tests.Repositories;

[Category("DecisionSnapshots")]
public sealed class PortfolioDecisionSnapshotRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 18, 30, 0, TimeSpan.Zero);
    private static readonly string[] RepositoryOperations = ["GetAsync", "PublishAsync"];

    [Test]
    public void EquivalentInputsHaveByteIdenticalCanonicalContentAndKnownHash()
    {
        var ids = SnapshotIds.Create();
        var first = NewSnapshot(ids, [Position(ids.Instrument2, 2), Position(ids.Instrument1, 1)], [Order(ids.Order2, ids.Instrument2), Order(ids.Order1, ids.Instrument1)], [Flow(2, "two", Now.AddMinutes(-2)), Flow(1, "one", Now.AddMinutes(-3))]);
        var second = NewSnapshot(ids, [Position(ids.Instrument1, 1), Position(ids.Instrument2, 2)], [Order(ids.Order1, ids.Instrument1), Order(ids.Order2, ids.Instrument2)], [Flow(1, "one", Now.AddMinutes(-3)), Flow(2, "two", Now.AddMinutes(-2))]);
        Assert.Multiple(() =>
        {
            Assert.That(second.CanonicalContent, Is.EqualTo(first.CanonicalContent));
            Assert.That(second.ContentHash, Is.EqualTo(first.ContentHash));
            Assert.That(first.ContentHash, Is.EqualTo("fbe448f899d9f9164b3b2e37948ce7145f3b7914c0ce99c1e2ce82e75c784232"));
        });
    }

    [Test]
    public void MaterialChangeProducesDifferentContentAndHash()
    {
        var ids = SnapshotIds.Create(); var first = NewSnapshot(ids, [Position(ids.Instrument1, 1)], [], []); var changed = NewSnapshot(ids, [Position(ids.Instrument1, 1.00000001m)], [], []);
        Assert.Multiple(() => { Assert.That(changed.CanonicalContent, Is.Not.EqualTo(first.CanonicalContent)); Assert.That(changed.ContentHash, Is.Not.EqualTo(first.ContentHash)); });
    }

    [Test]
    public async Task PublishedSnapshotRoundTripsExactlyAndIsAppendOnly()
    {
        await using var database = await CreateDatabaseAsync(); var ownership = await AddOwnershipAsync(database.Context); var snapshot = NewSnapshot(ownership.Ids, [Position(ownership.Ids.Instrument1, 1.12345678m)], [Order(ownership.Ids.Order1, ownership.Ids.Instrument1)], [Flow(12.34567891m, "cash-1", Now.AddMinutes(-3))]);
        var repository = new PortfolioDecisionSnapshotRepository(database.Context);
        Assert.That(await repository.PublishAsync(snapshot, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear(); var loaded = await repository.GetAsync(snapshot.Id, default);
        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null); Assert.That(loaded!.CanonicalContent, Is.EqualTo(snapshot.CanonicalContent)); Assert.That(loaded.ContentHash, Is.EqualTo(snapshot.ContentHash));
            Assert.That(loaded.PositionSnapshots, Is.EqualTo(snapshot.PositionSnapshots)); Assert.That(loaded.OpenOrderSnapshots, Is.EqualTo(snapshot.OpenOrderSnapshots)); Assert.That(loaded.RelevantCashFlows, Is.EqualTo(snapshot.RelevantCashFlows));
            Assert.That(loaded.DataFreshness, Is.EqualTo(snapshot.DataFreshness)); Assert.That(loaded.AsOf, Is.EqualTo(snapshot.AsOf)); Assert.That(loaded.CreatedAt, Is.EqualTo(snapshot.CreatedAt)); Assert.That(loaded.SnapshotSchemaVersion, Is.EqualTo(1));
            Assert.That(typeof(IPortfolioDecisionSnapshotRepository).GetMethods().Select(x => x.Name), Is.EquivalentTo(RepositoryOperations));
        });
        Assert.That(async () => await database.Context.Database.ExecuteSqlRawAsync("UPDATE portfolio_decision_snapshots SET reconciliation_status = 'Pending'"), Throws.TypeOf<SqliteException>());
        Assert.That(async () => await database.Context.Database.ExecuteSqlRawAsync("DELETE FROM portfolio_decision_snapshots"), Throws.TypeOf<SqliteException>());
    }

    [Test]
    public async Task PublicationRejectsPortfolioBotAndConfigurationOwnershipMismatch()
    {
        await using var database = await CreateDatabaseAsync(); var ownership = await AddOwnershipAsync(database.Context); var other = await AddBotAsync(database.Context, "Other");
        var repository = new PortfolioDecisionSnapshotRepository(database.Context);
        var wrongBotIds = ownership.Ids with { Bot = other.Id, Configuration = other.ActiveConfigurationVersionId! };
        var wrongConfigurationIds = ownership.Ids with { Configuration = other.ActiveConfigurationVersionId! };
        Assert.Multiple(() =>
        {
            Assert.That(async () => await repository.PublishAsync(NewSnapshot(wrongBotIds, [], [], []), default), Throws.InvalidOperationException);
            Assert.That(async () => await repository.PublishAsync(NewSnapshot(wrongConfigurationIds, [], [], []), default), Throws.InvalidOperationException);
        });
    }

    [Test]
    public async Task RuntimeModelHasNoChangesPendingBeyondCommittedMigration()
    {
        await using var database = await CreateDatabaseAsync();
        Assert.That(database.Context.Database.HasPendingModelChanges(), Is.False);
    }

    private static PortfolioDecisionSnapshot NewSnapshot(SnapshotIds ids, IEnumerable<PositionSnapshot> positions, IEnumerable<OpenOrderSnapshot> orders, IEnumerable<CashFlowSnapshot> flows) =>
        new(PortfolioDecisionSnapshotId.New(), ids.Portfolio, ids.Bot, ids.Configuration, Now, ReconciliationStatus.Reconciled,
            new Money(100.12345678m, Currency.USD), new Money(90.12345678m, Currency.USD), new Money(10, Currency.USD), positions, orders,
            12.34567891m, flows, new DataFreshness(Now.AddMinutes(-5), Now.AddMinutes(-1), TimeSpan.FromMinutes(10)), Now);
    private static PositionSnapshot Position(InstrumentId id, decimal quantity) => new(id, quantity, new Money(quantity * 10, Currency.USD));
    private static OpenOrderSnapshot Order(OrderId id, InstrumentId instrumentId) => new(id, instrumentId, 1.25m);
    private static CashFlowSnapshot Flow(decimal amount, string source, DateTimeOffset at) => new(new Money(amount, Currency.USD), at, source);
    private static async Task<(SnapshotIds Ids, Portfolio Portfolio)> AddOwnershipAsync(TradingDbContext context)
    {
        var bot = await AddBotAsync(context, "Owner"); var portfolio = new Portfolio(PortfolioId.New(), "Portfolio", Currency.USD, new Money(1000, Currency.USD), 10, Now); portfolio.AssignTradingBot(bot.Id);
        await new PortfolioRepository(context).AddAsync(portfolio, default); context.ChangeTracker.Clear();
        return (new SnapshotIds(portfolio.Id, bot.Id, bot.ActiveConfigurationVersionId!, InstrumentId.New(), InstrumentId.New(), OrderId.New(), OrderId.New()), portfolio);
    }
    private static async Task<TradingBot> AddBotAsync(TradingDbContext context, string name)
    {
        var bot = new TradingBot(TradingBotId.New(), name, Now); var version = bot.AddConfiguration(TradingBotConfigurationVersionId.New(), new InvestmentMandate("growth", TimeSpan.FromDays(365), new UniverseDefinition(["Equity"], ["US"], [Currency.USD])), new RiskPolicy([new RiskLimit("position", 10, "percent")]), new ToolPolicy([new ToolAllowance("quotes", 2)]), new RunBudget(TimeSpan.FromMinutes(1), 100, new Money(1, Currency.USD), 2, 1, 1), new SchedulingPolicy(TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), TimeSpan.FromDays(1)), ExecutionMode.PaperTrading, new ModelConfiguration("openai", "model", 0, 100), "v1", Now); bot.ActivateConfiguration(version.Id, Now);
        await new TradingBotRepository(context).AddAsync(bot, default); context.ChangeTracker.Clear(); return bot;
    }
    private static async Task<TemporarySqliteDatabase> CreateDatabaseAsync() { var db = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(db.Context).InitializeAsync(); return db; }
    private sealed record SnapshotIds(PortfolioId Portfolio, TradingBotId Bot, TradingBotConfigurationVersionId Configuration, InstrumentId Instrument1, InstrumentId Instrument2, OrderId Order1, OrderId Order2)
    { public static SnapshotIds Create() => new(PortfolioId.Parse("01HF7YAT00S8K1M3Q5V7X9ZBC1"), TradingBotId.Parse("01HF7YAT00S8K1M3Q5V7X9ZBC2"), TradingBotConfigurationVersionId.Parse("01HF7YAT00S8K1M3Q5V7X9ZBC3"), InstrumentId.Parse("01HF7YAT00S8K1M3Q5V7X9ZBC4"), InstrumentId.Parse("01HF7YAT00S8K1M3Q5V7X9ZBC5"), OrderId.Parse("01HF7YAT00S8K1M3Q5V7X9ZBC6"), OrderId.Parse("01HF7YAT00S8K1M3Q5V7X9ZBC7")); }
}
