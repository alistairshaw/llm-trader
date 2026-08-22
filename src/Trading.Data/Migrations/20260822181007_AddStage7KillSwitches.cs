using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AddStage7KillSwitches : Migration
{
    private static readonly string[] ScopeVersionColumns = ["scope_kind", "scope_id", "version"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            table: "schema_metadata",
            keyColumn: "key",
            keyValue: "application_data_format_version",
            column: "value",
            value: "7");

        migrationBuilder.CreateTable(
            name: "kill_switch_history",
            columns: table => new
            {
                id = table.Column<string>(type: "TEXT", nullable: false),
                idempotency_key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                scope_kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                scope_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                prior_state = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                resulting_state = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                actor_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                confirmation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                changed_at = table.Column<long>(type: "INTEGER", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_kill_switch_history", x => x.id);
                table.CheckConstraint("ck_kill_switch_history_scope", "scope_kind IN ('Platform','BrokerAccount','Portfolio','TradingBot')");
                table.CheckConstraint("ck_kill_switch_history_state", "prior_state IN ('Clear','Active') AND resulting_state IN ('Clear','Active')");
                table.CheckConstraint("ck_kill_switch_history_version", "version > 0");
            });

        migrationBuilder.CreateTable(
            name: "kill_switches",
            columns: table => new
            {
                scope_kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                scope_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                state = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                actor_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                confirmation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                changed_at = table.Column<long>(type: "INTEGER", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_kill_switches", x => new { x.scope_kind, x.scope_id });
                table.CheckConstraint("ck_kill_switch_scope", "scope_kind IN ('Platform','BrokerAccount','Portfolio','TradingBot')");
                table.CheckConstraint("ck_kill_switch_state", "state IN ('Clear','Active')");
                table.CheckConstraint("ck_kill_switch_version", "version > 0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_kill_switch_history_idempotency_key",
            table: "kill_switch_history",
            column: "idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_kill_switch_history_scope_kind_scope_id_version",
            table: "kill_switch_history",
            columns: ScopeVersionColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "kill_switch_history");

        migrationBuilder.DropTable(
            name: "kill_switches");

        migrationBuilder.UpdateData(
            table: "schema_metadata",
            keyColumn: "key",
            keyValue: "application_data_format_version",
            column: "value",
            value: "6");
    }
}
