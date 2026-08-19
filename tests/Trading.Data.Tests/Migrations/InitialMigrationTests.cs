using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Trading.Data.Tests.Migrations;

[TestFixture]
[Category("Migrations")]
internal sealed class InitialMigrationTests
{
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["broker_connections"] = ["id", "broker_type", "display_name", "environment", "credential_reference", "status", "capabilities_json", "created_at", "updated_at", "version"],
            ["broker_accounts"] = ["id", "broker_connection_id", "external_account_id", "display_name", "account_type", "base_currency", "status", "last_reconciled_at", "capabilities_json", "created_at", "updated_at", "version"],
            ["instruments"] = ["id", "instrument_type", "primary_symbol", "display_name", "currency", "exchange", "price_precision", "quantity_precision", "status", "created_at", "updated_at", "version"],
            ["instrument_broker_mappings"] = ["id", "instrument_id", "broker_connection_id", "external_instrument_id", "symbol", "exchange", "effective_from", "effective_to", "metadata_json"],
            ["trading_bots"] = ["id", "name", "status", "active_configuration_version_id", "requested_next_run_at", "accepted_next_run_at", "last_completed_run_id", "created_at", "updated_at", "version"],
            ["trading_bot_configuration_versions"] = ["id", "trading_bot_id", "version_number", "investment_mandate_json", "risk_policy_json", "tool_policy_json", "run_budget_json", "scheduling_policy_json", "execution_mode", "model_configuration_json", "prompt_version", "content_hash", "created_at", "activated_at", "superseded_at"],
            ["portfolios"] = ["id", "name", "base_currency", "broker_account_id", "assigned_trading_bot_id", "status", "capital_allocation_amount", "cash_reserve_policy_json", "created_at", "updated_at", "version"],
            ["positions"] = ["id", "portfolio_id", "instrument_id", "quantity", "average_cost_amount", "average_cost_currency", "realized_pnl_amount", "realized_pnl_currency", "opened_at", "updated_at", "closed_at", "version"],
            ["position_applied_fills"] = ["position_id", "fill_id", "applied_at"],
            ["portfolio_ledger_entries"] = ["id", "portfolio_id", "entry_type", "amount", "currency", "instrument_id", "quantity", "effective_at", "recorded_at", "source_type", "source_id", "reverses_entry_id", "description", "metadata_json"],
            ["portfolio_decision_snapshots"] = ["id", "portfolio_id", "trading_bot_id", "configuration_version_id", "as_of", "reconciliation_status", "data_freshness_json", "snapshot_schema_version", "snapshot_json", "content_hash", "created_at"],
            ["schema_metadata"] = ["key", "value", "updated_at"],
        };

    [Test]
    public async Task InitialMigrationAppliesToNewDatabaseAndIsIdempotent()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var initializer = new DatabaseInitializer(database.Context);

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        Assert.That(await ScalarAsync<long>(database.Context.Database.GetDbConnection(), "SELECT COUNT(*) FROM __ef_migrations_history"), Is.EqualTo(1));
        Assert.That(await ScalarAsync<string>(database.Context.Database.GetDbConnection(), "SELECT value FROM schema_metadata WHERE key = 'application_data_format_version'"), Is.EqualTo("2"));
    }

    [Test]
    public async Task InitialMigrationUpgradesEmptyStageOneFixture()
    {
        var directory = Path.Combine(Path.GetTempPath(), "trading-stage1-upgrade", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "stage1.db");
        File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "stage1-empty.db"), databasePath);
        try
        {
            var options = new DatabaseOptions { DatabasePath = databasePath };
            await using var context = new TradingDbContext(TradingDbContextFactory.CreateOptions(options, TestContext.CurrentContext.TestDirectory));
            await context.Database.OpenConnectionAsync();
            Assert.That(await TableNamesAsync(context.Database.GetDbConnection()), Is.Empty);

            await new DatabaseInitializer(context).InitializeAsync();

            Assert.That(await TableNamesAsync(context.Database.GetDbConnection()), Does.Contain("portfolios"));
            Assert.That(await ScalarAsync<long>(context.Database.GetDbConnection(), "SELECT COUNT(*) FROM __ef_migrations_history"), Is.EqualTo(1));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task MigratedSchemaMatchesStageTwoContract()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        await new DatabaseInitializer(database.Context).InitializeAsync();
        var connection = database.Context.Database.GetDbConnection();

        var tables = await TableNamesAsync(connection);
        Assert.That(tables, Is.EquivalentTo(ExpectedColumns.Keys.Concat(["__EFMigrationsLock", "__ef_migrations_history"])));
        foreach (var expected in ExpectedColumns)
        {
            Assert.That(await ColumnNamesAsync(connection, expected.Key), Is.EqualTo(expected.Value), expected.Key);
        }

        var versionColumns = new[] { "broker_connections", "broker_accounts", "instruments", "trading_bots", "portfolios", "positions" };
        foreach (var table in versionColumns)
        {
            Assert.That(await ColumnNamesAsync(connection, table), Does.Contain("version"), table);
        }

        var indexSql = await SchemaSqlAsync(connection, "index");
        Assert.That(indexSql.Values.Count(sql => sql.Contains(" UNIQUE ", StringComparison.OrdinalIgnoreCase)), Is.GreaterThanOrEqualTo(10));
        Assert.That(indexSql.Values, Has.Some.Contains("broker_account_id IS NOT NULL"));
        Assert.That(indexSql.Values, Has.Some.Contains("assigned_trading_bot_id IS NOT NULL"));
        Assert.That(indexSql.Keys, Does.Contain("IX_positions_portfolio_id_instrument_id"));
        Assert.That(indexSql.Keys, Does.Contain("IX_portfolio_ledger_entries_portfolio_id_effective_at"));
        Assert.That(indexSql.Keys, Does.Contain("IX_portfolio_decision_snapshots_portfolio_id_as_of"));
        Assert.That(indexSql.Keys, Does.Contain("IX_trading_bots_status_accepted_next_run_at"));

        var tableSql = await SchemaSqlAsync(connection, "table");
        var requiredChecks = new[] { "ck_broker_connections_environment", "ck_broker_connections_status", "ck_broker_accounts_status", "ck_instruments_type", "ck_instruments_status", "ck_instruments_price_precision", "ck_instruments_quantity_precision", "ck_instrument_broker_mappings_interval", "ck_trading_bots_status", "ck_trading_bot_configuration_versions_number", "ck_trading_bot_configuration_versions_execution_mode", "ck_trading_bot_configuration_versions_hash", "ck_portfolios_status", "ck_portfolio_ledger_entries_type", "ck_portfolio_ledger_entries_source_type", "ck_portfolio_decision_snapshots_reconciliation_status", "ck_portfolio_decision_snapshots_schema_version", "ck_portfolio_decision_snapshots_hash" };
        var completeSchema = string.Join('\n', tableSql.Values);
        Assert.Multiple(() =>
        {
            foreach (var check in requiredChecks)
            {
                Assert.That(completeSchema, Does.Contain(check));
            }
        });

        var foreignKeys = new List<(string Table, string Principal, string DeleteAction)>();
        foreach (var table in ExpectedColumns.Keys)
        {
            foreignKeys.AddRange(await ForeignKeysAsync(connection, table));
        }

        Assert.That(foreignKeys, Has.Count.EqualTo(17));
        Assert.That(foreignKeys.Select(key => key.DeleteAction), Is.All.EqualTo("RESTRICT"));
        Assert.That(foreignKeys, Does.Contain(("position_applied_fills", "fills", "RESTRICT")));
        Assert.That(foreignKeys, Does.Contain(("portfolio_ledger_entries", "portfolio_ledger_entries", "RESTRICT")));
        Assert.That(await ScalarAsync<string>(connection, "SELECT MigrationId FROM __ef_migrations_history"), Does.EndWith("_InitialStage2Persistence"));
        Assert.That(await ScalarAsync<string>(connection, "SELECT value FROM schema_metadata WHERE key = 'application_data_format_version'"), Is.EqualTo("2"));
    }

    private static async Task<string[]> TableNamesAsync(DbConnection connection) =>
        await StringsAsync(connection, "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name");

    private static async Task<string[]> ColumnNamesAsync(DbConnection connection, string table) =>
        await StringsAsync(connection, $"SELECT name FROM pragma_table_info('{table}') ORDER BY cid");

    private static async Task<string[]> StringsAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(); var values = new List<string>();
        while (await reader.ReadAsync()) { values.Add(reader.GetString(0)); }
        return [.. values];
    }

    private static async Task<Dictionary<string, string>> SchemaSqlAsync(DbConnection connection, string type)
    {
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT name, sql FROM sqlite_schema WHERE type = $type AND sql IS NOT NULL";
        var parameter = command.CreateParameter(); parameter.ParameterName = "$type"; parameter.Value = type; command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(); var values = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await reader.ReadAsync()) { values.Add(reader.GetString(0), reader.GetString(1)); }
        return values;
    }

    private static async Task<IReadOnlyList<(string Table, string Principal, string DeleteAction)>> ForeignKeysAsync(DbConnection connection, string table)
    {
        await using var command = connection.CreateCommand(); command.CommandText = $"SELECT \"table\", on_delete FROM pragma_foreign_key_list('{table}')";
        await using var reader = await command.ExecuteReaderAsync(); var values = new List<(string, string, string)>();
        while (await reader.ReadAsync()) { values.Add((table, reader.GetString(0), reader.GetString(1))); }
        return values;
    }

    private static async Task<T> ScalarAsync<T>(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
