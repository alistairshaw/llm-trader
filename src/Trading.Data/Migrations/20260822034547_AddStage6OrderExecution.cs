using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF scaffolding emits inline metadata arrays required by MigrationBuilder.

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AddStage6OrderExecution : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "broker_reconciliations",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                broker_account_id = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                started_at = table.Column<long>(type: "INTEGER", nullable: false),
                completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                broker_snapshot_json = table.Column<string>(type: "TEXT", nullable: false),
                differences_json = table.Column<string>(type: "TEXT", nullable: false),
                resolution_json = table.Column<string>(type: "TEXT", nullable: false),
                correlation_id = table.Column<string>(type: "TEXT", nullable: false),
                content_hash = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_broker_reconciliations", x => x.id);
                table.CheckConstraint("ck_broker_reconciliations_hash", "length(content_hash)=64 AND content_hash=lower(content_hash)");
                table.CheckConstraint("ck_broker_reconciliations_status", "status IN ('Pending','Matched','Discrepancy','Failed')");
                table.ForeignKey(
                    name: "FK_broker_reconciliations_broker_accounts_broker_account_id",
                    column: x => x.broker_account_id,
                    principalTable: "broker_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                source = table.Column<string>(type: "TEXT", nullable: false),
                external_message_id = table.Column<string>(type: "TEXT", nullable: false),
                message_type = table.Column<string>(type: "TEXT", nullable: false),
                received_at = table.Column<long>(type: "INTEGER", nullable: false),
                processed_at = table.Column<long>(type: "INTEGER", nullable: true),
                status = table.Column<string>(type: "TEXT", nullable: false),
                payload_json = table.Column<string>(type: "TEXT", nullable: false),
                payload_hash = table.Column<string>(type: "TEXT", nullable: false),
                last_error = table.Column<string>(type: "TEXT", nullable: true),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inbox_messages", x => x.id);
                table.CheckConstraint("ck_inbox_hash", "length(payload_hash)=64 AND payload_hash=lower(payload_hash)");
                table.CheckConstraint("ck_inbox_status", "status IN ('Pending','Processing','Processed','Deferred','Failed')");
                table.CheckConstraint("ck_inbox_version", "version > 0");
            });

        migrationBuilder.CreateTable(
            name: "orders",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                client_order_id = table.Column<string>(type: "TEXT", nullable: false),
                portfolio_id = table.Column<string>(type: "TEXT", nullable: false),
                broker_account_id = table.Column<string>(type: "TEXT", nullable: false),
                trade_proposal_id = table.Column<string>(type: "TEXT", nullable: false),
                capital_reservation_id = table.Column<string>(type: "TEXT", nullable: true),
                instrument_id = table.Column<string>(type: "TEXT", nullable: false),
                side = table.Column<string>(type: "TEXT", nullable: false),
                quantity = table.Column<string>(type: "TEXT", nullable: false),
                order_type = table.Column<string>(type: "TEXT", nullable: false),
                limit_price = table.Column<string>(type: "TEXT", nullable: true),
                time_in_force = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                broker_order_id = table.Column<string>(type: "TEXT", nullable: true),
                correlation_id = table.Column<string>(type: "TEXT", nullable: false),
                created_at = table.Column<long>(type: "INTEGER", nullable: false),
                submitted_at = table.Column<long>(type: "INTEGER", nullable: true),
                completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_orders", x => x.id);
                table.CheckConstraint("ck_orders_limit", "(order_type='Market' AND limit_price IS NULL) OR (order_type='Limit' AND CAST(limit_price AS NUMERIC) > 0)");
                table.CheckConstraint("ck_orders_quantity", "CAST(quantity AS NUMERIC) > 0");
                table.CheckConstraint("ck_orders_side", "side IN ('Buy','Sell')");
                table.CheckConstraint("ck_orders_status", "status IN ('PendingSubmission','Submitting','SubmissionUnknown','Submitted','PartiallyFilled','Filled','Rejected','CancelPending','Cancelled','Expired','Failed')");
                table.CheckConstraint("ck_orders_time_in_force", "time_in_force IN ('Day','GoodTilCancelled')");
                table.CheckConstraint("ck_orders_type", "order_type IN ('Market','Limit')");
                table.CheckConstraint("ck_orders_version", "version > 0");
                table.ForeignKey(
                    name: "FK_orders_broker_accounts_broker_account_id",
                    column: x => x.broker_account_id,
                    principalTable: "broker_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_orders_instruments_instrument_id",
                    column: x => x.instrument_id,
                    principalTable: "instruments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_orders_portfolios_portfolio_id",
                    column: x => x.portfolio_id,
                    principalTable: "portfolios",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_orders_trade_proposals_trade_proposal_id",
                    column: x => x.trade_proposal_id,
                    principalTable: "trade_proposals",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                message_type = table.Column<string>(type: "TEXT", nullable: false),
                aggregate_type = table.Column<string>(type: "TEXT", nullable: false),
                aggregate_id = table.Column<string>(type: "TEXT", nullable: false),
                payload_json = table.Column<string>(type: "TEXT", nullable: false),
                payload_hash = table.Column<string>(type: "TEXT", nullable: false),
                occurred_at = table.Column<long>(type: "INTEGER", nullable: false),
                available_at = table.Column<long>(type: "INTEGER", nullable: false),
                processed_at = table.Column<long>(type: "INTEGER", nullable: true),
                attempt_count = table.Column<int>(type: "INTEGER", nullable: false),
                last_error = table.Column<string>(type: "TEXT", nullable: true),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.id);
                table.CheckConstraint("ck_outbox_attempt_count", "attempt_count >= 0");
                table.CheckConstraint("ck_outbox_hash", "length(payload_hash)=64 AND payload_hash=lower(payload_hash)");
                table.CheckConstraint("ck_outbox_version", "version > 0");
            });

        migrationBuilder.CreateTable(
            name: "fills",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                order_id = table.Column<string>(type: "TEXT", nullable: false),
                broker_account_id = table.Column<string>(type: "TEXT", nullable: false),
                broker_execution_id = table.Column<string>(type: "TEXT", nullable: false),
                quantity = table.Column<string>(type: "TEXT", nullable: false),
                price = table.Column<string>(type: "TEXT", nullable: false),
                currency = table.Column<string>(type: "TEXT", nullable: false),
                fee_amount = table.Column<string>(type: "TEXT", nullable: false),
                fee_currency = table.Column<string>(type: "TEXT", nullable: false),
                executed_at = table.Column<long>(type: "INTEGER", nullable: false),
                received_at = table.Column<long>(type: "INTEGER", nullable: false),
                raw_payload_reference = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_fills", x => x.id);
                table.CheckConstraint("ck_fills_fee", "CAST(fee_amount AS NUMERIC) >= 0");
                table.CheckConstraint("ck_fills_price", "CAST(price AS NUMERIC) > 0");
                table.CheckConstraint("ck_fills_quantity", "CAST(quantity AS NUMERIC) > 0");
                table.ForeignKey(
                    name: "FK_fills_broker_accounts_broker_account_id",
                    column: x => x.broker_account_id,
                    principalTable: "broker_accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_fills_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_transitions",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                order_id = table.Column<string>(type: "TEXT", nullable: false),
                sequence_number = table.Column<int>(type: "INTEGER", nullable: false),
                previous_status = table.Column<string>(type: "TEXT", nullable: false),
                new_status = table.Column<string>(type: "TEXT", nullable: false),
                reason_code = table.Column<string>(type: "TEXT", nullable: false),
                reason_detail = table.Column<string>(type: "TEXT", nullable: true),
                source = table.Column<string>(type: "TEXT", nullable: false),
                occurred_at = table.Column<long>(type: "INTEGER", nullable: false),
                received_at = table.Column<long>(type: "INTEGER", nullable: false),
                correlation_id = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_transitions", x => x.id);
                table.CheckConstraint("ck_order_transitions_sequence", "sequence_number > 0");
                table.ForeignKey(
                    name: "FK_order_transitions_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.UpdateData(
            table: "schema_metadata",
            keyColumn: "key",
            keyValue: "application_data_format_version",
            column: "value",
            value: "6");

        migrationBuilder.CreateIndex(
            name: "IX_capital_reservations_order_id",
            table: "capital_reservations",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "IX_broker_reconciliations_broker_account_id_started_at",
            table: "broker_reconciliations",
            columns: new[] { "broker_account_id", "started_at" });

        migrationBuilder.CreateIndex(
            name: "IX_broker_reconciliations_correlation_id",
            table: "broker_reconciliations",
            column: "correlation_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_fills_broker_account_id_broker_execution_id",
            table: "fills",
            columns: new[] { "broker_account_id", "broker_execution_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_fills_order_id_executed_at",
            table: "fills",
            columns: new[] { "order_id", "executed_at" });

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_source_external_message_id",
            table: "inbox_messages",
            columns: new[] { "source", "external_message_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_status_received_at",
            table: "inbox_messages",
            columns: new[] { "status", "received_at" });

        migrationBuilder.CreateIndex(
            name: "IX_order_transitions_correlation_id",
            table: "order_transitions",
            column: "correlation_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_transitions_order_id_sequence_number",
            table: "order_transitions",
            columns: new[] { "order_id", "sequence_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_orders_broker_account_id_broker_order_id",
            table: "orders",
            columns: new[] { "broker_account_id", "broker_order_id" },
            unique: true,
            filter: "broker_order_id IS NOT NULL");


        migrationBuilder.CreateIndex(
            name: "IX_orders_client_order_id",
            table: "orders",
            column: "client_order_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_orders_correlation_id",
            table: "orders",
            column: "correlation_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_orders_instrument_id",
            table: "orders",
            column: "instrument_id");

        migrationBuilder.CreateIndex(
            name: "IX_orders_portfolio_id_status_created_at",
            table: "orders",
            columns: new[] { "portfolio_id", "status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "IX_orders_trade_proposal_id",
            table: "orders",
            column: "trade_proposal_id");

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_aggregate_type_aggregate_id_message_type",
            table: "outbox_messages",
            columns: new[] { "aggregate_type", "aggregate_id", "message_type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_processed_at_available_at",
            table: "outbox_messages",
            columns: new[] { "processed_at", "available_at" });

        migrationBuilder.AddForeignKey(
            name: "FK_capital_reservations_orders_order_id",
            table: "capital_reservations",
            column: "order_id",
            principalTable: "orders",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_capital_reservations_orders_order_id",
            table: "capital_reservations");

        migrationBuilder.DropTable(
            name: "broker_reconciliations");

        migrationBuilder.DropTable(
            name: "fills");

        migrationBuilder.DropTable(
            name: "inbox_messages");

        migrationBuilder.DropTable(
            name: "order_transitions");

        migrationBuilder.DropTable(
            name: "outbox_messages");

        migrationBuilder.DropTable(
            name: "orders");

        migrationBuilder.DropIndex(
            name: "IX_capital_reservations_order_id",
            table: "capital_reservations");

        migrationBuilder.UpdateData(
            table: "schema_metadata",
            keyColumn: "key",
            keyValue: "application_data_format_version",
            column: "value",
            value: "5");
    }
}
