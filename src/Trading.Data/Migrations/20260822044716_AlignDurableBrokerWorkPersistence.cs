using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF scaffolding emits inline metadata arrays required by MigrationBuilder.

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AlignDurableBrokerWorkPersistence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS inbox_payload_immutable;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS outbox_payload_immutable;");
        migrationBuilder.Sql("CREATE TEMP TABLE broker_work_upgrade_guard (row_count INTEGER NOT NULL CHECK (row_count = 0));");
        migrationBuilder.Sql("INSERT INTO broker_work_upgrade_guard SELECT (SELECT COUNT(*) FROM inbox_messages) + (SELECT COUNT(*) FROM outbox_messages);");
        migrationBuilder.Sql("DROP TABLE broker_work_upgrade_guard;");

        migrationBuilder.DropIndex(
            name: "IX_outbox_messages_aggregate_type_aggregate_id_message_type",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_outbox_messages_processed_at_available_at",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_hash",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_inbox_messages_source_external_message_id",
            table: "inbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_inbox_messages_status_received_at",
            table: "inbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inbox_hash",
            table: "inbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inbox_status",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "aggregate_id",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "aggregate_type",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "message_type",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "external_message_id",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "message_type",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "source",
            table: "inbox_messages");

        migrationBuilder.RenameColumn(
            name: "processed_at",
            table: "outbox_messages",
            newName: "lease_expires_at");

        migrationBuilder.RenameColumn(
            name: "occurred_at",
            table: "outbox_messages",
            newName: "created_at");

        migrationBuilder.RenameColumn(
            name: "processed_at",
            table: "inbox_messages",
            newName: "lease_expires_at");

        migrationBuilder.AddColumn<long>(
            name: "completed_at",
            table: "outbox_messages",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "correlation_id",
            table: "outbox_messages",
            type: "TEXT",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "idempotency_key",
            table: "outbox_messages",
            type: "TEXT",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "lease_owner",
            table: "outbox_messages",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "order_id",
            table: "outbox_messages",
            type: "TEXT",
            maxLength: 26,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "outbox_messages",
            type: "TEXT",
            maxLength: 16,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "work_kind",
            table: "outbox_messages",
            type: "TEXT",
            maxLength: 32,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "attempt_count",
            table: "inbox_messages",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "available_at",
            table: "inbox_messages",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "completed_at",
            table: "inbox_messages",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "correlation_id",
            table: "inbox_messages",
            type: "TEXT",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "idempotency_key",
            table: "inbox_messages",
            type: "TEXT",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "lease_owner",
            table: "inbox_messages",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_idempotency_key",
            table: "outbox_messages",
            column: "idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_order_id",
            table: "outbox_messages",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_status_available_at_created_at_id",
            table: "outbox_messages",
            columns: new[] { "status", "available_at", "created_at", "id" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_status_lease_expires_at",
            table: "outbox_messages",
            columns: new[] { "status", "lease_expires_at" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_completion",
            table: "outbox_messages",
            sql: "(status IN ('Completed','Failed') AND completed_at IS NOT NULL) OR (status IN ('Pending','Claimed') AND completed_at IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_hash",
            table: "outbox_messages",
            sql: "length(payload_hash)=64 AND payload_hash=lower(payload_hash) AND payload_hash NOT GLOB '*[^0-9a-f]*'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_lease",
            table: "outbox_messages",
            sql: "(status='Claimed' AND lease_owner IS NOT NULL AND lease_expires_at IS NOT NULL) OR (status<>'Claimed' AND lease_owner IS NULL AND lease_expires_at IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_status",
            table: "outbox_messages",
            sql: "status IN ('Pending','Claimed','Completed','Failed')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_times",
            table: "outbox_messages",
            sql: "available_at >= created_at AND (completed_at IS NULL OR completed_at >= created_at)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_work_kind",
            table: "outbox_messages",
            sql: "work_kind IN ('Submit','Reconcile','Cancel','ApplyBrokerEvent')");

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_idempotency_key",
            table: "inbox_messages",
            column: "idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_status_available_at_received_at_id",
            table: "inbox_messages",
            columns: new[] { "status", "available_at", "received_at", "id" });

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_status_lease_expires_at",
            table: "inbox_messages",
            columns: new[] { "status", "lease_expires_at" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_inbox_attempt_count",
            table: "inbox_messages",
            sql: "attempt_count >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_inbox_completion",
            table: "inbox_messages",
            sql: "(status IN ('Completed','Failed') AND completed_at IS NOT NULL) OR (status IN ('Pending','Claimed') AND completed_at IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_inbox_hash",
            table: "inbox_messages",
            sql: "length(payload_hash)=64 AND payload_hash=lower(payload_hash) AND payload_hash NOT GLOB '*[^0-9a-f]*'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_inbox_lease",
            table: "inbox_messages",
            sql: "(status='Claimed' AND lease_owner IS NOT NULL AND lease_expires_at IS NOT NULL) OR (status<>'Claimed' AND lease_owner IS NULL AND lease_expires_at IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_inbox_status",
            table: "inbox_messages",
            sql: "status IN ('Pending','Claimed','Completed','Failed')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_inbox_times",
            table: "inbox_messages",
            sql: "available_at >= received_at AND (completed_at IS NULL OR completed_at >= received_at)");

        migrationBuilder.AddForeignKey(
            name: "FK_outbox_messages_orders_order_id",
            table: "outbox_messages",
            column: "order_id",
            principalTable: "orders",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_outbox_messages_orders_order_id",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_outbox_messages_idempotency_key",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_outbox_messages_order_id",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_outbox_messages_status_available_at_created_at_id",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_outbox_messages_status_lease_expires_at",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_completion",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_hash",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_lease",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_status",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_times",
            table: "outbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_work_kind",
            table: "outbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_inbox_messages_idempotency_key",
            table: "inbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_inbox_messages_status_available_at_received_at_id",
            table: "inbox_messages");

        migrationBuilder.DropIndex(
            name: "IX_inbox_messages_status_lease_expires_at",
            table: "inbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inbox_attempt_count",
            table: "inbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inbox_completion",
            table: "inbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inbox_hash",
            table: "inbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inbox_lease",
            table: "inbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inbox_status",
            table: "inbox_messages");

        migrationBuilder.DropCheckConstraint(
            name: "ck_inbox_times",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "completed_at",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "correlation_id",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "idempotency_key",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "lease_owner",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "order_id",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "status",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "work_kind",
            table: "outbox_messages");

        migrationBuilder.DropColumn(
            name: "attempt_count",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "available_at",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "completed_at",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "correlation_id",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "idempotency_key",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "lease_owner",
            table: "inbox_messages");

        migrationBuilder.RenameColumn(
            name: "lease_expires_at",
            table: "outbox_messages",
            newName: "processed_at");

        migrationBuilder.RenameColumn(
            name: "created_at",
            table: "outbox_messages",
            newName: "occurred_at");

        migrationBuilder.RenameColumn(
            name: "lease_expires_at",
            table: "inbox_messages",
            newName: "processed_at");

        migrationBuilder.AddColumn<string>(
            name: "aggregate_id",
            table: "outbox_messages",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "aggregate_type",
            table: "outbox_messages",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "message_type",
            table: "outbox_messages",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "external_message_id",
            table: "inbox_messages",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "message_type",
            table: "inbox_messages",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "source",
            table: "inbox_messages",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_aggregate_type_aggregate_id_message_type",
            table: "outbox_messages",
            columns: new[] { "aggregate_type", "aggregate_id", "message_type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_processed_at_available_at",
            table: "outbox_messages",
            columns: new[] { "processed_at", "available_at" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_hash",
            table: "outbox_messages",
            sql: "length(payload_hash)=64 AND payload_hash=lower(payload_hash)");

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_source_external_message_id",
            table: "inbox_messages",
            columns: new[] { "source", "external_message_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_status_received_at",
            table: "inbox_messages",
            columns: new[] { "status", "received_at" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_inbox_hash",
            table: "inbox_messages",
            sql: "length(payload_hash)=64 AND payload_hash=lower(payload_hash)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_inbox_status",
            table: "inbox_messages",
            sql: "status IN ('Pending','Processing','Processed','Deferred','Failed')");
    }
}
