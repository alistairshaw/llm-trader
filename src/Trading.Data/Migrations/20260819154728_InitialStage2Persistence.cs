using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF scaffolding emits inline metadata arrays required by MigrationBuilder.

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class InitialStage2Persistence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "broker_connections",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                broker_type = table.Column<string>(type: "TEXT", nullable: false),
                display_name = table.Column<string>(type: "TEXT", nullable: false),
                environment = table.Column<string>(type: "TEXT", nullable: false),
                credential_reference = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                capabilities_json = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_broker_connections", x => x.id);
                table.CheckConstraint("ck_broker_connections_environment", "environment IN ('Paper', 'Live')");
                table.CheckConstraint("ck_broker_connections_status", "status IN ('Enabled', 'Disabled', 'Disconnected')");
            });

        migrationBuilder.CreateTable(
            name: "instruments",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                instrument_type = table.Column<string>(type: "TEXT", nullable: false),
                primary_symbol = table.Column<string>(type: "TEXT", nullable: false),
                display_name = table.Column<string>(type: "TEXT", nullable: false),
                currency = table.Column<string>(type: "TEXT", nullable: false),
                exchange = table.Column<string>(type: "TEXT", nullable: false),
                price_precision = table.Column<int>(type: "INTEGER", nullable: false),
                quantity_precision = table.Column<int>(type: "INTEGER", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_instruments", x => x.id);
                table.CheckConstraint("ck_instruments_price_precision", "price_precision BETWEEN 0 AND 8");
                table.CheckConstraint("ck_instruments_quantity_precision", "quantity_precision BETWEEN 0 AND 8");
                table.CheckConstraint("ck_instruments_status", "status IN ('Active', 'Inactive')");
                table.CheckConstraint("ck_instruments_type", "instrument_type IN ('Equity', 'Option', 'Fund', 'Bond', 'Cash', 'Crypto')");
            });

        migrationBuilder.CreateTable(
            name: "schema_metadata",
            columns: table => new
            {
                key = table.Column<string>(type: "TEXT", nullable: false),
                value = table.Column<string>(type: "TEXT", nullable: false),
                updated_at = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_schema_metadata", x => x.key);
            });

        migrationBuilder.CreateTable(
            name: "broker_accounts",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                broker_connection_id = table.Column<string>(type: "TEXT", nullable: false),
                external_account_id = table.Column<string>(type: "TEXT", nullable: false),
                display_name = table.Column<string>(type: "TEXT", nullable: false),
                account_type = table.Column<string>(type: "TEXT", nullable: false),
                base_currency = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                last_reconciled_at = table.Column<long>(type: "INTEGER", nullable: true),
                capabilities_json = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_broker_accounts", x => x.id);
                table.CheckConstraint("ck_broker_accounts_status", "status IN ('Active', 'Restricted', 'Disabled')");
                table.ForeignKey(
                    name: "FK_broker_accounts_broker_connections_broker_connection_id",
                    column: x => x.broker_connection_id,
                    principalTable: "broker_connections",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "instrument_broker_mappings",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                broker_connection_id = table.Column<string>(type: "TEXT", nullable: false),
                external_instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                symbol = table.Column<string>(type: "TEXT", nullable: false),
                exchange = table.Column<string>(type: "TEXT", nullable: false),
                effective_from = table.Column<long>(type: "INTEGER", nullable: false),
                effective_to = table.Column<long>(type: "INTEGER", nullable: true),
                metadata_json = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_instrument_broker_mappings", x => x.id);
                table.CheckConstraint("ck_instrument_broker_mappings_interval", "effective_to IS NULL OR effective_to > effective_from");
                table.ForeignKey(
                    name: "FK_instrument_broker_mappings_broker_connections_broker_connection_id",
                    column: x => x.broker_connection_id,
                    principalTable: "broker_connections",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_instrument_broker_mappings_instruments_instrument_id",
                    column: x => x.instrument_id,
                    principalTable: "instruments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "portfolio_decision_snapshots",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                portfolio_id = table.Column<string>(type: "TEXT", nullable: false),
                trading_bot_id = table.Column<string>(type: "TEXT", nullable: false),
                configuration_version_id = table.Column<string>(type: "TEXT", nullable: false),
                as_of = table.Column<long>(type: "INTEGER", nullable: false),
                reconciliation_status = table.Column<string>(type: "TEXT", nullable: false),
                data_freshness_json = table.Column<string>(type: "TEXT", nullable: false),
                snapshot_schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                snapshot_json = table.Column<string>(type: "TEXT", nullable: false),
                content_hash = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_portfolio_decision_snapshots", x => x.id);
                table.CheckConstraint("ck_portfolio_decision_snapshots_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)");
                table.CheckConstraint("ck_portfolio_decision_snapshots_reconciliation_status", "reconciliation_status IN ('Reconciled', 'Pending', 'Uncertain')");
                table.CheckConstraint("ck_portfolio_decision_snapshots_schema_version", "snapshot_schema_version > 0");
            });

        migrationBuilder.CreateTable(
            name: "portfolio_ledger_entries",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                portfolio_id = table.Column<string>(type: "TEXT", nullable: false),
                entry_type = table.Column<string>(type: "TEXT", nullable: false),
                amount = table.Column<string>(type: "TEXT", nullable: true),
                currency = table.Column<string>(type: "TEXT", nullable: true),
                instrument_id = table.Column<string>(type: "TEXT", nullable: true),
                quantity = table.Column<string>(type: "TEXT", nullable: true),
                effective_at = table.Column<long>(type: "INTEGER", nullable: false),
                recorded_at = table.Column<long>(type: "INTEGER", nullable: false),
                source_type = table.Column<string>(type: "TEXT", nullable: false),
                source_id = table.Column<string>(type: "TEXT", nullable: false),
                reverses_entry_id = table.Column<string>(type: "TEXT", nullable: true),
                description = table.Column<string>(type: "TEXT", nullable: true),
                metadata_json = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_portfolio_ledger_entries", x => x.id);
                table.CheckConstraint("ck_portfolio_ledger_entries_source_type", "source_type IN ('BrokerExecution', 'BrokerEvent', 'AuditedAdjustment')");
                table.CheckConstraint("ck_portfolio_ledger_entries_type", "entry_type IN ('Deposit', 'Withdrawal', 'Settlement', 'Fee', 'Dividend', 'Interest', 'Tax', 'CorporateAction', 'ManualCorrection')");
                table.ForeignKey(
                    name: "FK_portfolio_ledger_entries_instruments_instrument_id",
                    column: x => x.instrument_id,
                    principalTable: "instruments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_portfolio_ledger_entries_portfolio_ledger_entries_reverses_entry_id",
                    column: x => x.reverses_entry_id,
                    principalTable: "portfolio_ledger_entries",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "portfolios",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                name = table.Column<string>(type: "TEXT", nullable: false),
                base_currency = table.Column<string>(type: "TEXT", nullable: false),
                broker_account_id = table.Column<string>(type: "TEXT", nullable: true),
                assigned_trading_bot_id = table.Column<string>(type: "TEXT", nullable: true),
                status = table.Column<string>(type: "TEXT", nullable: false),
                capital_allocation_amount = table.Column<string>(type: "TEXT", nullable: false),
                cash_reserve_policy_json = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_portfolios", x => x.id);
                table.CheckConstraint("ck_portfolios_status", "status IN ('Active', 'Paused', 'Closed')");
                table.ForeignKey(
                    name: "FK_portfolios_broker_accounts_broker_account_id",
                    column: x => x.broker_account_id,
                    principalTable: "broker_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "positions",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                portfolio_id = table.Column<string>(type: "TEXT", nullable: false),
                instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                quantity_unit = table.Column<string>(type: "TEXT", nullable: false),
                quantity = table.Column<string>(type: "TEXT", nullable: false),
                average_cost_amount = table.Column<string>(type: "TEXT", nullable: false),
                average_cost_currency = table.Column<string>(type: "TEXT", nullable: false),
                realized_pnl_amount = table.Column<string>(type: "TEXT", nullable: false),
                realized_pnl_currency = table.Column<string>(type: "TEXT", nullable: false),
                opened_at = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                closed_at = table.Column<long>(type: "INTEGER", nullable: true),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_positions", x => x.id);
                table.ForeignKey(
                    name: "FK_positions_instruments_instrument_id",
                    column: x => x.instrument_id,
                    principalTable: "instruments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_positions_portfolios_portfolio_id",
                    column: x => x.portfolio_id,
                    principalTable: "portfolios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "position_applied_fills",
            columns: table => new
            {
                position_id = table.Column<string>(type: "TEXT", nullable: false),
                fill_id = table.Column<string>(type: "TEXT", nullable: false),
                applied_at = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_position_applied_fills", x => new { x.position_id, x.fill_id });
                table.ForeignKey(
                    name: "FK_position_applied_fills_positions_position_id",
                    column: x => x.position_id,
                    principalTable: "positions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "trading_bot_configuration_versions",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                trading_bot_id = table.Column<string>(type: "TEXT", nullable: false),
                version_number = table.Column<int>(type: "INTEGER", nullable: false),
                investment_mandate_json = table.Column<string>(type: "TEXT", nullable: false),
                risk_policy_json = table.Column<string>(type: "TEXT", nullable: false),
                tool_policy_json = table.Column<string>(type: "TEXT", nullable: false),
                run_budget_json = table.Column<string>(type: "TEXT", nullable: false),
                scheduling_policy_json = table.Column<string>(type: "TEXT", nullable: false),
                execution_mode = table.Column<string>(type: "TEXT", nullable: false),
                model_configuration_json = table.Column<string>(type: "TEXT", nullable: false),
                prompt_version = table.Column<string>(type: "TEXT", nullable: false),
                content_hash = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                activated_at = table.Column<long>(type: "INTEGER", nullable: true),
                superseded_at = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_trading_bot_configuration_versions", x => x.id);
                table.CheckConstraint("ck_trading_bot_configuration_versions_execution_mode", "execution_mode IN ('ResearchOnly', 'HumanApproval', 'PaperTrading', 'LiveTrading')");
                table.CheckConstraint("ck_trading_bot_configuration_versions_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)");
                table.CheckConstraint("ck_trading_bot_configuration_versions_number", "version_number > 0");
            });

        migrationBuilder.CreateTable(
            name: "trading_bots",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                name = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                active_configuration_version_id = table.Column<string>(type: "TEXT", nullable: true),
                requested_next_run_at = table.Column<long>(type: "INTEGER", nullable: true),
                accepted_next_run_at = table.Column<long>(type: "INTEGER", nullable: true),
                last_completed_run_id = table.Column<string>(type: "TEXT", nullable: true),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_trading_bots", x => x.id);
                table.CheckConstraint("ck_trading_bots_status", "status IN ('Enabled', 'Paused', 'Retired')");
                table.ForeignKey(
                    name: "FK_trading_bots_trading_bot_configuration_versions_active_configuration_version_id",
                    column: x => x.active_configuration_version_id,
                    principalTable: "trading_bot_configuration_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            table: "schema_metadata",
            columns: new[] { "key", "updated_at", "value" },
            values: new object[] { "application_data_format_version", 0L, "2" });

        migrationBuilder.CreateIndex(
            name: "IX_broker_accounts_broker_connection_id_external_account_id",
            table: "broker_accounts",
            columns: new[] { "broker_connection_id", "external_account_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_instrument_broker_mappings_broker_connection_id_external_instrument_id",
            table: "instrument_broker_mappings",
            columns: new[] { "broker_connection_id", "external_instrument_id" });

        migrationBuilder.CreateIndex(
            name: "IX_instrument_broker_mappings_broker_connection_id_external_instrument_id_effective_from",
            table: "instrument_broker_mappings",
            columns: new[] { "broker_connection_id", "external_instrument_id", "effective_from" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_instrument_broker_mappings_instrument_id",
            table: "instrument_broker_mappings",
            column: "instrument_id");

        migrationBuilder.CreateIndex(
            name: "IX_instruments_instrument_type_primary_symbol_exchange",
            table: "instruments",
            columns: new[] { "instrument_type", "primary_symbol", "exchange" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_portfolio_decision_snapshots_configuration_version_id",
            table: "portfolio_decision_snapshots",
            column: "configuration_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_portfolio_decision_snapshots_portfolio_id_as_of",
            table: "portfolio_decision_snapshots",
            columns: new[] { "portfolio_id", "as_of" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_portfolio_decision_snapshots_trading_bot_id_as_of",
            table: "portfolio_decision_snapshots",
            columns: new[] { "trading_bot_id", "as_of" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_portfolio_ledger_entries_instrument_id",
            table: "portfolio_ledger_entries",
            column: "instrument_id");

        migrationBuilder.CreateIndex(
            name: "IX_portfolio_ledger_entries_portfolio_id_effective_at",
            table: "portfolio_ledger_entries",
            columns: new[] { "portfolio_id", "effective_at" });

        migrationBuilder.CreateIndex(
            name: "IX_portfolio_ledger_entries_portfolio_id_source_type_source_id",
            table: "portfolio_ledger_entries",
            columns: new[] { "portfolio_id", "source_type", "source_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_portfolio_ledger_entries_reverses_entry_id",
            table: "portfolio_ledger_entries",
            column: "reverses_entry_id");

        migrationBuilder.CreateIndex(
            name: "IX_portfolios_assigned_trading_bot_id",
            table: "portfolios",
            column: "assigned_trading_bot_id",
            unique: true,
            filter: "assigned_trading_bot_id IS NOT NULL AND status = 'Active'");

        migrationBuilder.CreateIndex(
            name: "IX_portfolios_broker_account_id",
            table: "portfolios",
            column: "broker_account_id",
            unique: true,
            filter: "broker_account_id IS NOT NULL AND status = 'Active'");

        migrationBuilder.CreateIndex(
            name: "IX_positions_instrument_id",
            table: "positions",
            column: "instrument_id");

        migrationBuilder.CreateIndex(
            name: "IX_positions_portfolio_id_instrument_id",
            table: "positions",
            columns: new[] { "portfolio_id", "instrument_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_trading_bot_configuration_versions_trading_bot_id_content_hash",
            table: "trading_bot_configuration_versions",
            columns: new[] { "trading_bot_id", "content_hash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_trading_bot_configuration_versions_trading_bot_id_version_number",
            table: "trading_bot_configuration_versions",
            columns: new[] { "trading_bot_id", "version_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_trading_bots_active_configuration_version_id",
            table: "trading_bots",
            column: "active_configuration_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_trading_bots_name",
            table: "trading_bots",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_trading_bots_status_accepted_next_run_at",
            table: "trading_bots",
            columns: new[] { "status", "accepted_next_run_at" });

        migrationBuilder.AddForeignKey(
            name: "FK_portfolio_decision_snapshots_portfolios_portfolio_id",
            table: "portfolio_decision_snapshots",
            column: "portfolio_id",
            principalTable: "portfolios",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_portfolio_decision_snapshots_trading_bot_configuration_versions_configuration_version_id",
            table: "portfolio_decision_snapshots",
            column: "configuration_version_id",
            principalTable: "trading_bot_configuration_versions",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_portfolio_decision_snapshots_trading_bots_trading_bot_id",
            table: "portfolio_decision_snapshots",
            column: "trading_bot_id",
            principalTable: "trading_bots",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_portfolio_ledger_entries_portfolios_portfolio_id",
            table: "portfolio_ledger_entries",
            column: "portfolio_id",
            principalTable: "portfolios",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_portfolios_trading_bots_assigned_trading_bot_id",
            table: "portfolios",
            column: "assigned_trading_bot_id",
            principalTable: "trading_bots",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_trading_bot_configuration_versions_trading_bots_trading_bot_id",
            table: "trading_bot_configuration_versions",
            column: "trading_bot_id",
            principalTable: "trading_bots",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            CREATE TRIGGER trading_bot_configuration_versions_immutable_content
            BEFORE UPDATE OF trading_bot_id, version_number, investment_mandate_json, risk_policy_json,
                tool_policy_json, run_budget_json, scheduling_policy_json, execution_mode,
                model_configuration_json, prompt_version, content_hash, created_at
            ON trading_bot_configuration_versions
            BEGIN
                SELECT RAISE(ABORT, 'published trading bot configuration content is immutable');
            END;
            """);

        migrationBuilder.Sql(
            """
            CREATE TRIGGER portfolio_decision_snapshots_immutable_update
            BEFORE UPDATE ON portfolio_decision_snapshots
            BEGIN
                SELECT RAISE(ABORT, 'portfolio decision snapshots are immutable');
            END;
            """);

        migrationBuilder.Sql(
            """
            CREATE TRIGGER portfolio_decision_snapshots_immutable_delete
            BEFORE DELETE ON portfolio_decision_snapshots
            BEGIN
                SELECT RAISE(ABORT, 'portfolio decision snapshots are immutable');
            END;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS portfolio_decision_snapshots_immutable_delete;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS portfolio_decision_snapshots_immutable_update;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trading_bot_configuration_versions_immutable_content;");
        migrationBuilder.DropForeignKey(
            name: "FK_trading_bots_trading_bot_configuration_versions_active_configuration_version_id",
            table: "trading_bots");

        migrationBuilder.DropTable(
            name: "instrument_broker_mappings");

        migrationBuilder.DropTable(
            name: "portfolio_decision_snapshots");

        migrationBuilder.DropTable(
            name: "portfolio_ledger_entries");

        migrationBuilder.DropTable(
            name: "position_applied_fills");

        migrationBuilder.DropTable(
            name: "schema_metadata");

        migrationBuilder.DropTable(
            name: "positions");

        migrationBuilder.DropTable(
            name: "instruments");

        migrationBuilder.DropTable(
            name: "portfolios");

        migrationBuilder.DropTable(
            name: "broker_accounts");

        migrationBuilder.DropTable(
            name: "broker_connections");

        migrationBuilder.DropTable(
            name: "trading_bot_configuration_versions");

        migrationBuilder.DropTable(
            name: "trading_bots");
    }
}
