using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class AddImmutableGuardrailEvaluationArtifacts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_guardrail_evaluation_outcome",
            table: "guardrail_evaluations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_guardrail_evaluation_stage",
            table: "guardrail_evaluations");

        migrationBuilder.AddColumn<string>(
            name: "content_hash",
            table: "guardrail_evaluations",
            type: "TEXT",
            maxLength: 64,
            nullable: true);

        migrationBuilder.Sql("UPDATE guardrail_evaluations SET content_hash = lower(hex(id) || substr(hex(id), 1, 12))");

        migrationBuilder.AlterColumn<string>(
            name: "content_hash",
            table: "guardrail_evaluations",
            type: "TEXT",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_guardrail_evaluations_content_hash",
            table: "guardrail_evaluations",
            column: "content_hash",
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_guardrail_evaluation_hash",
            table: "guardrail_evaluations",
            sql: "length(content_hash) = 64 AND content_hash = lower(content_hash)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_guardrail_evaluation_outcome",
            table: "guardrail_evaluations",
            sql: "outcome IN ('Passed','Failed')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_guardrail_evaluation_stage",
            table: "guardrail_evaluations",
            sql: "evaluation_stage IN ('Initial','ApprovalRevalidation','ReservationRevalidation','Hierarchical')");

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_guardrail_evaluations_content_hash",
            table: "guardrail_evaluations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_guardrail_evaluation_hash",
            table: "guardrail_evaluations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_guardrail_evaluation_outcome",
            table: "guardrail_evaluations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_guardrail_evaluation_stage",
            table: "guardrail_evaluations");

        migrationBuilder.DropColumn(
            name: "content_hash",
            table: "guardrail_evaluations");

        migrationBuilder.AddCheckConstraint(
            name: "ck_guardrail_evaluation_outcome",
            table: "guardrail_evaluations",
            sql: "outcome IN ('Passed','Failed','RequiresApproval')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_guardrail_evaluation_stage",
            table: "guardrail_evaluations",
            sql: "evaluation_stage IN ('Initial','ApprovalRevalidation','ReservationRevalidation')");
    }
}
