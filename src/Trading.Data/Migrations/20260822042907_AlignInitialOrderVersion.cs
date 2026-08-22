using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AlignInitialOrderVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite rebuilds orders while replacing its check constraint. Drop every
        // application trigger attached to, or referring to, that table first.
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS fills_account_consistent_insert;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS orders_execution_identity_immutable;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS orders_ownership_consistent_insert;");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_version",
            table: "orders");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_version",
            table: "orders",
            sql: "version >= 0");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS fills_account_consistent_insert;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS orders_execution_identity_immutable;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS orders_ownership_consistent_insert;");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_version",
            table: "orders");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_version",
            table: "orders",
            sql: "version > 0");

        CreateOrderIntegrityTriggers(migrationBuilder);
    }

    private static void CreateOrderIntegrityTriggers(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TRIGGER orders_ownership_consistent_insert BEFORE INSERT ON orders
            WHEN NOT EXISTS (SELECT 1 FROM portfolios p WHERE p.id=NEW.portfolio_id AND p.broker_account_id=NEW.broker_account_id)
              OR NOT EXISTS (SELECT 1 FROM trade_proposals p WHERE p.id=NEW.trade_proposal_id AND p.portfolio_id=NEW.portfolio_id AND p.instrument_id=NEW.instrument_id)
              OR (NEW.capital_reservation_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM capital_reservations r WHERE r.id=NEW.capital_reservation_id AND r.portfolio_id=NEW.portfolio_id AND r.trade_proposal_id=NEW.trade_proposal_id))
            BEGIN SELECT RAISE(ABORT, 'order ownership is inconsistent'); END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER orders_execution_identity_immutable BEFORE UPDATE ON orders
            WHEN OLD.client_order_id<>NEW.client_order_id OR OLD.portfolio_id<>NEW.portfolio_id OR OLD.broker_account_id<>NEW.broker_account_id
              OR OLD.trade_proposal_id<>NEW.trade_proposal_id OR OLD.instrument_id<>NEW.instrument_id OR OLD.side<>NEW.side
              OR OLD.quantity<>NEW.quantity OR OLD.quantity_unit<>NEW.quantity_unit OR OLD.currency<>NEW.currency
              OR OLD.order_type<>NEW.order_type OR OLD.limit_price IS NOT NEW.limit_price
              OR OLD.time_in_force<>NEW.time_in_force OR OLD.correlation_id<>NEW.correlation_id
            BEGIN SELECT RAISE(ABORT, 'order execution identity is immutable'); END;
            """);
        migrationBuilder.Sql("CREATE TRIGGER fills_account_consistent_insert BEFORE INSERT ON fills WHEN NOT EXISTS (SELECT 1 FROM orders o WHERE o.id=NEW.order_id AND o.broker_account_id=NEW.broker_account_id) BEGIN SELECT RAISE(ABORT, 'fill account is inconsistent'); END;");
    }
}
