using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUnit.Framework;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;

namespace Trading.Data.Tests.Converters;

[TestFixture]
[Category("Converters")]
internal sealed class CanonicalPersistenceConverterTests
{
    private static readonly Type[] IdentifierTypes =
    [
        typeof(TradingBotId), typeof(TradingBotConfigurationVersionId), typeof(BotRunId), typeof(BotRunTriggerId),
        typeof(ToolInvocationId), typeof(PortfolioId), typeof(PositionId), typeof(PortfolioDecisionSnapshotId),
        typeof(PortfolioLedgerEntryId), typeof(BrokerConnectionId), typeof(BrokerAccountId), typeof(InstrumentId),
        typeof(InstrumentBrokerMappingId), typeof(ResearchRequestId), typeof(ResearchSubscriptionId),
        typeof(ResearchReportId), typeof(HypothesisId), typeof(HypothesisVersionId), typeof(TradeProposalId),
        typeof(GuardrailEvaluationId), typeof(ProposalApprovalId), typeof(CapitalReservationId), typeof(OrderId),
        typeof(OrderTransitionId), typeof(FillId),
    ];

    [TestCaseSource(nameof(IdentifierTypes))]
    public async Task EveryIdentifierRoundTripsAsCanonicalUlidText(Type identifierType)
    {
        var value = identifierType.GetMethod("New")!.Invoke(null, null)!;
        var parse = identifierType.GetMethod("Parse")!.CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(string), identifierType));
        var factory = typeof(CanonicalPersistenceConverters).GetMethod(nameof(CanonicalPersistenceConverters.Identifier))!
            .MakeGenericMethod(identifierType);
        var converter = (ValueConverter)factory.Invoke(null, [parse])!;

        var providerValue = (string)converter.ConvertToProvider(value)!;
        var stored = await RoundTripSqliteAsync(providerValue).ConfigureAwait(false);
        var result = converter.ConvertFromProvider(stored);

        Assert.Multiple(() =>
        {
            Assert.That(providerValue, Has.Length.EqualTo(26));
            Assert.That(providerValue, Is.EqualTo(providerValue.ToUpperInvariant()));
            Assert.That(result, Is.EqualTo(value));
        });
    }

    [Test]
    public void IdentifierProviderTextMustBeCanonical()
    {
        var converter = CanonicalPersistenceConverters.Identifier(PortfolioId.Parse);
        var lowercase = PortfolioId.New().ToString().ToLowerInvariant();
        Assert.That(() => converter.ConvertFromProvider(lowercase), Throws.TypeOf<FormatException>());
    }

    [TestCase("9999999999999999.99999999")]
    [TestCase("-9999999999999999.99999999")]
    [TestCase("0")]
    [TestCase("0.00000001")]
    public async Task BoundaryDecimalsRoundTripExactly(string text)
    {
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        var providerValue = (string)CanonicalPersistenceConverters.ExactDecimal.ConvertToProvider(value)!;
        var stored = await RoundTripSqliteAsync(providerValue).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.EqualTo(text));
            Assert.That(CanonicalPersistenceConverters.ExactDecimal.ConvertFromProvider(stored), Is.EqualTo(value));
        });
    }

    [TestCase("10000000000000000.00000001")]
    [TestCase("1.000000001")]
    public async Task UnsupportedDecimalIsRejectedBeforeSqlExecution(string text)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync().ConfigureAwait(false);
        await ExecuteAsync(connection, "CREATE TABLE probe (value TEXT NOT NULL)").ConfigureAwait(false);
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        Assert.That(() => CanonicalPersistenceConverters.ExactDecimal.ConvertToProvider(value), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM probe").ConfigureAwait(false), Is.Zero);
    }

    [TestCase("1.0")]
    [TestCase("01")]
    [TestCase("-0")]
    [TestCase("1E+1")]
    public void NonCanonicalDecimalProviderTextIsRejected(string text) =>
        Assert.That(() => CanonicalPersistenceConverters.ExactDecimal.ConvertFromProvider(text), Throws.Exception);

    [Test]
    public async Task UtcTimestampsRoundTripAtMillisecondPrecisionAndOrder()
    {
        var first = new DateTimeOffset(2026, 8, 19, 12, 0, 0, 123, TimeSpan.Zero).AddTicks(4567);
        var second = first.AddMilliseconds(1);
        var firstProvider = (long)CanonicalPersistenceConverters.UtcTimestamp.ConvertToProvider(first)!;
        var secondProvider = (long)CanonicalPersistenceConverters.UtcTimestamp.ConvertToProvider(second)!;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync().ConfigureAwait(false);
        await ExecuteAsync(connection, "CREATE TABLE probe (value INTEGER NOT NULL)").ConfigureAwait(false);
        await InsertAsync(connection, firstProvider).ConfigureAwait(false);
        await InsertAsync(connection, secondProvider).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(secondProvider, Is.GreaterThan(firstProvider));
            Assert.That(CanonicalPersistenceConverters.UtcTimestamp.ConvertFromProvider(firstProvider),
                Is.EqualTo(new DateTimeOffset(2026, 8, 19, 12, 0, 0, 123, TimeSpan.Zero)));
        });
        Assert.That(await ScalarAsync<long>(connection, "SELECT value FROM probe ORDER BY value LIMIT 1").ConfigureAwait(false),
            Is.EqualTo(firstProvider));
    }

    [Test]
    public void NonUtcTimestampIsRejected() => Assert.That(
        () => CanonicalPersistenceConverters.UtcTimestamp.ConvertToProvider(
            new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.FromHours(1))),
        Throws.TypeOf<ArgumentException>());

    [Test]
    public async Task EnumerationUsesConstrainedCanonicalText()
    {
        var converter = CanonicalPersistenceConverters.Enumeration<ExecutionMode>();
        var stored = await RoundTripSqliteAsync((string)converter.ConvertToProvider(ExecutionMode.PaperTrading)!).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.EqualTo("PaperTrading"));
            Assert.That(converter.ConvertFromProvider(stored), Is.EqualTo(ExecutionMode.PaperTrading));
            Assert.That(() => converter.ConvertFromProvider("papertrading"), Throws.TypeOf<ArgumentException>());
            Assert.That(() => converter.ConvertToProvider((ExecutionMode)999), Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public async Task EveryStageTwoFinancialValueRoundTripsThroughCanonicalJson()
    {
        object[] values =
        [
            new Currency("USD"), new Money(12.5m, Currency.USD), new Price(210.125m, Currency.USD),
            new Quantity(3.25m, "share"), new Percentage(7.5m),
        ];

        foreach (var value in values)
        {
            var factory = typeof(CanonicalPersistenceConverters).GetMethod(nameof(CanonicalPersistenceConverters.CanonicalJson))!
                .MakeGenericMethod(value.GetType());
            var converter = (ValueConverter)factory.Invoke(null, [1])!;
            var stored = await RoundTripSqliteAsync((string)converter.ConvertToProvider(value)!).ConfigureAwait(false);
            Assert.That(converter.ConvertFromProvider(stored), Is.EqualTo(value));
        }
    }

    [Test]
    public async Task CanonicalJsonIsByteStableAndHasLowercaseSha256()
    {
        var first = CanonicalJsonSerializer.Serialize(3, new Dictionary<string, object> { ["z"] = 2, ["a"] = new { b = 1, a = 0 } });
        var second = CanonicalJsonSerializer.Serialize(3, new Dictionary<string, object> { ["a"] = new { a = 0, b = 1 }, ["z"] = 2 });
        var stored = await RoundTripSqliteAsync(first).ConfigureAwait(false);
        var hash = CanonicalJsonSerializer.Sha256(stored);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(hash, Has.Length.EqualTo(64));
            Assert.That(hash, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(hash, Is.EqualTo(CanonicalJsonSerializer.Sha256(second)));
            Assert.That(first, Does.StartWith("{\"content\":"));
            Assert.That(first, Does.Contain("\"schemaVersion\":3"));
        });
    }

    [Test]
    public void CanonicalJsonRejectsWrongSchemaVersionAndNonCanonicalInput()
    {
        var converter = CanonicalPersistenceConverters.CanonicalJson<Currency>(2);
        var canonical = (string)converter.ConvertToProvider(Currency.USD)!;
        Assert.Multiple(() =>
        {
            Assert.That(() => CanonicalJsonSerializer.Deserialize<Currency>(1, canonical), Throws.TypeOf<System.Text.Json.JsonException>());
            Assert.That(() => CanonicalJsonSerializer.Deserialize<Currency>(2, "{ \"schemaVersion\": 2, \"content\": {\"code\":\"USD\"}}"),
                Throws.TypeOf<System.Text.Json.JsonException>());
            Assert.That(() => CanonicalJsonSerializer.Sha256("{ \"schemaVersion\":2,\"content\":{}}"),
                Throws.TypeOf<System.Text.Json.JsonException>());
        });
    }

    [Test]
    public void ImmutableComparerUsesValueEqualityAndReturnsSameSnapshot()
    {
        var comparer = CanonicalPersistenceConverters.Immutable<Money>();
        var value = new Money(10m, Currency.USD);
        Assert.Multiple(() =>
        {
            Assert.That(comparer.Equals(value, new Money(10m, Currency.USD)), Is.True);
            Assert.That(comparer.Snapshot(value), Is.SameAs(value));
        });
    }

    private static async Task<string> RoundTripSqliteAsync(string value)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync().ConfigureAwait(false);
        await ExecuteAsync(connection, "CREATE TABLE probe (value TEXT NOT NULL)").ConfigureAwait(false);
        await InsertAsync(connection, value).ConfigureAwait(false);
        return await ScalarAsync<string>(connection, "SELECT value FROM probe").ConfigureAwait(false);
    }

    private static async Task InsertAsync(SqliteConnection connection, object value)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO probe(value) VALUES ($value)";
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<T> ScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }
}
