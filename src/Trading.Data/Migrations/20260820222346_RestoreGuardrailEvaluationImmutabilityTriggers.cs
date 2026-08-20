using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading.Data.Migrations;

/// <inheritdoc />
public partial class RestoreGuardrailEvaluationImmutabilityTriggers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS guardrail_evaluations_immutable;");
        migrationBuilder.Sql("CREATE TRIGGER trg_guardrail_evaluations_immutable_update BEFORE UPDATE ON guardrail_evaluations BEGIN SELECT RAISE(ABORT, 'guardrail evaluations are immutable'); END;");
        migrationBuilder.Sql("CREATE TRIGGER trg_guardrail_evaluations_immutable_delete BEFORE DELETE ON guardrail_evaluations BEGIN SELECT RAISE(ABORT, 'guardrail evaluations are immutable'); END;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_guardrail_evaluations_immutable_update;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_guardrail_evaluations_immutable_delete;");
        migrationBuilder.Sql("CREATE TRIGGER guardrail_evaluations_immutable BEFORE UPDATE ON guardrail_evaluations BEGIN SELECT RAISE(ABORT, 'guardrail evaluation is immutable'); END;");
    }
}
