using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AlignOrderPersistenceContract : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop every application trigger attached to, or referring to, a table SQLite will rebuild.
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS fills_account_consistent_insert;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS orders_ownership_consistent_insert;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS orders_execution_identity_immutable;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS order_transitions_immutable_update;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS order_transitions_immutable_delete;");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_status",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_time_in_force",
            table: "orders");

        migrationBuilder.AddColumn<string>(
            name: "currency",
            table: "orders",
            type: "TEXT",
            maxLength: 3,
            nullable: false);

        migrationBuilder.AddColumn<string>(
            name: "quantity_unit",
            table: "orders",
            type: "TEXT",
            maxLength: 32,
            nullable: false);

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_currency",
            table: "orders",
            sql: "length(currency)=3 AND currency NOT GLOB '*[^A-Z]*'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_quantity_unit",
            table: "orders",
            sql: "length(quantity_unit) BETWEEN 1 AND 32 AND quantity_unit NOT GLOB '*[^a-z]*'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_status",
            table: "orders",
            sql: "status IN ('Created','Submitting','Submitted','Acknowledged','PartiallyFilled','Filled','CancelPending','Cancelled','Rejected','Expired','Unknown')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_time_in_force",
            table: "orders",
            sql: "time_in_force IN ('Day','GoodTillCancelled','ImmediateOrCancel','FillOrKill')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_order_transitions_new_status",
            table: "order_transitions",
            sql: "new_status IN ('Created','Submitting','Submitted','Acknowledged','PartiallyFilled','Filled','CancelPending','Cancelled','Rejected','Expired','Unknown')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_order_transitions_previous_status",
            table: "order_transitions",
            sql: "previous_status IN ('Created','Submitting','Submitted','Acknowledged','PartiallyFilled','Filled','CancelPending','Cancelled','Rejected','Expired','Unknown')");

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_currency",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_quantity_unit",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_status",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_time_in_force",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_order_transitions_new_status",
            table: "order_transitions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_order_transitions_previous_status",
            table: "order_transitions");

        migrationBuilder.DropColumn(
            name: "currency",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "quantity_unit",
            table: "orders");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_status",
            table: "orders",
            sql: "status IN ('PendingSubmission','Submitting','SubmissionUnknown','Submitted','PartiallyFilled','Filled','Rejected','CancelPending','Cancelled','Expired','Failed')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_time_in_force",
            table: "orders",
            sql: "time_in_force IN ('Day','GoodTilCancelled')");
    }
}
