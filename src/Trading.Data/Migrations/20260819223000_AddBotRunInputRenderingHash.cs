using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Data.Migrations;

[DbContext(typeof(TradingDbContext))]
[Migration("20260819223000_AddBotRunInputRenderingHash")]
public sealed class AddBotRunInputRenderingHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "input_rendering_hash", table: "bot_runs", type: "TEXT", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "input_rendering_hash", table: "bot_runs");
}
