using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Trading.TestInfrastructure;

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
            ["trading_bots"] = ["id", "accepted_next_run_at", "active_configuration_version_id", "created_at", "last_completed_run_id", "name", "requested_next_run_at", "status", "updated_at", "version"],
            ["trading_bot_configuration_versions"] = ["id", "trading_bot_id", "version_number", "investment_mandate_json", "risk_policy_json", "tool_policy_json", "run_budget_json", "scheduling_policy_json", "execution_mode", "model_configuration_json", "prompt_version", "content_hash", "created_at", "activated_at", "superseded_at"],
            ["bot_run_triggers"] = ["id", "trading_bot_id", "trigger_type", "reason", "source_type", "source_id", "occurred_at", "consumed_by_run_id", "created_at"],
            ["bot_runs"] = ["id", "trading_bot_id", "configuration_version_id", "portfolio_snapshot_id", "status", "lease_owner", "lease_expires_at", "started_at", "completed_at", "finish_status", "finish_summary", "requested_next_run_at", "requested_wake_reason", "accepted_next_run_at", "terminal_reason", "usage_json", "model_transcript_schema_version", "model_transcript_json", "input_rendering_version", "version", "input_rendering_hash"],
            ["bot_tool_invocations"] = ["id", "bot_run_id", "sequence_number", "tool_name", "tool_schema_version", "arguments_json", "status", "started_at", "completed_at", "result_json", "result_artifact_id", "error_code", "error_detail", "usage_json"],
            ["portfolios"] = ["id", "name", "base_currency", "broker_account_id", "assigned_trading_bot_id", "status", "capital_allocation_amount", "cash_reserve_policy_json", "created_at", "updated_at", "version"],
            ["positions"] = ["id", "portfolio_id", "instrument_id", "quantity_unit", "quantity", "average_cost_amount", "average_cost_currency", "realized_pnl_amount", "realized_pnl_currency", "opened_at", "updated_at", "closed_at", "version"],
            ["position_applied_fills"] = ["position_id", "fill_id", "applied_at"],
            ["portfolio_ledger_entries"] = ["id", "portfolio_id", "entry_type", "amount", "currency", "instrument_id", "quantity", "effective_at", "recorded_at", "source_type", "source_id", "reverses_entry_id", "description", "metadata_json"],
            ["portfolio_decision_snapshots"] = ["id", "portfolio_id", "trading_bot_id", "configuration_version_id", "as_of", "reconciliation_status", "data_freshness_json", "snapshot_schema_version", "snapshot_json", "content_hash", "created_at"],
            ["research_requests"] = ["id", "subject_type", "subject_id", "question", "normalized_research_key", "as_of", "status", "visibility", "requesting_bot_id", "freshness_requirement_json", "request_json", "started_at", "completed_at", "result_report_id", "created_at", "version"],
            ["research_subscriptions"] = ["id", "research_request_id", "trading_bot_id", "subscribed_at", "notification_status", "notified_at"],
            ["research_runs"] = ["id", "research_request_id", "attempt_number", "status", "model_configuration_json", "prompt_version", "tool_set_version", "report_schema_version", "started_at", "completed_at", "terminal_reason", "usage_json", "version"],
            ["research_tool_invocations"] = ["id", "research_run_id", "sequence_number", "tool_name", "tool_schema_version", "arguments_json", "status", "started_at", "completed_at", "result_json", "result_artifact_id", "error_code", "error_detail", "usage_json"],
            ["research_reports"] = ["id", "report_series_id", "version_number", "research_request_id", "research_run_id", "subject_type", "subject_id", "question", "visibility", "data_cutoff", "generated_at", "expires_at", "status", "supersedes_report_id", "report_schema_version", "content_json", "content_markdown", "content_hash", "generator_metadata_json"],
            ["research_report_sources"] = ["id", "research_report_id", "source_sequence", "source_type", "source_uri", "stable_source_id", "title", "publisher", "published_at", "retrieved_at", "content_hash", "metadata_json"],
            ["hypotheses"] = ["id", "name", "status", "current_version_id", "created_at", "updated_at", "version"],
            ["hypothesis_versions"] = ["id", "hypothesis_id", "version_number", "specification_schema_version", "specification_json", "content_hash", "created_at", "frozen_at"],
            ["hypothesis_evidence_reports"] = ["hypothesis_version_id", "research_report_id", "relationship_type"],
            ["hypothesis_test_results"] = ["id", "hypothesis_version_id", "dataset_version", "code_version", "parameters_hash", "status", "started_at", "completed_at", "metrics_json", "artifacts_json", "result_hash"],
            ["trade_proposals"] = ["id", "trading_bot_id", "bot_run_id", "portfolio_id", "portfolio_snapshot_id", "configuration_version_id", "instrument_id", "proposal_type", "requested_action_json", "rationale", "hypothesis_version_id", "status", "created_at", "valid_until", "idempotency_key", "version"],
            ["trade_proposal_evidence_reports"] = ["trade_proposal_id", "research_report_id"],
            ["guardrail_evaluations"] = ["id", "content_hash", "evaluated_at", "evaluation_sequence", "evaluation_stage", "outcome", "policy_version", "rule_results_json", "state_snapshot_id", "trade_proposal_id"],
            ["proposal_approvals"] = ["id", "trade_proposal_id", "decision", "actor_type", "actor_id", "reason", "decided_at", "proposal_version", "state_snapshot_id"],
            ["capital_reservations"] = ["id", "amount", "consumed_at", "created_at", "currency", "expires_at", "order_id", "portfolio_id", "released_at", "status", "trade_proposal_id", "version"],
            ["orders"] = ["id", "broker_account_id", "broker_order_id", "capital_reservation_id", "client_order_id", "completed_at", "correlation_id", "created_at", "currency", "instrument_id", "limit_price", "order_type", "portfolio_id", "quantity", "quantity_unit", "side", "status", "submitted_at", "time_in_force", "trade_proposal_id", "version"],
            ["order_transitions"] = ["id", "correlation_id", "new_status", "occurred_at", "order_id", "previous_status", "reason_code", "reason_detail", "received_at", "sequence_number", "source"],
            ["fills"] = ["id", "order_id", "broker_account_id", "broker_execution_id", "quantity", "price", "currency", "fee_amount", "fee_currency", "executed_at", "received_at", "raw_payload_reference"],
            ["broker_reconciliations"] = ["id", "broker_account_id", "status", "started_at", "completed_at", "broker_snapshot_json", "differences_json", "resolution_json", "correlation_id", "content_hash"],
            ["inbox_messages"] = ["id", "attempt_count", "available_at", "completed_at", "correlation_id", "idempotency_key", "last_error", "lease_expires_at", "lease_owner", "payload_hash", "payload_json", "received_at", "status", "version"],
            ["outbox_messages"] = ["id", "attempt_count", "available_at", "completed_at", "correlation_id", "created_at", "idempotency_key", "last_error", "lease_expires_at", "lease_owner", "order_id", "payload_hash", "payload_json", "status", "version", "work_kind"],
            ["schema_metadata"] = ["key", "value", "updated_at"],
        };

    [Test]
    public async Task InitialMigrationAppliesToNewDatabaseAndIsIdempotent()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var initializer = new DatabaseInitializer(database.Context);

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        Assert.That(await ScalarAsync<long>(database.Context.Database.GetDbConnection(), "SELECT COUNT(*) FROM __ef_migrations_history"), Is.EqualTo(15));
        Assert.That(await ScalarAsync<string>(database.Context.Database.GetDbConnection(), "SELECT value FROM schema_metadata WHERE key = 'application_data_format_version'"), Is.EqualTo("6"));
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
            Assert.That(await ScalarAsync<long>(context.Database.GetDbConnection(), "SELECT COUNT(*) FROM __ef_migrations_history"), Is.EqualTo(15));
        }
        finally
        {
            SqliteTestDatabaseCleanup.DeleteOwnedDirectory(directory,
                SqliteTestDatabaseCleanup.ConnectionString(databasePath));
        }
    }

    [Test]
    public async Task MigratedSchemaMatchesCurrentPersistenceContract()
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

            Assert.That(foreignKeys, Has.Count.EqualTo(63));
        Assert.That(foreignKeys.Select(key => key.DeleteAction), Is.All.EqualTo("RESTRICT"));
        Assert.That(foreignKeys, Does.Contain(("portfolio_ledger_entries", "portfolio_ledger_entries", "RESTRICT")));
            Assert.That(await ScalarAsync<string>(connection, "SELECT MigrationId FROM __ef_migrations_history ORDER BY MigrationId DESC LIMIT 1"), Does.EndWith("_RestoreDurableBrokerWorkTriggers"));
        Assert.That(await ScalarAsync<string>(connection, "SELECT value FROM schema_metadata WHERE key = 'application_data_format_version'"), Is.EqualTo("6"));
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
