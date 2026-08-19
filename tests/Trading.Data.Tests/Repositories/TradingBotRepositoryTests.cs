using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;

namespace Trading.Data.Tests.Repositories;

[Category("TradingBotPersistence")]
public sealed class TradingBotRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly int[] ExpectedVersions = [1, 2];

    [Test]
    public async Task BotAndConfigurationHistoryRoundTripWithStableCanonicalHashes()
    {
        await using var database = await CreateDatabaseAsync();
        var bot = NewConfiguredBot("Long Horizon");
        var repository = new TradingBotRepository(database.Context);
        Assert.That(await repository.AddAsync(bot, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        var hashesBefore = await Hashes(database.Context, bot.Id); database.Context.ChangeTracker.Clear();
        var loaded = await repository.GetAsync(bot.Id, CancellationToken.None);
        var hashesAfter = await Hashes(database.Context, bot.Id);
        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Name, Is.EqualTo(bot.Name));
            Assert.That(loaded.Status, Is.EqualTo(TradingBotStatus.Paused));
            Assert.That(loaded.ActiveConfigurationVersionId, Is.EqualTo(bot.ActiveConfigurationVersionId));
            Assert.That(loaded.ConfigurationVersions, Has.Count.EqualTo(1));
            Assert.That(loaded.ConfigurationVersions[0].InvestmentMandate, Is.EqualTo(bot.ConfigurationVersions[0].InvestmentMandate));
            Assert.That(loaded.ConfigurationVersions[0].RiskPolicy, Is.EqualTo(bot.ConfigurationVersions[0].RiskPolicy));
            Assert.That(loaded.ConfigurationVersions[0].ToolPolicy, Is.EqualTo(bot.ConfigurationVersions[0].ToolPolicy));
            Assert.That(loaded.ConfigurationVersions[0].RunBudget, Is.EqualTo(bot.ConfigurationVersions[0].RunBudget));
            Assert.That(loaded.ConfigurationVersions[0].SchedulingPolicy, Is.EqualTo(bot.ConfigurationVersions[0].SchedulingPolicy));
            Assert.That(loaded.ConfigurationVersions[0].ModelConfiguration, Is.EqualTo(bot.ConfigurationVersions[0].ModelConfiguration));
            Assert.That(hashesAfter, Is.EqualTo(hashesBefore));
            Assert.That(hashesAfter.Single(), Does.Match("^[0-9a-f]{64}$"));
        });
    }

    [Test]
    public async Task InitialCreationResolvesActiveVersionCycleAtomically()
    {
        await using var database = await CreateDatabaseAsync();
        var bot = NewConfiguredBot("Atomic");
        Assert.That(await new TradingBotRepository(database.Context).AddAsync(bot, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        Assert.That(await ScalarString(database.Context, "SELECT active_configuration_version_id FROM trading_bots"), Is.EqualTo(bot.ActiveConfigurationVersionId!.ToString()));
        Assert.That(await ScalarLong(database.Context, "SELECT count(*) FROM trading_bot_configuration_versions"), Is.EqualTo(1));
    }

    [Test]
    public async Task DuplicateNameReturnsExplicitConflictAndRollsBackConfiguration()
    {
        await using var database = await CreateDatabaseAsync();
        var repository = new TradingBotRepository(database.Context);
        await repository.AddAsync(NewConfiguredBot("Unique"), CancellationToken.None);
        var duplicate = NewConfiguredBot("Unique");
        var result = await repository.AddAsync(duplicate, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new PersistenceWriteResult.UniquenessConflict("trading_bot_name_or_configuration_version")));
            Assert.That(ScalarLong(database.Context, "SELECT count(*) FROM trading_bots").Result, Is.EqualTo(1));
            Assert.That(ScalarLong(database.Context, "SELECT count(*) FROM trading_bot_configuration_versions").Result, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AddingAndActivatingVersionPreservesPublishedContentAndOneActiveVersion()
    {
        await using var database = await CreateDatabaseAsync(); var repository = new TradingBotRepository(database.Context);
        var bot = NewConfiguredBot("Versions"); await repository.AddAsync(bot, CancellationToken.None); database.Context.ChangeTracker.Clear();
        var loaded = (await repository.GetAsync(bot.Id, CancellationToken.None))!;
        var originalHash = (await Hashes(database.Context, bot.Id)).Single();
        var next = loaded.AddConfiguration(TradingBotConfigurationVersionId.New(), Mandate("income"), Risk(), Tools(), Budget(), Schedule(), ExecutionMode.HumanApproval, Model(), "prompt-v2", CreatedAt.AddDays(1));
        loaded.ActivateConfiguration(next.Id, CreatedAt.AddDays(1));
        Assert.That(await repository.UpdateAsync(loaded, loaded.Version, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear(); var reloaded = (await repository.GetAsync(bot.Id, CancellationToken.None))!;
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.ConfigurationVersions.Select(x => x.VersionNumber), Is.EqualTo(ExpectedVersions));
            Assert.That(reloaded.ConfigurationVersions.Count(x => x.IsActive), Is.EqualTo(1));
            Assert.That(reloaded.ActiveConfigurationVersionId, Is.EqualTo(next.Id));
            Assert.That(Hashes(database.Context, bot.Id).Result[0], Is.EqualTo(originalHash));
        });
    }

    [Test]
    public async Task StaleVersionReturnsExplicitConcurrencyConflict()
    {
        await using var database = await CreateDatabaseAsync(); var repository = new TradingBotRepository(database.Context);
        var bot = NewConfiguredBot("Concurrency"); await repository.AddAsync(bot, CancellationToken.None); database.Context.ChangeTracker.Clear();
        var first = (await repository.GetAsync(bot.Id, CancellationToken.None))!; var stale = (await repository.GetAsync(bot.Id, CancellationToken.None))!;
        first.Pause(CreatedAt.AddMinutes(1));
        Assert.That(await repository.UpdateAsync(first, 0, CancellationToken.None), Is.TypeOf<PersistenceWriteResult.Succeeded>());
        database.Context.ChangeTracker.Clear();
        Assert.That(await repository.UpdateAsync(stale, 0, CancellationToken.None), Is.EqualTo(new PersistenceWriteResult.ConcurrencyConflict(0, 1)));
    }

    [Test]
    public async Task PublishedConfigurationContentCannotBeUpdated()
    {
        await using var database = await CreateDatabaseAsync(); var bot = NewConfiguredBot("Immutable");
        await new TradingBotRepository(database.Context).AddAsync(bot, CancellationToken.None);
        Assert.That(async () => await database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE trading_bot_configuration_versions SET prompt_version = 'changed'"), Throws.TypeOf<SqliteException>());
    }

    [Test]
    public async Task RuntimeModelHasNoChangesPendingBeyondTheCommittedMigration()
    {
        await using var database = await CreateDatabaseAsync();
        Assert.That(database.Context.Database.HasPendingModelChanges(), Is.False);
    }

    private static TradingBot NewConfiguredBot(string name)
    {
        var bot = new TradingBot(TradingBotId.New(), name, CreatedAt);
        var version = bot.AddConfiguration(TradingBotConfigurationVersionId.New(), Mandate("growth"), Risk(), Tools(), Budget(), Schedule(), ExecutionMode.PaperTrading, Model(), "prompt-v1", CreatedAt);
        bot.ActivateConfiguration(version.Id, CreatedAt); return bot;
    }
    private static InvestmentMandate Mandate(string objective) => new(objective, TimeSpan.FromDays(365), new UniverseDefinition(["Equity"], ["US"], [Currency.USD]));
    private static RiskPolicy Risk() => new([new RiskLimit("position", 10m, "percent")]);
    private static ToolPolicy Tools() => new([new ToolAllowance("market-data", 4)]);
    private static RunBudget Budget() => new(TimeSpan.FromMinutes(5), 10_000, new Money(5m, Currency.USD), 8, 2, 1);
    private static SchedulingPolicy Schedule() => new(TimeSpan.FromHours(1), TimeSpan.FromMinutes(5), TimeSpan.FromDays(1));
    private static ModelConfiguration Model() => new("openai", "model", 0.2m, 2000);
    private static async Task<TemporarySqliteDatabase> CreateDatabaseAsync() { var db = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(db.Context).InitializeAsync(); return db; }
    private static async Task<List<string>> Hashes(TradingDbContext context, TradingBotId id)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT content_hash FROM trading_bot_configuration_versions WHERE trading_bot_id = $id ORDER BY version_number";
        var parameter = command.CreateParameter(); parameter.ParameterName = "$id"; parameter.Value = id.ToString(); command.Parameters.Add(parameter);
        var result = new List<string>(); await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) result.Add(reader.GetString(0)); return result;
    }
    private static async Task<long> ScalarLong(TradingDbContext context, string sql) { await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task<string?> ScalarString(TradingDbContext context, string sql) { await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; return (string?)await command.ExecuteScalarAsync(); }
}
