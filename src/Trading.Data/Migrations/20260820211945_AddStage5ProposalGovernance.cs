using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF scaffolding emits inline metadata arrays required by MigrationBuilder.

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AddStage5ProposalGovernance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "capital_reservations",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                portfolio_id = table.Column<string>(type: "TEXT", nullable: false),
                trade_proposal_id = table.Column<string>(type: "TEXT", nullable: false),
                order_id = table.Column<string>(type: "TEXT", nullable: true),
                amount = table.Column<string>(type: "TEXT", nullable: false),
                currency = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                expires_at = table.Column<long>(type: "INTEGER", nullable: false),
                consumed_at = table.Column<long>(type: "INTEGER", nullable: true),
                released_at = table.Column<long>(type: "INTEGER", nullable: true),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_capital_reservations", x => x.id);
                table.CheckConstraint("ck_capital_reservations_amount", "CAST(amount AS NUMERIC) > 0");
                table.CheckConstraint("ck_capital_reservations_status", "status IN ('Active','Consumed','Released','Expired')");
                table.CheckConstraint("ck_capital_reservations_terminal", "(status='Active' AND consumed_at IS NULL AND released_at IS NULL) OR (status='Consumed' AND consumed_at IS NOT NULL AND released_at IS NULL) OR (status IN ('Released','Expired') AND released_at IS NOT NULL AND consumed_at IS NULL)");
                table.CheckConstraint("ck_capital_reservations_time", "expires_at > created_at");
                table.CheckConstraint("ck_capital_reservations_version", "version > 0");
                table.ForeignKey(
                    name: "FK_capital_reservations_portfolios_portfolio_id",
                    column: x => x.portfolio_id,
                    principalTable: "portfolios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "guardrail_evaluations",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                trade_proposal_id = table.Column<string>(type: "TEXT", nullable: false),
                evaluation_sequence = table.Column<int>(type: "INTEGER", nullable: false),
                evaluation_stage = table.Column<string>(type: "TEXT", nullable: false),
                policy_version = table.Column<string>(type: "TEXT", nullable: false),
                outcome = table.Column<string>(type: "TEXT", nullable: false),
                state_snapshot_id = table.Column<string>(type: "TEXT", nullable: false),
                rule_results_json = table.Column<string>(type: "TEXT", nullable: false),
                evaluated_at = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_guardrail_evaluations", x => x.id);
                table.CheckConstraint("ck_guardrail_evaluation_outcome", "outcome IN ('Passed','Failed','RequiresApproval')");
                table.CheckConstraint("ck_guardrail_evaluation_sequence", "evaluation_sequence > 0");
                table.CheckConstraint("ck_guardrail_evaluation_stage", "evaluation_stage IN ('Initial','ApprovalRevalidation','ReservationRevalidation')");
                table.ForeignKey(
                    name: "FK_guardrail_evaluations_portfolio_decision_snapshots_state_snapshot_id",
                    column: x => x.state_snapshot_id,
                    principalTable: "portfolio_decision_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hypotheses",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                name = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                current_version_id = table.Column<string>(type: "TEXT", nullable: true),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hypotheses", x => x.id);
                table.CheckConstraint("ck_hypotheses_status", "status IN ('Draft','Active','Retired')");
                table.CheckConstraint("ck_hypotheses_version", "version > 0");
            });

        migrationBuilder.CreateTable(
            name: "hypothesis_versions",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                hypothesis_id = table.Column<string>(type: "TEXT", nullable: false),
                version_number = table.Column<int>(type: "INTEGER", nullable: false),
                specification_schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                specification_json = table.Column<string>(type: "TEXT", nullable: false),
                content_hash = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                frozen_at = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hypothesis_versions", x => x.id);
                table.CheckConstraint("ck_hypothesis_versions_hash", "length(content_hash)=64 AND content_hash=lower(content_hash)");
                table.CheckConstraint("ck_hypothesis_versions_number", "version_number > 0 AND specification_schema_version > 0");
                table.ForeignKey(
                    name: "FK_hypothesis_versions_hypotheses_hypothesis_id",
                    column: x => x.hypothesis_id,
                    principalTable: "hypotheses",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hypothesis_evidence_reports",
            columns: table => new
            {
                hypothesis_version_id = table.Column<string>(type: "TEXT", nullable: false),
                research_report_id = table.Column<string>(type: "TEXT", nullable: false),
                relationship_type = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hypothesis_evidence_reports", x => new { x.hypothesis_version_id, x.research_report_id });
                table.CheckConstraint("ck_hypothesis_evidence_relationship", "relationship_type IN ('Supporting','Contradictory','Contextual')");
                table.ForeignKey(
                    name: "FK_hypothesis_evidence_reports_hypothesis_versions_hypothesis_version_id",
                    column: x => x.hypothesis_version_id,
                    principalTable: "hypothesis_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_hypothesis_evidence_reports_research_reports_research_report_id",
                    column: x => x.research_report_id,
                    principalTable: "research_reports",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hypothesis_test_results",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                hypothesis_version_id = table.Column<string>(type: "TEXT", nullable: false),
                dataset_version = table.Column<string>(type: "TEXT", nullable: false),
                code_version = table.Column<string>(type: "TEXT", nullable: false),
                parameters_hash = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                started_at = table.Column<long>(type: "INTEGER", nullable: false),
                completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                metrics_json = table.Column<string>(type: "TEXT", nullable: false),
                artifacts_json = table.Column<string>(type: "TEXT", nullable: false),
                result_hash = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hypothesis_test_results", x => x.id);
                table.CheckConstraint("ck_hypothesis_test_hashes", "length(parameters_hash)=64 AND parameters_hash=lower(parameters_hash) AND length(result_hash)=64 AND result_hash=lower(result_hash)");
                table.CheckConstraint("ck_hypothesis_test_status", "status IN ('Pending','Running','Completed','Failed','Cancelled')");
                table.ForeignKey(
                    name: "FK_hypothesis_test_results_hypothesis_versions_hypothesis_version_id",
                    column: x => x.hypothesis_version_id,
                    principalTable: "hypothesis_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "trade_proposals",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                trading_bot_id = table.Column<string>(type: "TEXT", nullable: false),
                bot_run_id = table.Column<string>(type: "TEXT", nullable: false),
                portfolio_id = table.Column<string>(type: "TEXT", nullable: false),
                portfolio_snapshot_id = table.Column<string>(type: "TEXT", nullable: false),
                configuration_version_id = table.Column<string>(type: "TEXT", nullable: false),
                instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                proposal_type = table.Column<string>(type: "TEXT", nullable: false),
                requested_action_json = table.Column<string>(type: "TEXT", nullable: false),
                rationale = table.Column<string>(type: "TEXT", nullable: false),
                hypothesis_version_id = table.Column<string>(type: "TEXT", nullable: true),
                status = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                valid_until = table.Column<long>(type: "INTEGER", nullable: false),
                idempotency_key = table.Column<string>(type: "TEXT", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_trade_proposals", x => x.id);
                table.CheckConstraint("ck_trade_proposals_status", "status IN ('Recorded','Validating','AwaitingApproval','Approved','Rejected','Reserved','Expired','Cancelled','ResearchOnly')");
                table.CheckConstraint("ck_trade_proposals_time", "valid_until > created_at");
                table.CheckConstraint("ck_trade_proposals_type", "proposal_type IN ('DirectTrade','TargetAllocation')");
                table.CheckConstraint("ck_trade_proposals_version", "version > 0");
                table.ForeignKey(
                    name: "FK_trade_proposals_bot_runs_bot_run_id",
                    column: x => x.bot_run_id,
                    principalTable: "bot_runs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_trade_proposals_hypothesis_versions_hypothesis_version_id",
                    column: x => x.hypothesis_version_id,
                    principalTable: "hypothesis_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_trade_proposals_instruments_instrument_id",
                    column: x => x.instrument_id,
                    principalTable: "instruments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_trade_proposals_portfolio_decision_snapshots_portfolio_snapshot_id",
                    column: x => x.portfolio_snapshot_id,
                    principalTable: "portfolio_decision_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_trade_proposals_portfolios_portfolio_id",
                    column: x => x.portfolio_id,
                    principalTable: "portfolios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_trade_proposals_trading_bot_configuration_versions_configuration_version_id",
                    column: x => x.configuration_version_id,
                    principalTable: "trading_bot_configuration_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_trade_proposals_trading_bots_trading_bot_id",
                    column: x => x.trading_bot_id,
                    principalTable: "trading_bots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "proposal_approvals",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                trade_proposal_id = table.Column<string>(type: "TEXT", nullable: false),
                decision = table.Column<string>(type: "TEXT", nullable: false),
                actor_type = table.Column<string>(type: "TEXT", nullable: false),
                actor_id = table.Column<string>(type: "TEXT", nullable: false),
                reason = table.Column<string>(type: "TEXT", nullable: true),
                decided_at = table.Column<long>(type: "INTEGER", nullable: false),
                proposal_version = table.Column<long>(type: "INTEGER", nullable: false),
                state_snapshot_id = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_proposal_approvals", x => x.id);
                table.CheckConstraint("ck_proposal_approvals_actor", "actor_type IN ('User','AuthorizedPolicy')");
                table.CheckConstraint("ck_proposal_approvals_decision", "decision IN ('Approved','Rejected')");
                table.CheckConstraint("ck_proposal_approvals_version", "proposal_version > 0");
                table.ForeignKey(
                    name: "FK_proposal_approvals_portfolio_decision_snapshots_state_snapshot_id",
                    column: x => x.state_snapshot_id,
                    principalTable: "portfolio_decision_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_proposal_approvals_trade_proposals_trade_proposal_id",
                    column: x => x.trade_proposal_id,
                    principalTable: "trade_proposals",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "trade_proposal_evidence_reports",
            columns: table => new
            {
                trade_proposal_id = table.Column<string>(type: "TEXT", nullable: false),
                research_report_id = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_trade_proposal_evidence_reports", x => new { x.trade_proposal_id, x.research_report_id });
                table.ForeignKey(
                    name: "FK_trade_proposal_evidence_reports_research_reports_research_report_id",
                    column: x => x.research_report_id,
                    principalTable: "research_reports",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_trade_proposal_evidence_reports_trade_proposals_trade_proposal_id",
                    column: x => x.trade_proposal_id,
                    principalTable: "trade_proposals",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.UpdateData(
            table: "schema_metadata",
            keyColumn: "key",
            keyValue: "application_data_format_version",
            column: "value",
            value: "5");

        migrationBuilder.CreateIndex(
            name: "IX_capital_reservations_portfolio_id_status_expires_at",
            table: "capital_reservations",
            columns: new[] { "portfolio_id", "status", "expires_at" });

        migrationBuilder.CreateIndex(
            name: "IX_capital_reservations_trade_proposal_id",
            table: "capital_reservations",
            column: "trade_proposal_id",
            unique: true,
            filter: "status = 'Active'");

        migrationBuilder.CreateIndex(
            name: "IX_guardrail_evaluations_state_snapshot_id",
            table: "guardrail_evaluations",
            column: "state_snapshot_id");

        migrationBuilder.CreateIndex(
            name: "IX_guardrail_evaluations_trade_proposal_id_evaluation_sequence",
            table: "guardrail_evaluations",
            columns: new[] { "trade_proposal_id", "evaluation_sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_hypotheses_current_version_id",
            table: "hypotheses",
            column: "current_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_hypothesis_evidence_reports_research_report_id",
            table: "hypothesis_evidence_reports",
            column: "research_report_id");

        migrationBuilder.CreateIndex(
            name: "IX_hypothesis_test_results_hypothesis_version_id",
            table: "hypothesis_test_results",
            column: "hypothesis_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_hypothesis_versions_hypothesis_id_content_hash",
            table: "hypothesis_versions",
            columns: new[] { "hypothesis_id", "content_hash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_hypothesis_versions_hypothesis_id_version_number",
            table: "hypothesis_versions",
            columns: new[] { "hypothesis_id", "version_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_proposal_approvals_state_snapshot_id",
            table: "proposal_approvals",
            column: "state_snapshot_id");

        migrationBuilder.CreateIndex(
            name: "IX_proposal_approvals_trade_proposal_id",
            table: "proposal_approvals",
            column: "trade_proposal_id");

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposal_evidence_reports_research_report_id",
            table: "trade_proposal_evidence_reports",
            column: "research_report_id");

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposals_bot_run_id",
            table: "trade_proposals",
            column: "bot_run_id");

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposals_configuration_version_id",
            table: "trade_proposals",
            column: "configuration_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposals_hypothesis_version_id",
            table: "trade_proposals",
            column: "hypothesis_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposals_idempotency_key",
            table: "trade_proposals",
            column: "idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposals_instrument_id",
            table: "trade_proposals",
            column: "instrument_id");

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposals_portfolio_id_status_created_at",
            table: "trade_proposals",
            columns: new[] { "portfolio_id", "status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposals_portfolio_snapshot_id",
            table: "trade_proposals",
            column: "portfolio_snapshot_id");

        migrationBuilder.CreateIndex(
            name: "IX_trade_proposals_trading_bot_id",
            table: "trade_proposals",
            column: "trading_bot_id");

        migrationBuilder.AddForeignKey(
            name: "FK_capital_reservations_trade_proposals_trade_proposal_id",
            table: "capital_reservations",
            column: "trade_proposal_id",
            principalTable: "trade_proposals",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_guardrail_evaluations_trade_proposals_trade_proposal_id",
            table: "guardrail_evaluations",
            column: "trade_proposal_id",
            principalTable: "trade_proposals",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_hypotheses_hypothesis_versions_current_version_id",
            table: "hypotheses",
            column: "current_version_id",
            principalTable: "hypothesis_versions",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql("""
            CREATE TRIGGER hypothesis_versions_frozen_immutable BEFORE UPDATE ON hypothesis_versions
            WHEN OLD.frozen_at IS NOT NULL BEGIN SELECT RAISE(ABORT, 'frozen hypothesis version is immutable'); END;
            CREATE TRIGGER hypothesis_versions_frozen_no_delete BEFORE DELETE ON hypothesis_versions
            WHEN OLD.frozen_at IS NOT NULL BEGIN SELECT RAISE(ABORT, 'frozen hypothesis version cannot be deleted'); END;
            CREATE TRIGGER hypothesis_evidence_immutable BEFORE UPDATE ON hypothesis_evidence_reports BEGIN SELECT RAISE(ABORT, 'hypothesis evidence is immutable'); END;
            CREATE TRIGGER hypothesis_evidence_no_delete BEFORE DELETE ON hypothesis_evidence_reports BEGIN SELECT RAISE(ABORT, 'hypothesis evidence cannot be deleted'); END;
            CREATE TRIGGER hypothesis_test_results_immutable BEFORE UPDATE ON hypothesis_test_results BEGIN SELECT RAISE(ABORT, 'hypothesis test result is immutable'); END;
            CREATE TRIGGER hypothesis_test_results_no_delete BEFORE DELETE ON hypothesis_test_results BEGIN SELECT RAISE(ABORT, 'hypothesis test result cannot be deleted'); END;
            CREATE TRIGGER trade_proposals_content_immutable BEFORE UPDATE ON trade_proposals
            WHEN NEW.trading_bot_id<>OLD.trading_bot_id OR NEW.bot_run_id<>OLD.bot_run_id OR NEW.portfolio_id<>OLD.portfolio_id OR NEW.portfolio_snapshot_id<>OLD.portfolio_snapshot_id OR NEW.configuration_version_id<>OLD.configuration_version_id OR NEW.instrument_id<>OLD.instrument_id OR NEW.proposal_type<>OLD.proposal_type OR NEW.requested_action_json<>OLD.requested_action_json OR NEW.rationale<>OLD.rationale OR NEW.hypothesis_version_id IS NOT OLD.hypothesis_version_id OR NEW.created_at<>OLD.created_at OR NEW.valid_until<>OLD.valid_until OR NEW.idempotency_key<>OLD.idempotency_key
            BEGIN SELECT RAISE(ABORT, 'proposal content is immutable'); END;
            CREATE TRIGGER trade_proposals_no_delete BEFORE DELETE ON trade_proposals BEGIN SELECT RAISE(ABORT, 'proposal cannot be deleted'); END;
            CREATE TRIGGER trade_proposal_evidence_immutable BEFORE UPDATE ON trade_proposal_evidence_reports BEGIN SELECT RAISE(ABORT, 'proposal evidence is immutable'); END;
            CREATE TRIGGER trade_proposal_evidence_no_delete BEFORE DELETE ON trade_proposal_evidence_reports BEGIN SELECT RAISE(ABORT, 'proposal evidence cannot be deleted'); END;
            CREATE TRIGGER guardrail_evaluations_immutable BEFORE UPDATE ON guardrail_evaluations BEGIN SELECT RAISE(ABORT, 'guardrail evaluation is immutable'); END;
            CREATE TRIGGER guardrail_evaluations_no_delete BEFORE DELETE ON guardrail_evaluations BEGIN SELECT RAISE(ABORT, 'guardrail evaluation cannot be deleted'); END;
            CREATE TRIGGER proposal_approvals_immutable BEFORE UPDATE ON proposal_approvals BEGIN SELECT RAISE(ABORT, 'proposal approval is immutable'); END;
            CREATE TRIGGER proposal_approvals_no_delete BEFORE DELETE ON proposal_approvals BEGIN SELECT RAISE(ABORT, 'proposal approval cannot be deleted'); END;
            CREATE TRIGGER capital_reservations_terminal_immutable BEFORE UPDATE ON capital_reservations
            WHEN OLD.status<>'Active' BEGIN SELECT RAISE(ABORT, 'terminal reservation is immutable'); END;
            CREATE TRIGGER capital_reservations_no_delete BEFORE DELETE ON capital_reservations BEGIN SELECT RAISE(ABORT, 'reservation cannot be deleted'); END;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_hypotheses_hypothesis_versions_current_version_id",
            table: "hypotheses");

        migrationBuilder.DropTable(
            name: "capital_reservations");

        migrationBuilder.DropTable(
            name: "guardrail_evaluations");

        migrationBuilder.DropTable(
            name: "hypothesis_evidence_reports");

        migrationBuilder.DropTable(
            name: "hypothesis_test_results");

        migrationBuilder.DropTable(
            name: "proposal_approvals");

        migrationBuilder.DropTable(
            name: "trade_proposal_evidence_reports");

        migrationBuilder.DropTable(
            name: "trade_proposals");

        migrationBuilder.DropTable(
            name: "hypothesis_versions");

        migrationBuilder.DropTable(
            name: "hypotheses");

        migrationBuilder.UpdateData(
            table: "schema_metadata",
            keyColumn: "key",
            keyValue: "application_data_format_version",
            column: "value",
            value: "4");
    }
}
