using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF scaffolding emits inline metadata arrays required by MigrationBuilder.

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AddStage3BotRuntime : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bot_runs",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                trading_bot_id = table.Column<string>(type: "TEXT", nullable: false),
                configuration_version_id = table.Column<string>(type: "TEXT", nullable: false),
                portfolio_snapshot_id = table.Column<string>(type: "TEXT", nullable: true),
                status = table.Column<string>(type: "TEXT", nullable: false),
                lease_owner = table.Column<string>(type: "TEXT", nullable: true),
                lease_expires_at = table.Column<long>(type: "INTEGER", nullable: true),
                started_at = table.Column<long>(type: "INTEGER", nullable: false),
                completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                finish_status = table.Column<string>(type: "TEXT", nullable: true),
                finish_summary = table.Column<string>(type: "TEXT", nullable: true),
                requested_next_run_at = table.Column<long>(type: "INTEGER", nullable: true),
                requested_wake_reason = table.Column<string>(type: "TEXT", nullable: true),
                accepted_next_run_at = table.Column<long>(type: "INTEGER", nullable: true),
                terminal_reason = table.Column<string>(type: "TEXT", nullable: true),
                usage_json = table.Column<string>(type: "TEXT", nullable: false),
                model_transcript_schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                model_transcript_json = table.Column<string>(type: "TEXT", nullable: false),
                input_rendering_version = table.Column<string>(type: "TEXT", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bot_runs", x => x.id);
                table.CheckConstraint("ck_bot_runs_completion", "(status IN ('Pending', 'AcquiringLease', 'PreparingSnapshot', 'Reasoning', 'WaitingForTool') AND completed_at IS NULL) OR (status IN ('Completed', 'TimedOut', 'BudgetExceeded', 'Cancelled', 'Faulted') AND completed_at IS NOT NULL)");
                table.CheckConstraint("ck_bot_runs_lease", "(lease_owner IS NULL AND lease_expires_at IS NULL) OR (lease_owner IS NOT NULL AND lease_expires_at IS NOT NULL)");
                table.CheckConstraint("ck_bot_runs_rendering_version", "length(input_rendering_version) > 0");
                table.CheckConstraint("ck_bot_runs_status", "status IN ('Pending', 'AcquiringLease', 'PreparingSnapshot', 'Reasoning', 'WaitingForTool', 'Completed', 'TimedOut', 'BudgetExceeded', 'Cancelled', 'Faulted')");
                table.CheckConstraint("ck_bot_runs_transcript_schema", "model_transcript_schema_version > 0");
                table.CheckConstraint("ck_bot_runs_version", "version > 0");
                table.ForeignKey(
                    name: "FK_bot_runs_portfolio_decision_snapshots_portfolio_snapshot_id",
                    column: x => x.portfolio_snapshot_id,
                    principalTable: "portfolio_decision_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_bot_runs_trading_bot_configuration_versions_configuration_version_id",
                    column: x => x.configuration_version_id,
                    principalTable: "trading_bot_configuration_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_bot_runs_trading_bots_trading_bot_id",
                    column: x => x.trading_bot_id,
                    principalTable: "trading_bots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "bot_run_triggers",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                trading_bot_id = table.Column<string>(type: "TEXT", nullable: false),
                trigger_type = table.Column<string>(type: "TEXT", nullable: false),
                reason = table.Column<string>(type: "TEXT", nullable: false),
                source_type = table.Column<string>(type: "TEXT", nullable: true),
                source_id = table.Column<string>(type: "TEXT", nullable: true),
                occurred_at = table.Column<long>(type: "INTEGER", nullable: false),
                consumed_by_run_id = table.Column<string>(type: "TEXT", nullable: true),
                created_at = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bot_run_triggers", x => x.id);
                table.CheckConstraint("ck_bot_run_triggers_reason", "length(reason) > 0");
                table.CheckConstraint("ck_bot_run_triggers_source", "(source_type IS NULL AND source_id IS NULL) OR (source_type IS NOT NULL AND source_id IS NOT NULL)");
                table.CheckConstraint("ck_bot_run_triggers_type", "trigger_type IN ('Manual', 'BaselineSchedule', 'AcceptedNextRun', 'ResearchCompleted', 'ResearchFailed', 'PortfolioEvent', 'RiskOrReconciliation')");
                table.ForeignKey(
                    name: "FK_bot_run_triggers_bot_runs_consumed_by_run_id",
                    column: x => x.consumed_by_run_id,
                    principalTable: "bot_runs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_bot_run_triggers_trading_bots_trading_bot_id",
                    column: x => x.trading_bot_id,
                    principalTable: "trading_bots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "bot_tool_invocations",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                bot_run_id = table.Column<string>(type: "TEXT", nullable: false),
                sequence_number = table.Column<int>(type: "INTEGER", nullable: false),
                tool_name = table.Column<string>(type: "TEXT", nullable: false),
                tool_schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                arguments_json = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                started_at = table.Column<long>(type: "INTEGER", nullable: false),
                completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                result_json = table.Column<string>(type: "TEXT", nullable: true),
                result_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                error_code = table.Column<string>(type: "TEXT", nullable: true),
                error_detail = table.Column<string>(type: "TEXT", nullable: true),
                usage_json = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bot_tool_invocations", x => x.id);
                table.CheckConstraint("ck_bot_tool_invocations_completion", "(status = 'Started' AND completed_at IS NULL) OR (status IN ('Completed', 'Failed', 'Cancelled') AND completed_at IS NOT NULL)");
                table.CheckConstraint("ck_bot_tool_invocations_schema", "tool_schema_version > 0");
                table.CheckConstraint("ck_bot_tool_invocations_sequence", "sequence_number > 0");
                table.CheckConstraint("ck_bot_tool_invocations_status", "status IN ('Started', 'Completed', 'Failed', 'Cancelled')");
                table.ForeignKey(
                    name: "FK_bot_tool_invocations_bot_runs_bot_run_id",
                    column: x => x.bot_run_id,
                    principalTable: "bot_runs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.UpdateData(
            table: "schema_metadata",
            keyColumn: "key",
            keyValue: "application_data_format_version",
            column: "value",
            value: "3");

        migrationBuilder.CreateIndex(
            name: "IX_trading_bots_last_completed_run_id",
            table: "trading_bots",
            column: "last_completed_run_id");

        migrationBuilder.CreateIndex(
            name: "IX_bot_run_triggers_consumed_by_run_id",
            table: "bot_run_triggers",
            column: "consumed_by_run_id");

        migrationBuilder.CreateIndex(
            name: "IX_bot_run_triggers_trading_bot_id_consumed_by_run_id_occurred_at",
            table: "bot_run_triggers",
            columns: new[] { "trading_bot_id", "consumed_by_run_id", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "IX_bot_run_triggers_trading_bot_id_source_type_source_id",
            table: "bot_run_triggers",
            columns: new[] { "trading_bot_id", "source_type", "source_id" },
            unique: true,
            filter: "source_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_bot_runs_configuration_version_id",
            table: "bot_runs",
            column: "configuration_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_bot_runs_portfolio_snapshot_id",
            table: "bot_runs",
            column: "portfolio_snapshot_id");

        migrationBuilder.CreateIndex(
            name: "IX_bot_runs_status_lease_expires_at",
            table: "bot_runs",
            columns: new[] { "status", "lease_expires_at" });

        migrationBuilder.CreateIndex(
            name: "IX_bot_runs_trading_bot_id",
            table: "bot_runs",
            column: "trading_bot_id",
            unique: true,
            filter: "status IN ('Pending', 'AcquiringLease', 'PreparingSnapshot', 'Reasoning', 'WaitingForTool')");

        migrationBuilder.CreateIndex(
            name: "IX_bot_runs_trading_bot_id_started_at",
            table: "bot_runs",
            columns: new[] { "trading_bot_id", "started_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_bot_tool_invocations_bot_run_id_sequence_number",
            table: "bot_tool_invocations",
            columns: new[] { "bot_run_id", "sequence_number" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_trading_bots_bot_runs_last_completed_run_id",
            table: "trading_bots",
            column: "last_completed_run_id",
            principalTable: "bot_runs",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_trading_bots_bot_runs_last_completed_run_id",
            table: "trading_bots");

        migrationBuilder.DropTable(
            name: "bot_run_triggers");

        migrationBuilder.DropTable(
            name: "bot_tool_invocations");

        migrationBuilder.DropTable(
            name: "bot_runs");

        migrationBuilder.DropIndex(
            name: "IX_trading_bots_last_completed_run_id",
            table: "trading_bots");

        migrationBuilder.UpdateData(
            table: "schema_metadata",
            keyColumn: "key",
            keyValue: "application_data_format_version",
            column: "value",
            value: "2");
    }
}
