using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Portfolios;

namespace Trading.Data.Tests.Repositories;

[Category("PortfolioPersistence")]
public sealed class PortfolioRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly string[] LedgerOperations = ["GetAsync", "AppendAsync"];

    [Test]
    public async Task PortfolioRoundTripsAndActiveBrokerOwnershipIsUnique()
    {
        await using var database = await CreateDatabaseAsync();
        var account = await AddAccountAsync(database.Context);
        var repository = new PortfolioRepository(database.Context);
        var first = NewPortfolio("First"); first.AssociateBrokerAccount(account.Id);
        Assert.That(await repository.AddAsync(first, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear();
        var loaded = await repository.GetAsync(first.Id, default);
        Assert.Multiple(() => { Assert.That(loaded!.BrokerAccountId, Is.EqualTo(account.Id)); Assert.That(loaded.Status, Is.EqualTo(PortfolioStatus.Active)); Assert.That(loaded.CapitalAllocation, Is.EqualTo(new Money(1000.12345678m, Currency.USD))); Assert.That(loaded.CashReservePercentage, Is.EqualTo(12.5m)); });
        var second = NewPortfolio("Second"); second.AssociateBrokerAccount(account.Id);
        Assert.That(await repository.AddAsync(second, default), Is.TypeOf<PersistenceWriteResult.UniquenessConflict>());
    }

    [Test]
    public async Task PositionRetainsZeroQuantityAndAppliedSourceAcrossReload()
    {
        await using var database = await CreateDatabaseAsync();
        var portfolio = await AddPortfolioAsync(database.Context); var instrument = await AddInstrumentAsync(database.Context);
        var position = new Position(PositionId.New(), portfolio.Id, instrument.Id, "share", Currency.USD, Now);
        position.ApplyChange(2, new Money(10, Currency.USD), Money.Zero(Currency.USD), PositionChangeSource.Execution, "fill-1", Now.AddMinutes(1));
        position.ApplyChange(-2, Money.Zero(Currency.USD), new Money(3, Currency.USD), PositionChangeSource.Execution, "fill-2", Now.AddMinutes(2));
        var repository = new PositionRepository(database.Context);
        Assert.That(await repository.AddAsync(position, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear(); var loaded = await repository.GetForPortfolioInstrumentAsync(portfolio.Id, instrument.Id, default);
        Assert.Multiple(() => { Assert.That(loaded!.Quantity, Is.Zero); Assert.That(loaded.ClosedAt, Is.EqualTo(Now.AddMinutes(2))); Assert.That(loaded.RealizedProfitLoss.Amount, Is.EqualTo(3)); Assert.That(loaded.ApplyChange(1, new Money(11, Currency.USD), Money.Zero(Currency.USD), PositionChangeSource.Execution, "fill-2", Now.AddMinutes(3)), Is.False); });
    }

    [Test]
    public async Task DuplicatePortfolioInstrumentIsRejectedAndReferencedRowsCannotBeDeleted()
    {
        await using var database = await CreateDatabaseAsync(); var portfolio = await AddPortfolioAsync(database.Context); var instrument = await AddInstrumentAsync(database.Context); var repository = new PositionRepository(database.Context);
        await repository.AddAsync(new Position(PositionId.New(), portfolio.Id, instrument.Id, "share", Currency.USD, Now), default);
        Assert.That(await repository.AddAsync(new Position(PositionId.New(), portfolio.Id, instrument.Id, "share", Currency.USD, Now), default), Is.TypeOf<PersistenceWriteResult.UniquenessConflict>());
        Assert.That(async () => await database.Context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM portfolios WHERE id = {portfolio.Id.ToString()}"), Throws.TypeOf<SqliteException>());
    }

    [Test]
    public async Task LedgerAppendIsIdempotentAndCorrectionPreservesOriginal()
    {
        await using var database = await CreateDatabaseAsync(); var portfolio = await AddPortfolioAsync(database.Context); var repository = new PortfolioLedgerRepository(database.Context);
        var original = new PortfolioLedgerEntry(PortfolioLedgerEntryId.New(), portfolio.Id, PortfolioLedgerEntryType.Deposit, new Money(25, Currency.USD), null, null, Now, LedgerSourceType.BrokerEvent, "event-1");
        Assert.That(await repository.AppendAsync(original, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var duplicate = new PortfolioLedgerEntry(PortfolioLedgerEntryId.New(), portfolio.Id, PortfolioLedgerEntryType.Deposit, new Money(25, Currency.USD), null, null, Now, LedgerSourceType.BrokerEvent, "event-1");
        Assert.That(await repository.AppendAsync(duplicate, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var correction = new PortfolioLedgerEntry(PortfolioLedgerEntryId.New(), portfolio.Id, PortfolioLedgerEntryType.ManualCorrection, new Money(-25, Currency.USD), null, null, Now.AddMinutes(1), LedgerSourceType.AuditedAdjustment, "correction-1", Now.AddMinutes(1), original.Id);
        Assert.That(await repository.AppendAsync(correction, default), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await database.Context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM portfolio_ledger_entries").SingleAsync(), Is.EqualTo(2));
        Assert.Multiple(async () => { Assert.That(await repository.GetAsync(original.Id, default), Is.Not.Null); Assert.That((await repository.GetAsync(correction.Id, default))!.ReversesEntryId, Is.EqualTo(original.Id)); });
    }

    [Test]
    public async Task LedgerRowsAreRestrictedFromUpdateAndDeleteByContextPolicy()
    {
        await using var database = await CreateDatabaseAsync(); var portfolio = await AddPortfolioAsync(database.Context); var repository = new PortfolioLedgerRepository(database.Context);
        var entry = new PortfolioLedgerEntry(PortfolioLedgerEntryId.New(), portfolio.Id, PortfolioLedgerEntryType.Fee, new Money(-1, Currency.USD), null, null, Now, LedgerSourceType.BrokerEvent, "fee-1"); await repository.AppendAsync(entry, default);
        Assert.That(typeof(IPortfolioLedgerRepository).GetMethods().Select(x => x.Name), Is.EquivalentTo(LedgerOperations));
        Assert.That(async () => await database.Context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM portfolios WHERE id = {portfolio.Id.ToString()}"), Throws.TypeOf<SqliteException>());
    }

    private static Portfolio NewPortfolio(string name) => new(PortfolioId.New(), name, Currency.USD, new Money(1000.12345678m, Currency.USD), 12.5m, Now);
    private static async Task<TemporarySqliteDatabase> CreateDatabaseAsync() { var db = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(db.Context).InitializeAsync(); return db; }
    private static async Task<Portfolio> AddPortfolioAsync(TradingDbContext context) { var value = NewPortfolio(Guid.NewGuid().ToString("N")); await new PortfolioRepository(context).AddAsync(value, default); return value; }
    private static async Task<BrokerAccount> AddAccountAsync(TradingDbContext context) { var connection = new BrokerConnection(BrokerConnectionId.New(), "sim", "Sim", BrokerEnvironment.Paper, "ref://sim", [], Now); await new BrokerConnectionRepository(context).AddAsync(connection, default); var account = new BrokerAccount(BrokerAccountId.New(), connection.Id, Guid.NewGuid().ToString("N"), "Account", "Cash", Currency.USD); await new BrokerAccountRepository(context).AddAsync(account, default); return account; }
    private static async Task<Instrument> AddInstrumentAsync(TradingDbContext context) { var value = new Instrument(InstrumentId.New(), InstrumentType.Equity, Guid.NewGuid().ToString("N"), "Instrument", Currency.USD, "NYSE", 4, 8, Now); await new InstrumentRepository(context).AddAsync(value, default); return value; }
}
