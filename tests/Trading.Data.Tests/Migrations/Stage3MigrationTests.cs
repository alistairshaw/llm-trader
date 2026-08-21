using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Trading.TestInfrastructure;

namespace Trading.Data.Tests.Migrations;

[TestFixture]
[Category("Stage3Migrations")]
internal sealed class Stage3MigrationTests
{
    private static readonly string[] StageTwoTables =
    [
        "broker_connections", "broker_accounts", "instruments", "instrument_broker_mappings",
        "trading_bots", "trading_bot_configuration_versions", "portfolios", "positions",
        "position_applied_fills", "portfolio_ledger_entries", "portfolio_decision_snapshots"
    ];

    private const string BotId = "01EEEEEEEEEEEEEEEEEEEEEEEE";
    private const string ConfigurationId = "01FFFFFFFFFFFFFFFFFFFFFFFF";
    private const string SnapshotId = "01MMMMMMMMMMMMMMMMMMMMMMMM";
    private static readonly string[] TriggerColumns = ["id", "trading_bot_id", "trigger_type", "reason", "source_type", "source_id", "occurred_at", "consumed_by_run_id", "created_at"];
    private static readonly string[] RunColumns =
    [
        "id", "trading_bot_id", "configuration_version_id", "portfolio_snapshot_id", "status", "lease_owner", "lease_expires_at",
        "started_at", "completed_at", "finish_status", "finish_summary", "requested_next_run_at", "requested_wake_reason",
        "accepted_next_run_at", "terminal_reason", "usage_json", "model_transcript_schema_version", "model_transcript_json",
        "input_rendering_version", "version", "input_rendering_hash"
    ];
    private static readonly string[] ToolColumns =
    [
        "id", "bot_run_id", "sequence_number", "tool_name", "tool_schema_version", "arguments_json", "status", "started_at",
        "completed_at", "result_json", "result_artifact_id", "error_code", "error_detail", "usage_json"
    ];

    [Test]
    public async Task FreshCreationAndReapplicationProduceTheCompleteStageThreeSchema()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var initializer = new DatabaseInitializer(database.Context);

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await ScalarAsync<long>(database.Context, "SELECT COUNT(*) FROM __ef_migrations_history"), Is.EqualTo(7));
            Assert.That(await ScalarAsync<string>(database.Context, "SELECT value FROM schema_metadata WHERE key='application_data_format_version'"), Is.EqualTo("5"));
            Assert.That(await TableNamesAsync(database.Context), Does.Contain("bot_run_triggers"));
            Assert.That(await TableNamesAsync(database.Context), Does.Contain("bot_runs"));
            Assert.That(await TableNamesAsync(database.Context), Does.Contain("bot_tool_invocations"));
        });
    }

    [Test]
    public async Task CompletedStageTwoFixtureUpgradesWithoutChangingAnyExistingRow()
    {
        var directory = Path.Combine(Path.GetTempPath(), "trading-stage2-upgrade", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "stage2.db");
        File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "stage2-completed.db"), path);
        try
        {
            await using var context = CreateContext(path);
            await context.Database.OpenConnectionAsync();
            var before = await CaptureStageTwoRowsAsync(context);
            Assert.That(before.Values, Is.All.Not.Empty);
            Assert.That(await ScalarAsync<string>(context, "SELECT content_hash FROM trading_bot_configuration_versions"), Is.EqualTo(new string('a', 64)));
            Assert.That(await ScalarAsync<string>(context, "SELECT content_hash FROM portfolio_decision_snapshots"), Is.EqualTo(new string('b', 64)));

            await new DatabaseInitializer(context).InitializeAsync();

            var after = await CaptureStageTwoRowsAsync(context);
            Assert.Multiple(() =>
            {
                foreach (var table in StageTwoTables)
                {
                    Assert.That(after[table], Is.EqualTo(before[table]), table);
                }
            });
            Assert.That(await ScalarAsync<long>(context, "SELECT COUNT(*) FROM __ef_migrations_history"), Is.EqualTo(7));
            Assert.That(await ScalarAsync<string>(context, "SELECT value FROM schema_metadata WHERE key='application_data_format_version'"), Is.EqualTo("5"));
        }
        finally
        {
            SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory,
                SqliteTestDatabaseCleanup.ConnectionString(path));
        }
    }

    [Test]
    public async Task SchemaHasExactAuditColumnsIndexesChecksAndRestrictedRelationships()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(database.Context).InitializeAsync();

        Assert.That(await ColumnsAsync(database.Context, "bot_run_triggers"), Is.EqualTo(TriggerColumns));
        Assert.That(await ColumnsAsync(database.Context, "bot_runs"), Is.EqualTo(RunColumns));
        Assert.That(await ColumnsAsync(database.Context, "bot_tool_invocations"), Is.EqualTo(ToolColumns));

        var indexes = await SchemaSqlAsync(database.Context, "index");
        Assert.Multiple(() =>
        {
            Assert.That(indexes["IX_bot_runs_trading_bot_id"], Does.Contain("'Pending', 'AcquiringLease', 'PreparingSnapshot', 'Reasoning', 'WaitingForTool'"));
            Assert.That(indexes["IX_bot_run_triggers_trading_bot_id_source_type_source_id"], Does.Contain("source_id IS NOT NULL"));
            Assert.That(indexes.Keys, Does.Contain("IX_bot_tool_invocations_bot_run_id_sequence_number"));
            Assert.That(indexes.Keys, Does.Contain("IX_bot_runs_status_lease_expires_at"));
            Assert.That(indexes.Keys, Does.Contain("IX_bot_runs_trading_bot_id_started_at"));
            Assert.That(indexes.Keys, Does.Contain("IX_bot_run_triggers_trading_bot_id_consumed_by_run_id_occurred_at"));
        });

        var tables = await SchemaSqlAsync(database.Context, "table");
        var auditSchema = string.Join('\n', new[] { tables["bot_run_triggers"], tables["bot_runs"], tables["bot_tool_invocations"] });
        foreach (var check in new[] { "ck_bot_run_triggers_type", "ck_bot_run_triggers_source", "ck_bot_runs_status", "ck_bot_runs_lease", "ck_bot_runs_completion", "ck_bot_runs_transcript_schema", "ck_bot_runs_version", "ck_bot_tool_invocations_sequence", "ck_bot_tool_invocations_schema", "ck_bot_tool_invocations_status", "ck_bot_tool_invocations_completion" })
        {
            Assert.That(auditSchema, Does.Contain(check));
        }

        var foreignKeys = new List<string>();
        foreach (var table in new[] { "bot_run_triggers", "bot_runs", "bot_tool_invocations", "trading_bots" })
        {
            foreignKeys.AddRange(await StringsAsync(database.Context, $"SELECT on_delete FROM pragma_foreign_key_list('{table}')"));
        }
        Assert.That(foreignKeys, Has.Count.EqualTo(8));
        Assert.That(foreignKeys, Is.All.EqualTo("RESTRICT"));
    }

    [Test]
    public async Task ActiveRunTriggerAndToolUniquenessAreEnforcedBySqlite()
    {
        await using var database = await UpgradedFixtureAsync();
        foreach (var status in new[] { "Pending", "AcquiringLease", "PreparingSnapshot", "Reasoning", "WaitingForTool" })
        {
            await InsertRunAsync(database.Context, $"01{status[0]}00000000000000000000000", status);
            Assert.That(async () => await InsertRunAsync(database.Context, $"02{status[0]}00000000000000000000000", "Pending"), Throws.TypeOf<SqliteException>(), status);
            await ExecuteAsync(database.Context, "DELETE FROM bot_runs");
        }

        await InsertRunAsync(database.Context, "01RRRRRRRRRRRRRRRRRRRRRR", "Pending");
        await ExecuteAsync(database.Context, $"INSERT INTO bot_run_triggers VALUES ('01TTTTTTTTTTTTTTTTTTTTTT','{BotId}','Manual','first','event','source-1',1000,NULL,1000)");
        Assert.That(async () => await ExecuteAsync(database.Context, $"INSERT INTO bot_run_triggers VALUES ('02TTTTTTTTTTTTTTTTTTTTTT','{BotId}','Manual','duplicate','event','source-1',1001,NULL,1001)"), Throws.TypeOf<SqliteException>());
        await ExecuteAsync(database.Context, "INSERT INTO bot_tool_invocations VALUES ('01VVVVVVVVVVVVVVVVVVVVVV','01RRRRRRRRRRRRRRRRRRRRRR',1,'Finish',1,'{}','Started',1000,NULL,NULL,NULL,NULL,NULL,NULL)");
        Assert.That(async () => await ExecuteAsync(database.Context, "INSERT INTO bot_tool_invocations VALUES ('02VVVVVVVVVVVVVVVVVVVVVV','01RRRRRRRRRRRRRRRRRRRRRR',1,'Finish',1,'{}','Started',1001,NULL,NULL,NULL,NULL,NULL,NULL)"), Throws.TypeOf<SqliteException>());
    }

    [Test]
    public async Task AuditRelationshipsRejectDeletionAndModelHasNoPendingChanges()
    {
        await using var database = await UpgradedFixtureAsync();
        await InsertRunAsync(database.Context, "01RRRRRRRRRRRRRRRRRRRRRR", "Pending");
        await ExecuteAsync(database.Context, $"INSERT INTO bot_run_triggers VALUES ('01TTTTTTTTTTTTTTTTTTTTTT','{BotId}','Manual','claimed',NULL,NULL,1000,'01RRRRRRRRRRRRRRRRRRRRRR',1000)");
        await ExecuteAsync(database.Context, "INSERT INTO bot_tool_invocations VALUES ('01VVVVVVVVVVVVVVVVVVVVVV','01RRRRRRRRRRRRRRRRRRRRRR',1,'Finish',1,'{}','Started',1000,NULL,NULL,NULL,NULL,NULL,NULL)");

        Assert.Multiple(() =>
        {
            Assert.That(async () => await ExecuteAsync(database.Context, $"DELETE FROM trading_bots WHERE id='{BotId}'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(database.Context, $"DELETE FROM trading_bot_configuration_versions WHERE id='{ConfigurationId}'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(database.Context, $"DELETE FROM portfolio_decision_snapshots WHERE id='{SnapshotId}'"), Throws.TypeOf<SqliteException>());
            Assert.That(async () => await ExecuteAsync(database.Context, "DELETE FROM bot_runs WHERE id='01RRRRRRRRRRRRRRRRRRRRRR'"), Throws.TypeOf<SqliteException>());
            Assert.That(database.Context.Database.HasPendingModelChanges(), Is.False);
        });
    }

    private static async Task<TemporarySqliteDatabase> UpgradedFixtureAsync()
    {
        var database = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(database.Context).InitializeAsync();
        await ExecuteAsync(database.Context, $"INSERT INTO trading_bots (id,name,status,active_configuration_version_id,requested_next_run_at,accepted_next_run_at,last_completed_run_id,created_at,updated_at,version) VALUES ('{BotId}','Runtime Bot','Enabled',NULL,NULL,NULL,NULL,1000,1000,1)");
        await ExecuteAsync(database.Context, $"INSERT INTO trading_bot_configuration_versions (id,trading_bot_id,version_number,investment_mandate_json,risk_policy_json,tool_policy_json,run_budget_json,scheduling_policy_json,execution_mode,model_configuration_json,prompt_version,content_hash,created_at,activated_at,superseded_at) VALUES ('{ConfigurationId}','{BotId}',1,'{{}}','{{}}','{{}}','{{}}','{{}}','PaperTrading','{{}}','prompt','{new string('c', 64)}',1000,1000,NULL)");
        await ExecuteAsync(database.Context, $"UPDATE trading_bots SET active_configuration_version_id='{ConfigurationId}' WHERE id='{BotId}'");
        await ExecuteAsync(database.Context, $"INSERT INTO portfolios (id,name,base_currency,broker_account_id,assigned_trading_bot_id,status,capital_allocation_amount,cash_reserve_policy_json,created_at,updated_at,version) VALUES ('01GGGGGGGGGGGGGGGGGGGGGG','Runtime Portfolio','USD',NULL,'{BotId}','Active','1000','{{}}',1000,1000,1)");
        await ExecuteAsync(database.Context, $"INSERT INTO portfolio_decision_snapshots (id,portfolio_id,trading_bot_id,configuration_version_id,as_of,reconciliation_status,data_freshness_json,snapshot_schema_version,snapshot_json,content_hash,created_at) VALUES ('{SnapshotId}','01GGGGGGGGGGGGGGGGGGGGGG','{BotId}','{ConfigurationId}',1000,'Reconciled','{{}}',1,'{{}}','{new string('d', 64)}',1000)");
        return database;
    }

    private static Task InsertRunAsync(TradingDbContext context, string id, string status) => ExecuteAsync(context,
        $"INSERT INTO bot_runs VALUES ('{id}','{BotId}','{ConfigurationId}','{SnapshotId}','{status}',NULL,NULL,1000,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'{{\"schemaVersion\":1}}',1,'{{\"schemaVersion\":1}}','stage3-v1',1,NULL)");

    private static TradingDbContext CreateContext(string path) => new(TradingDbContextFactory.CreateOptions(new DatabaseOptions { DatabasePath = path }, TestContext.CurrentContext.TestDirectory));

    private static async Task<Dictionary<string, string>> CaptureStageTwoRowsAsync(TradingDbContext context)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in StageTwoTables)
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT * FROM {table} ORDER BY 1";
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<string>();
            while (await reader.ReadAsync())
            {
                rows.Add(string.Join('|', Enumerable.Range(0, reader.FieldCount)
                    .Select(i => $"{reader.GetName(i)}={(reader.IsDBNull(i) ? "<null>" : $"{reader.GetFieldType(i).Name}:{reader.GetValue(i)}")}")
                    .Order(StringComparer.Ordinal)));
            }
            result.Add(table, string.Join('\n', rows));
        }
        return result;
    }

    private static async Task ExecuteAsync(TradingDbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(TradingDbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static Task<string[]> TableNamesAsync(TradingDbContext context) => StringsAsync(context, "SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
    private static Task<string[]> ColumnsAsync(TradingDbContext context, string table) => StringsAsync(context, $"SELECT name FROM pragma_table_info('{table}') ORDER BY cid");

    private static async Task<string[]> StringsAsync(TradingDbContext context, string sql)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand(); command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(); var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return [.. values];
    }

    private static async Task<Dictionary<string, string>> SchemaSqlAsync(TradingDbContext context, string type)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name, sql FROM sqlite_schema WHERE type=$type AND sql IS NOT NULL";
        var parameter = command.CreateParameter(); parameter.ParameterName = "$type"; parameter.Value = type; command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(); var values = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await reader.ReadAsync()) values.Add(reader.GetString(0), reader.GetString(1));
        return values;
    }
}
