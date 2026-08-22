using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class RestoreDurableBrokerWorkTriggers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE TRIGGER inbox_payload_immutable BEFORE UPDATE ON inbox_messages WHEN OLD.id<>NEW.id OR OLD.idempotency_key<>NEW.idempotency_key OR OLD.correlation_id<>NEW.correlation_id OR OLD.received_at<>NEW.received_at OR OLD.payload_json<>NEW.payload_json OR OLD.payload_hash<>NEW.payload_hash BEGIN SELECT RAISE(ABORT, 'inbox source facts are immutable'); END;");
        migrationBuilder.Sql("CREATE TRIGGER outbox_payload_immutable BEFORE UPDATE ON outbox_messages WHEN OLD.id<>NEW.id OR OLD.order_id<>NEW.order_id OR OLD.work_kind<>NEW.work_kind OR OLD.idempotency_key<>NEW.idempotency_key OR OLD.correlation_id<>NEW.correlation_id OR OLD.created_at<>NEW.created_at OR OLD.payload_json<>NEW.payload_json OR OLD.payload_hash<>NEW.payload_hash BEGIN SELECT RAISE(ABORT, 'outbox source facts are immutable'); END;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS inbox_payload_immutable;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS outbox_payload_immutable;");
    }
}
