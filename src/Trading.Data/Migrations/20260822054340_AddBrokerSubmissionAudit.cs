using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AddBrokerSubmissionAudit : Migration
{
    private static readonly string[] OrderStartedColumns = ["order_id", "started_at"];
    private static readonly string[] WorkAttemptColumns = ["work_item_id", "attempt_number"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "broker_submission_attempts",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                order_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                work_item_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                attempt_number = table.Column<int>(type: "INTEGER", nullable: false),
                client_order_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                command_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                adapter_identity = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                environment = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                started_at = table.Column<long>(type: "INTEGER", nullable: false),
                completed_at = table.Column<long>(type: "INTEGER", nullable: false),
                outcome = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                result_code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                broker_order_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                diagnostic_code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                correlation_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_broker_submission_attempts", x => x.id);
                table.CheckConstraint("ck_broker_submission_attempt_broker_id", "(outcome IN ('Accepted','Duplicate') AND broker_order_id IS NOT NULL) OR (outcome NOT IN ('Accepted','Duplicate'))");
                table.CheckConstraint("ck_broker_submission_attempt_hash", "length(command_hash)=64 AND command_hash=lower(command_hash) AND command_hash NOT GLOB '*[^0-9a-f]*'");
                table.CheckConstraint("ck_broker_submission_attempt_number", "attempt_number > 0");
                table.CheckConstraint("ck_broker_submission_attempt_outcome", "outcome IN ('Accepted','Rejected','Unknown','TerminalFailure','Duplicate')");
                table.CheckConstraint("ck_broker_submission_attempt_time", "completed_at >= started_at");
                table.ForeignKey(
                    name: "FK_broker_submission_attempts_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_broker_submission_attempts_outbox_messages_work_item_id",
                    column: x => x.work_item_id,
                    principalTable: "outbox_messages",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_broker_submission_attempts_order_id_started_at",
            table: "broker_submission_attempts",
            columns: OrderStartedColumns);

        migrationBuilder.CreateIndex(
            name: "IX_broker_submission_attempts_work_item_id_attempt_number",
            table: "broker_submission_attempts",
            columns: WorkAttemptColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "broker_submission_attempts");
    }
}
