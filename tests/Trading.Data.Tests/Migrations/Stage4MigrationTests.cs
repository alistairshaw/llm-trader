using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Trading.Data.Tests.Migrations;

[TestFixture]
[Category("Stage4Migrations")]
[Category("ResearchPersistence")]
internal sealed class Stage4MigrationTests
{
    private const string Bot = "01EEEEEEEEEEEEEEEEEEEEEEEE";
    private const string Request = "01RQEEEEEEEEEEEEEEEEEEEEEE";
    private const string Run = "01RNEEEEEEEEEEEEEEEEEEEEEE";
    private const string Report = "01RPEEEEEEEEEEEEEEEEEEEEEE";
    private static readonly string[] ResearchTables = ["research_requests", "research_subscriptions", "research_runs", "research_tool_invocations", "research_reports", "research_report_sources"];
    private static readonly string[] UniqueIndexes = ["IX_research_subscriptions_research_request_id_trading_bot_id", "IX_research_runs_research_request_id_attempt_number", "IX_research_tool_invocations_research_run_id_sequence_number", "IX_research_reports_report_series_id_version_number", "IX_research_reports_report_series_id_content_hash", "IX_research_report_sources_research_report_id_source_sequence"];
    private static readonly string[] ImmutabilityTriggers = ["research_reports_immutable_content", "research_reports_no_delete", "research_report_sources_immutable", "research_report_sources_no_delete", "research_tool_invocations_terminal_immutable", "research_tool_invocations_terminal_no_delete"];
    private static string Hash(char value) => new(value, 64);

    [Test]
    public async Task FreshAndCompletedStageThreeUpgradeProduceTheSameSchemaAndRetainHistory()
    {
        await using var fresh = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(fresh.Context).InitializeAsync();
        var freshSchema = await SchemaAsync(fresh.Context);

        await using var upgraded = await TemporarySqliteDatabase.CreateAsync();
        await upgraded.Context.Database.MigrateAsync("20260819223000_AddBotRunInputRenderingHash");
        await SeedStageThreeHistoryAsync(upgraded.Context);
        var before = await ScalarAsync<string>(upgraded.Context, "SELECT id||'|'||content_hash FROM portfolio_decision_snapshots");
        await new DatabaseInitializer(upgraded.Context).InitializeAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await SchemaAsync(upgraded.Context), Is.EqualTo(freshSchema));
            Assert.That(await ScalarAsync<string>(upgraded.Context, "SELECT id||'|'||content_hash FROM portfolio_decision_snapshots"), Is.EqualTo(before));
            Assert.That(await ScalarAsync<long>(upgraded.Context, "SELECT COUNT(*) FROM bot_runs"), Is.EqualTo(1));
            Assert.That(await ScalarAsync<long>(upgraded.Context, "SELECT COUNT(*) FROM __ef_migrations_history"), Is.EqualTo(17));
            Assert.That(await ScalarAsync<string>(upgraded.Context, "SELECT value FROM schema_metadata WHERE key='application_data_format_version'"), Is.EqualTo("7"));
        });
    }

    [Test]
    public async Task ResearchSchemaHasRequiredColumnsIndexesChecksRestrictionsAndNoDrift()
    {
        await using var db = await SeedResearchAsync();
        var tables = await SchemaObjectsAsync(db.Context, "table");
        var indexes = await SchemaObjectsAsync(db.Context, "index");
        var triggers = await SchemaObjectsAsync(db.Context, "trigger");
        Assert.Multiple(() =>
        {
            foreach (var table in ResearchTables)
                Assert.That(tables.Keys, Does.Contain(table));
            foreach (var index in UniqueIndexes)
                Assert.That(indexes.Keys, Does.Contain(index));
            foreach (var trigger in ImmutabilityTriggers)
                Assert.That(triggers.Keys, Does.Contain(trigger));
            Assert.That(db.Context.Database.HasPendingModelChanges(), Is.False);
        });
        foreach (var table in ResearchTables)
            Assert.That(await StringsAsync(db.Context, $"SELECT on_delete FROM pragma_foreign_key_list('{table}')"), Is.All.EqualTo("RESTRICT"));
    }

    [Test]
    public async Task UniqueResearchFactsAndCanonicalHashesAreEnforcedBySqlite()
    {
        await using var db = await SeedResearchAsync();
        Assert.Multiple(() =>
        {
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO research_subscriptions VALUES ('s2','{Request}','{Bot}',1,'Pending',NULL)"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO research_runs VALUES ('run2','{Request}',1,'Pending','{{}}','p','t','r',1,NULL,NULL,'{{}}',1)"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO research_reports VALUES ('report2','series',1,'{Request}','{Run}','Instrument','US:AAPL','q','Shared',1,1,NULL,'Published',NULL,'v1','{{}}',NULL,'{Hash('b')}','{{}}')"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO research_reports VALUES ('report3','series',2,'{Request}','{Run}','Instrument','US:AAPL','q','Shared',1,1,NULL,'Published','{Report}','v1','{{}}',NULL,'{Hash('a')}','{{}}')"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO research_report_sources VALUES ('source2','{Report}',1,'Filing',NULL,'10-k','Duplicate',NULL,NULL,1,'{Hash('c')}','{{}}')"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"INSERT INTO research_report_sources VALUES ('source3','{Report}',2,'Filing',NULL,'10-q','Bad',NULL,NULL,1,'ABC','{{}}')"), Throws.TypeOf<SqliteException>());
        });
    }

    [Test]
    public async Task PublishedReportsSourcesAndCompletedToolAuditAreImmutableAndDeleteRestricted()
    {
        await using var db = await SeedResearchAsync();
        Assert.Multiple(() =>
        {
            Assert.That(async () => await ExecuteAsync(db.Context, $"UPDATE research_reports SET content_json='{{\"changed\":true}}' WHERE id='{Report}'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, "UPDATE research_report_sources SET title='changed' WHERE id='source1'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, "UPDATE research_tool_invocations SET result_json='{}' WHERE id='tool1'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"DELETE FROM research_reports WHERE id='{Report}'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, "DELETE FROM research_report_sources WHERE id='source1'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, "DELETE FROM research_tool_invocations WHERE id='tool1'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(db.Context, $"DELETE FROM research_requests WHERE id='{Request}'"), Throws.TypeOf<SqliteException>());
        });
    }

    [Test]
    public async Task MutableResearchRequestUsesOptimisticConcurrency()
    {
        await using var db = await SeedResearchAsync();
        var first = await db.Context.ResearchRequests.SingleAsync(x => x.Id == Request);
        await using var other = new TradingDbContext(db.Context.Database.GetDbConnection() is SqliteConnection connection
            ? new DbContextOptionsBuilder<TradingDbContext>().UseSqlite(connection).Options
            : throw new InvalidOperationException());
        var stale = await other.ResearchRequests.SingleAsync(x => x.Id == Request);
        first.Status = "Running"; first.Version = 2; await db.Context.SaveChangesAsync();
        stale.Status = "Failed"; stale.Version = 2;
        Assert.That(async () => await other.SaveChangesAsync(), Throws.TypeOf<DbUpdateConcurrencyException>());
    }

    private static async Task<TemporarySqliteDatabase> SeedResearchAsync()
    {
        var db = await TemporarySqliteDatabase.CreateAsync(); await new DatabaseInitializer(db.Context).InitializeAsync();
        await ExecuteAsync(db.Context, $"INSERT INTO trading_bots (id,name,status,active_configuration_version_id,requested_next_run_at,accepted_next_run_at,last_completed_run_id,created_at,updated_at,version) VALUES ('{Bot}','Bot','Enabled',NULL,NULL,NULL,NULL,1,1,1)");
        await ExecuteAsync(db.Context, $"INSERT INTO research_requests VALUES ('{Request}','Instrument','US:AAPL','q','key',1,'Completed','Shared','{Bot}','{{\"schemaVersion\":1}}','{{\"schemaVersion\":1}}',1,2,NULL,1,1)");
        await ExecuteAsync(db.Context, $"INSERT INTO research_subscriptions VALUES ('s1','{Request}','{Bot}',1,'Delivered',2)");
        await ExecuteAsync(db.Context, $"INSERT INTO research_runs VALUES ('{Run}','{Request}',1,'Completed','{{\"schemaVersion\":1}}','p','t','r',1,2,NULL,'{{\"schemaVersion\":1}}',1)");
        await ExecuteAsync(db.Context, $"INSERT INTO research_tool_invocations VALUES ('tool1','{Run}',1,'FinishResearch',1,'{{}}','Succeeded',1,2,'{{}}',NULL,NULL,NULL,'{{}}')");
        await ExecuteAsync(db.Context, $"INSERT INTO research_reports VALUES ('{Report}','series',1,'{Request}','{Run}','Instrument','US:AAPL','q','Shared',1,2,NULL,'Published',NULL,'v1','{{\"schemaVersion\":1}}',NULL,'{Hash('a')}','{{\"schemaVersion\":1}}')");
        await ExecuteAsync(db.Context, $"UPDATE research_requests SET result_report_id='{Report}' WHERE id='{Request}'");
        await ExecuteAsync(db.Context, $"INSERT INTO research_report_sources VALUES ('source1','{Report}',1,'Filing',NULL,'10-k','Annual',NULL,NULL,1,'{Hash('b')}','{{\"schemaVersion\":1}}')");
        db.Context.ChangeTracker.Clear(); return db;
    }

    private static async Task SeedStageThreeHistoryAsync(TradingDbContext context)
    {
        await ExecuteAsync(context, $"INSERT INTO trading_bots (id,name,status,active_configuration_version_id,requested_next_run_at,accepted_next_run_at,last_completed_run_id,created_at,updated_at,version) VALUES ('{Bot}','Bot','Enabled',NULL,NULL,NULL,NULL,1,1,1)");
        await ExecuteAsync(context, $"INSERT INTO trading_bot_configuration_versions VALUES ('cfg','{Bot}',1,'{{}}','{{}}','{{}}','{{}}','{{}}','PaperTrading','{{}}','p','{Hash('c')}',1,1,NULL)");
        await ExecuteAsync(context, $"UPDATE trading_bots SET active_configuration_version_id='cfg' WHERE id='{Bot}'");
        await ExecuteAsync(context, $"INSERT INTO portfolios VALUES ('portfolio','P','USD',NULL,'{Bot}','Active','1000.00000000','{{}}',1,1,1)");
        await ExecuteAsync(context, $"INSERT INTO portfolio_decision_snapshots VALUES ('snapshot','portfolio','{Bot}','cfg',1,'Reconciled','{{}}',1,'{{}}','{Hash('d')}',1)");
        await ExecuteAsync(context, $"INSERT INTO bot_runs VALUES ('botrun','{Bot}','cfg','snapshot','Completed',NULL,NULL,1,2,'Success','done',NULL,NULL,NULL,NULL,'{{}}',1,'{{}}','v1',1,'{Hash('e')}')");
        await ExecuteAsync(context, $"UPDATE trading_bots SET last_completed_run_id='botrun' WHERE id='{Bot}'");
    }

    private static async Task<string> SchemaAsync(TradingDbContext context) => string.Join('\n', await StringsAsync(context, "SELECT type||'|'||name||'|'||coalesce(sql,'') FROM sqlite_schema WHERE name NOT LIKE 'sqlite_%' AND name <> '__ef_migrations_history' ORDER BY type,name"));
    private static async Task<Dictionary<string, string>> SchemaObjectsAsync(TradingDbContext context, string type) { var result = new Dictionary<string, string>(); await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT name,sql FROM sqlite_schema WHERE type=$type AND sql IS NOT NULL"; command.Parameters.Add(new SqliteParameter("$type", type)); await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) result.Add(reader.GetString(0), reader.GetString(1)); return result; }
    private static async Task ExecuteAsync(TradingDbContext context, string sql) { await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
    private static async Task<T> ScalarAsync<T>(TradingDbContext context, string sql) { await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; return (T)(await command.ExecuteScalarAsync())!; }
    private static async Task<string[]> StringsAsync(TradingDbContext context, string sql) { await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; await using var reader = await command.ExecuteReaderAsync(); var values = new List<string>(); while (await reader.ReadAsync()) values.Add(reader.GetString(0)); return [.. values]; }
}
