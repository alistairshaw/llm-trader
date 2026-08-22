using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Trading.Core.Orders;

namespace Trading.Data.Tests.Migrations;

[TestFixture, Category("Stage6Migrations"), Category("OrderPersistence"), Category("PersistenceMappings")]
internal sealed class Stage6MigrationTests
{
    private static string Hash(char value) => new(value, 64);
    private static readonly string[] Tables = ["orders", "order_transitions", "fills", "broker_reconciliations", "inbox_messages", "outbox_messages"];

    [Test]
    public async Task FreshAndCompletedStageFiveUpgradeAreEquivalentAndRetainHistory()
    {
        await using var fresh = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(fresh.Context).InitializeAsync();
        var expected = await SchemaAsync(fresh.Context);

        await using var upgraded = await TemporarySqliteDatabase.CreateAsync();
        await upgraded.Context.Database.MigrateAsync("20260820222346_RestoreGuardrailEvaluationImmutabilityTriggers");
        await SqlAsync(upgraded.Context, "INSERT INTO schema_metadata(key,value,updated_at) VALUES ('upgrade-proof','retained',42)");
        await new DatabaseInitializer(upgraded.Context).InitializeAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await SchemaAsync(upgraded.Context), Is.EqualTo(expected));
            Assert.That(await ScalarAsync<string>(upgraded.Context, "SELECT value FROM schema_metadata WHERE key='upgrade-proof'"), Is.EqualTo("retained"));
            Assert.That(await ScalarAsync<string>(upgraded.Context, "SELECT value FROM schema_metadata WHERE key='application_data_format_version'"), Is.EqualTo("6"));
            Assert.That(upgraded.Context.Database.HasPendingModelChanges(), Is.False);
        });
    }

    [Test]
    public async Task SchemaHasRequiredIdentityIndexesRestrictionsAndImmutabilityTriggers()
    {
        await using var db = await SeedAsync();
        var objects = await StringsAsync(db.Context, "SELECT type||'|'||name FROM sqlite_schema WHERE type IN ('table','index','trigger')");
        Assert.Multiple(() =>
        {
            foreach (var table in Tables) Assert.That(objects, Does.Contain("table|" + table));
            Assert.That(objects, Does.Contain("index|IX_orders_client_order_id"));
            Assert.That(objects, Does.Contain("index|IX_orders_broker_account_id_broker_order_id"));
            Assert.That(objects, Does.Contain("index|IX_fills_broker_account_id_broker_execution_id"));
            Assert.That(objects, Does.Contain("index|IX_inbox_messages_source_external_message_id"));
            Assert.That(objects, Does.Contain("trigger|fills_immutable_update"));
            Assert.That(objects, Does.Contain("trigger|order_transitions_immutable_delete"));
            Assert.That(objects, Does.Contain("trigger|orders_ownership_consistent_insert"));
        });
        foreach (var table in Tables) Assert.That(await StringsAsync(db.Context, $"SELECT on_delete FROM pragma_foreign_key_list('{table}')"), Is.All.EqualTo("RESTRICT"));
    }

    [Test]
    public async Task ExactValuesUtcAndConcurrencyRoundTrip()
    {
        await using var db = await SeedAsync();
        var order = await db.Context.Orders.SingleAsync();
        var fill = await db.Context.Fills.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(order.Quantity, Is.EqualTo("1234567890123456.12345678"));
            Assert.That(order.QuantityUnit, Is.EqualTo("shares"));
            Assert.That(order.Currency, Is.EqualTo("USD"));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Created));
            Assert.That(order.TimeInForce, Is.EqualTo(TimeInForce.Day));
            Assert.That(fill.Price, Is.EqualTo("210.125"));
            Assert.That(fill.ExecutedAt, Is.EqualTo(1735689600123));
        });
        await using var other = new TradingDbContext(new DbContextOptionsBuilder<TradingDbContext>().UseSqlite((SqliteConnection)db.Context.Database.GetDbConnection()).Options);
        var stale = await other.Orders.SingleAsync(); order.Status = OrderStatus.Submitted; order.Version = 2; await db.Context.SaveChangesAsync(); stale.Status = OrderStatus.Rejected; stale.Version = 2;
        Assert.That(async () => await other.SaveChangesAsync(), Throws.TypeOf<DbUpdateConcurrencyException>());
    }

    [Test]
    public async Task EveryCoreStatusAndTimeInForceUsesItsExactCanonicalToken()
    {
        await using var db = await SeedAsync();
        var sequence = 1;
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            await SqlAsync(db.Context, OrderSql($"status-{sequence}", $"client-status-{sequence}", $"corr-status-{sequence}", "account", status.ToString(), TimeInForce.Day.ToString()));
            Assert.That(await ScalarAsync<string>(db.Context, $"SELECT status FROM orders WHERE id='status-{sequence}'"), Is.EqualTo(status.ToString()));
            sequence++;
        }
        foreach (var timeInForce in Enum.GetValues<TimeInForce>())
        {
            await SqlAsync(db.Context, OrderSql($"tif-{sequence}", $"client-tif-{sequence}", $"corr-tif-{sequence}", "account", OrderStatus.Created.ToString(), timeInForce.ToString()));
            Assert.That(await ScalarAsync<string>(db.Context, $"SELECT time_in_force FROM orders WHERE id='tif-{sequence}'"), Is.EqualTo(timeInForce.ToString()));
            sequence++;
        }
    }

    [Test]
    public async Task DatabaseRejectsDuplicateCrossAccountInvalidAndMutableFacts()
    {
        await using var db = await SeedAsync();
        Assert.Multiple(() =>
        {
            Assert.That(async () => await SqlAsync(db.Context, OrderSql("order2", "client-1", "corr-2", "account")), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, OrderSql("order2", "client-2", "corr-2", "other-account")), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, "INSERT INTO fills VALUES ('fill2','order','account','execution-1','1','1','USD','0','USD',1,1,NULL)"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, "INSERT INTO fills VALUES ('fill2','order','account','execution-2','0','1','USD','0','USD',1,1,NULL)"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, "UPDATE fills SET price='2' WHERE id='fill'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, "UPDATE orders SET quantity='2' WHERE id='order'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, "UPDATE orders SET currency='EUR' WHERE id='order'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, "DELETE FROM orders WHERE id='order'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, OrderSql("bad-status", "bad-status", "bad-status", "account", "PendingSubmission", "Day")), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, OrderSql("bad-tif", "bad-tif", "bad-tif", "account", "Created", "GoodTilCancelled")), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, OrderSql("bad-currency", "bad-currency", "bad-currency", "account", "Created", "Day", "usd")), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await SqlAsync(db.Context, OrderSql("bad-unit", "bad-unit", "bad-unit", "account", "Created", "Day", "USD", "Shares")), Throws.TypeOf<SqliteException>());
        });
    }

    private static async Task<TemporarySqliteDatabase> SeedAsync()
    {
        var db = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(db.Context).InitializeAsync();
        await SqlAsync(db.Context, "INSERT INTO broker_connections VALUES ('connection','Simulated','Paper','Paper','secret-ref','Enabled','{}',1,1,1)");
        await SqlAsync(db.Context, "INSERT INTO broker_accounts VALUES ('account','connection','paper-account','Paper','Cash','USD','Active',NULL,'{}',1,1,1)");
        await SqlAsync(db.Context, "INSERT INTO broker_accounts VALUES ('other-account','connection','other','Other','Cash','USD','Active',NULL,'{}',1,1,1)");
        await SqlAsync(db.Context, "INSERT INTO trading_bots (id,name,status,active_configuration_version_id,requested_next_run_at,accepted_next_run_at,last_completed_run_id,created_at,updated_at,version) VALUES ('bot','Bot','Enabled',NULL,NULL,NULL,NULL,1,1,1)");
        await SqlAsync(db.Context, $"INSERT INTO trading_bot_configuration_versions VALUES ('cfg','bot',1,'{{}}','{{}}','{{}}','{{}}','{{}}','PaperTrading','{{}}','p','{Hash('a')}',1,1,NULL)");
        await SqlAsync(db.Context, "UPDATE trading_bots SET active_configuration_version_id='cfg' WHERE id='bot'");
        await SqlAsync(db.Context, "INSERT INTO instruments VALUES ('instrument','Equity','AAPL','Apple','USD','NASDAQ',8,8,'Active',1,1,1)");
        await SqlAsync(db.Context, "INSERT INTO portfolios VALUES ('portfolio','P','USD','account','bot','Active','1000','{}',1,1,1)");
        await SqlAsync(db.Context, $"INSERT INTO portfolio_decision_snapshots VALUES ('snapshot','portfolio','bot','cfg',1,'Reconciled','{{}}',1,'{{}}','{Hash('b')}',1)");
        await SqlAsync(db.Context, $"INSERT INTO bot_runs VALUES ('run','bot','cfg','snapshot','Completed',NULL,NULL,1,2,'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,'{Hash('c')}')");
        await SqlAsync(db.Context, "UPDATE trading_bots SET last_completed_run_id='run' WHERE id='bot'");
        await SqlAsync(db.Context, "INSERT INTO trade_proposals VALUES ('proposal','bot','run','portfolio','snapshot','cfg','instrument','DirectTrade','{}','rationale',NULL,'Approved',1,99,'idem',1)");
        await SqlAsync(db.Context, OrderSql("order", "client-1", "corr-1", "account"));
        await SqlAsync(db.Context, "INSERT INTO order_transitions (id,order_id,sequence_number,previous_status,new_status,reason_code,reason_detail,source,occurred_at,received_at,correlation_id) VALUES ('transition','order',1,'Created','Submitted','accepted',NULL,'Broker',1735689600123,1735689600123,'corr-1')");
        await SqlAsync(db.Context, "INSERT INTO fills (id,order_id,broker_account_id,broker_execution_id,quantity,price,currency,fee_amount,fee_currency,executed_at,received_at,raw_payload_reference) VALUES ('fill','order','account','execution-1','1.25','210.125','USD','0.01','USD',1735689600123,1735689600123,NULL)");
        db.Context.ChangeTracker.Clear(); return db;
    }
    private static string OrderSql(string id, string client, string correlation, string account,
        string status = "Created", string timeInForce = "Day", string currency = "USD", string quantityUnit = "shares") =>
        $"INSERT INTO orders (id,client_order_id,portfolio_id,broker_account_id,trade_proposal_id,capital_reservation_id,instrument_id,side,quantity,quantity_unit,currency,order_type,limit_price,time_in_force,status,broker_order_id,correlation_id,created_at,submitted_at,completed_at,version) VALUES ('{id}','{client}','portfolio','{account}','proposal',NULL,'instrument','Buy','1234567890123456.12345678','{quantityUnit}','{currency}','Market',NULL,'{timeInForce}','{status}',NULL,'{correlation}',1735689600123,NULL,NULL,1)";
    private static async Task SqlAsync(TradingDbContext context, string sql) { await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
    private static async Task<T> ScalarAsync<T>(TradingDbContext context, string sql) { await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; return (T)(await command.ExecuteScalarAsync())!; }
    private static async Task<string[]> StringsAsync(TradingDbContext context, string sql) { await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; await using var reader = await command.ExecuteReaderAsync(); var values = new List<string>(); while (await reader.ReadAsync()) values.Add(reader.GetString(0)); return [.. values]; }
    private static async Task<string> SchemaAsync(TradingDbContext context) => string.Join('\n', await StringsAsync(context, "SELECT type||'|'||name||'|'||coalesce(sql,'') FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%' AND name <> '__ef_migrations_history' ORDER BY type,name"));
}
