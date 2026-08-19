using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Brokers;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;

namespace Trading.Data.Tests.Repositories;

[Category("BrokerInstrumentPersistence")]
public sealed class BrokerInstrumentRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 19, 15, 0, 0, TimeSpan.Zero);
    private static readonly string[] ConnectionCapabilities = ["market-data", "orders"];
    private static readonly string[] AccountCapabilities = ["fractional", "shorting"];

    [Test]
    public async Task BrokerConnectionRoundTripsAllStateWithoutCredentialMaterial()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connection = new BrokerConnection(BrokerConnectionId.New(), "alpaca", "Paper account", BrokerEnvironment.Paper,
            "vault://brokers/alpaca/paper", ["market-data", "orders"], CreatedAt);
        connection.Enable();
        var repository = new BrokerConnectionRepository(database.Context);

        Assert.That(await repository.AddAsync(connection, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear();
        var loaded = await repository.GetAsync(connection.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Environment, Is.EqualTo(BrokerEnvironment.Paper));
            Assert.That(loaded.Status, Is.EqualTo(BrokerConnectionStatus.Enabled));
            Assert.That(loaded.Capabilities, Is.EqualTo(ConnectionCapabilities));
            Assert.That(loaded.CredentialReference, Is.EqualTo("vault://brokers/alpaca/paper"));
            Assert.That(loaded.CreatedAt, Is.EqualTo(CreatedAt));
            Assert.That(loaded.Version, Is.Zero);
        });
        await using var command = database.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CAST(group_concat(broker_type || display_name || environment || credential_reference || status || capabilities_json) AS TEXT) FROM broker_connections";
        var stored = (string?)await command.ExecuteScalarAsync();
        Assert.That(stored, Does.Not.Contain("api-key-secret"));
    }

    [Test]
    public async Task PaperAndLiveConnectionsRemainDistinctAfterReload()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var repository = new BrokerConnectionRepository(database.Context);
        var paper = new BrokerConnection(BrokerConnectionId.New(), "sim", "Paper", BrokerEnvironment.Paper, "ref://paper", [], CreatedAt);
        var live = new BrokerConnection(BrokerConnectionId.New(), "sim", "Live", BrokerEnvironment.Live, "ref://live", [], CreatedAt);
        await repository.AddAsync(paper, CancellationToken.None); await repository.AddAsync(live, CancellationToken.None);
        Assert.Multiple(async () =>
        {
            Assert.That((await repository.GetAsync(paper.Id, CancellationToken.None))!.Environment, Is.EqualTo(BrokerEnvironment.Paper));
            Assert.That((await repository.GetAsync(live.Id, CancellationToken.None))!.Environment, Is.EqualTo(BrokerEnvironment.Live));
        });
    }

    [Test]
    public async Task BrokerAccountRoundTripsReconciliationCapabilitiesAndLifecycle()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connection = await AddConnectionAsync(database.Context);
        var account = new BrokerAccount(BrokerAccountId.New(), connection.Id, "external-1", "Primary", "Margin", Currency.USD,
            ["fractional", "shorting"], CreatedAt);
        account.Reconcile(CreatedAt.AddMinutes(5)); account.Restrict();
        var repository = new BrokerAccountRepository(database.Context);
        Assert.That(await repository.AddAsync(account, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear();
        var loaded = await repository.GetAsync(account.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.BrokerConnectionId, Is.EqualTo(connection.Id));
            Assert.That(loaded.Status, Is.EqualTo(BrokerAccountStatus.Restricted));
            Assert.That(loaded.LastReconciledAt, Is.EqualTo(CreatedAt.AddMinutes(5)));
            Assert.That(loaded.Capabilities, Is.EqualTo(AccountCapabilities));
            Assert.That(loaded.BaseCurrency, Is.EqualTo(Currency.USD));
        });
    }

    [Test]
    public async Task DuplicateExternalAccountIdentityReturnsPurposeBuiltConflict()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connection = await AddConnectionAsync(database.Context);
        var repository = new BrokerAccountRepository(database.Context);
        await repository.AddAsync(new BrokerAccount(BrokerAccountId.New(), connection.Id, "same", "One", "Cash", Currency.USD), CancellationToken.None);
        var result = await repository.AddAsync(new BrokerAccount(BrokerAccountId.New(), connection.Id, "same", "Two", "Cash", Currency.USD), CancellationToken.None);
        Assert.That(result, Is.EqualTo(new PersistenceWriteResult.UniquenessConflict("broker_account_external_identity")));
    }

    [Test]
    public async Task InstrumentRoundTripsPrecisionLifecycleAndMappings()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connection = await AddConnectionAsync(database.Context);
        var instrument = new Instrument(InstrumentId.New(), InstrumentType.Equity, "MSFT", "Microsoft", Currency.USD, "NASDAQ", 4, 6, CreatedAt);
        instrument.AddBrokerMapping(InstrumentBrokerMappingId.New(), connection.Id, "us0378331005", "MSFT", "NASDAQ", CreatedAt, CreatedAt.AddYears(1));
        instrument.Deactivate();
        var repository = new InstrumentRepository(database.Context);
        Assert.That(await repository.AddAsync(instrument, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear();
        var loaded = await repository.GetAsync(instrument.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Status, Is.EqualTo(InstrumentStatus.Inactive));
            Assert.That(loaded.PricePrecision, Is.EqualTo(4));
            Assert.That(loaded.QuantityPrecision, Is.EqualTo(6));
            Assert.That(loaded.BrokerMappings, Has.Count.EqualTo(1));
            Assert.That(loaded.BrokerMappings[0].ExternalInstrumentId, Is.EqualTo("us0378331005"));
            Assert.That(loaded.BrokerMappings[0].EffectiveTo, Is.EqualTo(CreatedAt.AddYears(1)));
        });
    }

    [Test]
    public async Task OverlappingExternalMappingIntervalsReturnPurposeBuiltConflict()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connection = await AddConnectionAsync(database.Context);
        var repository = new InstrumentRepository(database.Context);
        var first = NewMappedInstrument(connection.Id, "external", CreatedAt, CreatedAt.AddDays(10));
        var second = NewMappedInstrument(connection.Id, "external", CreatedAt.AddDays(9), null);
        Assert.That(await repository.AddAsync(first, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var result = await repository.AddAsync(second, CancellationToken.None);
        Assert.That(result, Is.EqualTo(new PersistenceWriteResult.UniquenessConflict("instrument_mapping_effective_interval")));
    }

    [Test]
    public async Task StaleAggregateVersionReturnsConcurrencyConflict()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var repository = new BrokerConnectionRepository(database.Context);
        var original = new BrokerConnection(BrokerConnectionId.New(), "sim", "Connection", BrokerEnvironment.Paper, "ref://credential", [], CreatedAt);
        await repository.AddAsync(original, CancellationToken.None); database.Context.ChangeTracker.Clear();
        var first = (await repository.GetAsync(original.Id, CancellationToken.None))!;
        var stale = (await repository.GetAsync(original.Id, CancellationToken.None))!;
        first.Enable(); stale.MarkDisconnected();
        Assert.That(await repository.UpdateAsync(first, first.Version, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear();
        var conflict = await repository.UpdateAsync(stale, stale.Version, CancellationToken.None);
        Assert.That(conflict, Is.EqualTo(new PersistenceWriteResult.ConcurrencyConflict(0, 1)));
    }

    [Test]
    public async Task ReferencedBrokerAndInstrumentRowsCannotBeDeleted()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var connection = await AddConnectionAsync(database.Context);
        var instrument = NewMappedInstrument(connection.Id, "external", CreatedAt, null);
        await new InstrumentRepository(database.Context).AddAsync(instrument, CancellationToken.None);
        Assert.That(async () => await database.Context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM broker_connections WHERE id = {connection.Id.ToString()}"), Throws.TypeOf<SqliteException>());
        Assert.That(async () => await database.Context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM instruments WHERE id = {instrument.Id.ToString()}"), Throws.TypeOf<SqliteException>());
    }

    private static async Task<TemporarySqliteDatabase> CreateMigratedDatabaseAsync()
    {
        var database = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(database.Context).InitializeAsync();
        return database;
    }

    private static async Task<BrokerConnection> AddConnectionAsync(TradingDbContext context)
    {
        var connection = new BrokerConnection(BrokerConnectionId.New(), "sim", "Connection", BrokerEnvironment.Paper, "ref://credential", ["orders"], CreatedAt);
        await new BrokerConnectionRepository(context).AddAsync(connection, CancellationToken.None);
        return connection;
    }

    private static Instrument NewMappedInstrument(BrokerConnectionId connectionId, string externalId, DateTimeOffset from, DateTimeOffset? to)
    {
        var instrument = new Instrument(InstrumentId.New(), InstrumentType.Equity, Guid.NewGuid().ToString("N"), "Instrument", Currency.USD, "NYSE", 4, 4, CreatedAt);
        instrument.AddBrokerMapping(InstrumentBrokerMappingId.New(), connectionId, externalId, "SYM", "NYSE", from, to);
        return instrument;
    }
}
