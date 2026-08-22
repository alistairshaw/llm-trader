using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#pragma warning disable CA1861 // Static migration metadata arrays are intentionally local.

namespace Trading.Data.Migrations;

[DbContext(typeof(TradingDbContext))]
[Migration("20260822034600_AddStage6ExecutionIntegrityTriggers")]
public sealed class AddStage6ExecutionIntegrityTriggers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
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
              OR OLD.quantity<>NEW.quantity OR OLD.order_type<>NEW.order_type OR OLD.limit_price IS NOT NEW.limit_price
              OR OLD.time_in_force<>NEW.time_in_force OR OLD.correlation_id<>NEW.correlation_id
            BEGIN SELECT RAISE(ABORT, 'order execution identity is immutable'); END;
            """);
        migrationBuilder.Sql("CREATE TRIGGER fills_account_consistent_insert BEFORE INSERT ON fills WHEN NOT EXISTS (SELECT 1 FROM orders o WHERE o.id=NEW.order_id AND o.broker_account_id=NEW.broker_account_id) BEGIN SELECT RAISE(ABORT, 'fill account is inconsistent'); END;");
        foreach (var table in new[] { "order_transitions", "fills", "broker_reconciliations" })
        {
            migrationBuilder.Sql($"CREATE TRIGGER {table}_immutable_update BEFORE UPDATE ON {table} BEGIN SELECT RAISE(ABORT, '{table} facts are immutable'); END;");
            migrationBuilder.Sql($"CREATE TRIGGER {table}_immutable_delete BEFORE DELETE ON {table} BEGIN SELECT RAISE(ABORT, '{table} facts are immutable'); END;");
        }
        migrationBuilder.Sql("CREATE TRIGGER inbox_payload_immutable BEFORE UPDATE ON inbox_messages WHEN OLD.source<>NEW.source OR OLD.external_message_id<>NEW.external_message_id OR OLD.message_type<>NEW.message_type OR OLD.received_at<>NEW.received_at OR OLD.payload_json<>NEW.payload_json OR OLD.payload_hash<>NEW.payload_hash BEGIN SELECT RAISE(ABORT, 'inbox source facts are immutable'); END;");
        migrationBuilder.Sql("CREATE TRIGGER outbox_payload_immutable BEFORE UPDATE ON outbox_messages WHEN OLD.message_type<>NEW.message_type OR OLD.aggregate_type<>NEW.aggregate_type OR OLD.aggregate_id<>NEW.aggregate_id OR OLD.payload_json<>NEW.payload_json OR OLD.payload_hash<>NEW.payload_hash OR OLD.occurred_at<>NEW.occurred_at BEGIN SELECT RAISE(ABORT, 'outbox source facts are immutable'); END;");
        migrationBuilder.Sql("CREATE TRIGGER capital_reservations_terminal_immutable BEFORE UPDATE ON capital_reservations WHEN OLD.status<>'Active' BEGIN SELECT RAISE(ABORT, 'terminal reservation is immutable'); END;");
        migrationBuilder.Sql("CREATE TRIGGER capital_reservations_no_delete BEFORE DELETE ON capital_reservations BEGIN SELECT RAISE(ABORT, 'reservation cannot be deleted'); END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var trigger in new[] { "capital_reservations_no_delete", "capital_reservations_terminal_immutable", "outbox_payload_immutable", "inbox_payload_immutable", "broker_reconciliations_immutable_delete", "broker_reconciliations_immutable_update", "fills_immutable_delete", "fills_immutable_update", "order_transitions_immutable_delete", "order_transitions_immutable_update", "fills_account_consistent_insert", "orders_execution_identity_immutable", "orders_ownership_consistent_insert" })
            migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger};");
    }
}
