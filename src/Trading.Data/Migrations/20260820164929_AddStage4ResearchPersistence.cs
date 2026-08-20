using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF scaffolding emits inline metadata arrays required by MigrationBuilder.

namespace Trading.Data.Migrations;

    /// <inheritdoc />
    public partial class AddStage4ResearchPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "research_report_sources",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    research_report_id = table.Column<string>(type: "TEXT", nullable: false),
                    source_sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    source_type = table.Column<string>(type: "TEXT", nullable: false),
                    source_uri = table.Column<string>(type: "TEXT", nullable: true),
                    stable_source_id = table.Column<string>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    publisher = table.Column<string>(type: "TEXT", nullable: true),
                    published_at = table.Column<long>(type: "INTEGER", nullable: true),
                    retrieved_at = table.Column<long>(type: "INTEGER", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false),
                    metadata_json = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_report_sources", x => x.id);
                    table.CheckConstraint("ck_research_report_sources_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)");
                    table.CheckConstraint("ck_research_report_sources_sequence", "source_sequence > 0");
                });

            migrationBuilder.CreateTable(
                name: "research_reports",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    report_series_id = table.Column<string>(type: "TEXT", nullable: false),
                    version_number = table.Column<int>(type: "INTEGER", nullable: false),
                    research_request_id = table.Column<string>(type: "TEXT", nullable: false),
                    research_run_id = table.Column<string>(type: "TEXT", nullable: false),
                    subject_type = table.Column<string>(type: "TEXT", nullable: false),
                    subject_id = table.Column<string>(type: "TEXT", nullable: true),
                    question = table.Column<string>(type: "TEXT", nullable: false),
                    visibility = table.Column<string>(type: "TEXT", nullable: false),
                    data_cutoff = table.Column<long>(type: "INTEGER", nullable: false),
                    generated_at = table.Column<long>(type: "INTEGER", nullable: false),
                    expires_at = table.Column<long>(type: "INTEGER", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    supersedes_report_id = table.Column<string>(type: "TEXT", nullable: true),
                    report_schema_version = table.Column<string>(type: "TEXT", nullable: false),
                    content_json = table.Column<string>(type: "TEXT", nullable: false),
                    content_markdown = table.Column<string>(type: "TEXT", nullable: true),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false),
                    generator_metadata_json = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_reports", x => x.id);
                    table.CheckConstraint("ck_research_reports_hash", "length(content_hash) = 64 AND content_hash = lower(content_hash)");
                    table.CheckConstraint("ck_research_reports_status", "status IN ('Published','Expired','Superseded','Retracted')");
                    table.CheckConstraint("ck_research_reports_version", "version_number > 0");
                    table.CheckConstraint("ck_research_reports_visibility", "visibility IN ('Shared','BotPrivate','Restricted')");
                    table.ForeignKey(
                        name: "FK_research_reports_research_reports_supersedes_report_id",
                        column: x => x.supersedes_report_id,
                        principalTable: "research_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "research_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    subject_type = table.Column<string>(type: "TEXT", nullable: false),
                    subject_id = table.Column<string>(type: "TEXT", nullable: true),
                    question = table.Column<string>(type: "TEXT", nullable: false),
                    normalized_research_key = table.Column<string>(type: "TEXT", nullable: false),
                    as_of = table.Column<long>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    visibility = table.Column<string>(type: "TEXT", nullable: false),
                    requesting_bot_id = table.Column<string>(type: "TEXT", nullable: true),
                    freshness_requirement_json = table.Column<string>(type: "TEXT", nullable: false),
                    request_json = table.Column<string>(type: "TEXT", nullable: false),
                    started_at = table.Column<long>(type: "INTEGER", nullable: true),
                    completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                    result_report_id = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_requests", x => x.id);
                    table.CheckConstraint("ck_research_requests_status", "status IN ('Requested','Validating','Queued','Running','Completed','Failed','TimedOut','BudgetExceeded','Cancelled')");
                    table.CheckConstraint("ck_research_requests_version", "version > 0");
                    table.CheckConstraint("ck_research_requests_visibility", "visibility IN ('Shared','BotPrivate','Restricted')");
                    table.ForeignKey(
                        name: "FK_research_requests_research_reports_result_report_id",
                        column: x => x.result_report_id,
                        principalTable: "research_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_research_requests_trading_bots_requesting_bot_id",
                        column: x => x.requesting_bot_id,
                        principalTable: "trading_bots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "research_runs",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    research_request_id = table.Column<string>(type: "TEXT", nullable: false),
                    attempt_number = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    model_configuration_json = table.Column<string>(type: "TEXT", nullable: false),
                    prompt_version = table.Column<string>(type: "TEXT", nullable: false),
                    tool_set_version = table.Column<string>(type: "TEXT", nullable: false),
                    report_schema_version = table.Column<string>(type: "TEXT", nullable: false),
                    started_at = table.Column<long>(type: "INTEGER", nullable: false),
                    completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                    terminal_reason = table.Column<string>(type: "TEXT", nullable: true),
                    usage_json = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_runs", x => x.id);
                    table.CheckConstraint("ck_research_runs_attempt", "attempt_number > 0");
                    table.CheckConstraint("ck_research_runs_status", "status IN ('Pending','Running','WaitingForTool','Completed','Failed','TimedOut','BudgetExceeded','Cancelled')");
                    table.CheckConstraint("ck_research_runs_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_research_runs_research_requests_research_request_id",
                        column: x => x.research_request_id,
                        principalTable: "research_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "research_subscriptions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    research_request_id = table.Column<string>(type: "TEXT", nullable: false),
                    trading_bot_id = table.Column<string>(type: "TEXT", nullable: false),
                    subscribed_at = table.Column<long>(type: "INTEGER", nullable: false),
                    notification_status = table.Column<string>(type: "TEXT", nullable: false),
                    notified_at = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_subscriptions", x => x.id);
                    table.CheckConstraint("ck_research_subscriptions_notification", "notification_status IN ('Pending','Delivered','Failed')");
                    table.ForeignKey(
                        name: "FK_research_subscriptions_research_requests_research_request_id",
                        column: x => x.research_request_id,
                        principalTable: "research_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_research_subscriptions_trading_bots_trading_bot_id",
                        column: x => x.trading_bot_id,
                        principalTable: "trading_bots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "research_tool_invocations",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    research_run_id = table.Column<string>(type: "TEXT", nullable: false),
                    sequence_number = table.Column<int>(type: "INTEGER", nullable: false),
                    tool_name = table.Column<string>(type: "TEXT", nullable: false),
                    tool_schema_version = table.Column<int>(type: "INTEGER", nullable: false),
                    arguments_json = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    started_at = table.Column<long>(type: "INTEGER", nullable: false),
                    completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                    result_json = table.Column<string>(type: "TEXT", nullable: true),
                    result_artifact_id = table.Column<string>(type: "TEXT", nullable: true),
                    error_code = table.Column<string>(type: "TEXT", nullable: true),
                    error_detail = table.Column<string>(type: "TEXT", nullable: true),
                    usage_json = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_tool_invocations", x => x.id);
                    table.CheckConstraint("ck_research_tool_invocations_schema", "tool_schema_version > 0");
                    table.CheckConstraint("ck_research_tool_invocations_sequence", "sequence_number > 0");
                    table.CheckConstraint("ck_research_tool_invocations_status", "status IN ('Started','Succeeded','Failed','Rejected','Cancelled')");
                    table.ForeignKey(
                        name: "FK_research_tool_invocations_research_runs_research_run_id",
                        column: x => x.research_run_id,
                        principalTable: "research_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "schema_metadata",
                keyColumn: "key",
                keyValue: "application_data_format_version",
                column: "value",
                value: "4");

            migrationBuilder.CreateIndex(
                name: "IX_research_report_sources_research_report_id_source_sequence",
                table: "research_report_sources",
                columns: new[] { "research_report_id", "source_sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_research_reports_report_series_id_content_hash",
                table: "research_reports",
                columns: new[] { "report_series_id", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_research_reports_report_series_id_version_number",
                table: "research_reports",
                columns: new[] { "report_series_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_research_reports_research_request_id",
                table: "research_reports",
                column: "research_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_research_reports_research_run_id",
                table: "research_reports",
                column: "research_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_research_reports_subject_id_generated_at",
                table: "research_reports",
                columns: new[] { "subject_id", "generated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_research_reports_supersedes_report_id",
                table: "research_reports",
                column: "supersedes_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_research_requests_normalized_research_key_status",
                table: "research_requests",
                columns: new[] { "normalized_research_key", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_research_requests_requesting_bot_id",
                table: "research_requests",
                column: "requesting_bot_id");

            migrationBuilder.CreateIndex(
                name: "IX_research_requests_result_report_id",
                table: "research_requests",
                column: "result_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_research_runs_research_request_id_attempt_number",
                table: "research_runs",
                columns: new[] { "research_request_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_research_subscriptions_research_request_id_trading_bot_id",
                table: "research_subscriptions",
                columns: new[] { "research_request_id", "trading_bot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_research_subscriptions_trading_bot_id",
                table: "research_subscriptions",
                column: "trading_bot_id");

            migrationBuilder.CreateIndex(
                name: "IX_research_tool_invocations_research_run_id_sequence_number",
                table: "research_tool_invocations",
                columns: new[] { "research_run_id", "sequence_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_research_report_sources_research_reports_research_report_id",
                table: "research_report_sources",
                column: "research_report_id",
                principalTable: "research_reports",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_research_reports_research_requests_research_request_id",
                table: "research_reports",
                column: "research_request_id",
                principalTable: "research_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_research_reports_research_runs_research_run_id",
                table: "research_reports",
                column: "research_run_id",
                principalTable: "research_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE TRIGGER research_reports_immutable_content
                BEFORE UPDATE OF report_series_id, version_number, research_request_id, research_run_id, subject_type, subject_id, question, visibility, data_cutoff, generated_at, report_schema_version, content_json, content_markdown, content_hash, generator_metadata_json
                ON research_reports WHEN OLD.status IN ('Published','Expired','Superseded','Retracted')
                BEGIN SELECT RAISE(ABORT, 'published research report content is immutable'); END;
                CREATE TRIGGER research_reports_no_delete
                BEFORE DELETE ON research_reports
                BEGIN SELECT RAISE(ABORT, 'research report audit history cannot be deleted'); END;
                CREATE TRIGGER research_report_sources_immutable
                BEFORE UPDATE ON research_report_sources
                BEGIN SELECT RAISE(ABORT, 'research report source facts are immutable'); END;
                CREATE TRIGGER research_report_sources_no_delete
                BEFORE DELETE ON research_report_sources
                BEGIN SELECT RAISE(ABORT, 'research report source facts cannot be deleted'); END;
                CREATE TRIGGER research_tool_invocations_terminal_immutable
                BEFORE UPDATE ON research_tool_invocations
                WHEN OLD.status IN ('Succeeded','Failed','Rejected','Cancelled')
                BEGIN SELECT RAISE(ABORT, 'completed research tool audit facts are immutable'); END;
                CREATE TRIGGER research_tool_invocations_terminal_no_delete
                BEFORE DELETE ON research_tool_invocations
                WHEN OLD.status IN ('Succeeded','Failed','Rejected','Cancelled')
                BEGIN SELECT RAISE(ABORT, 'completed research tool audit facts cannot be deleted'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS research_reports_immutable_content;
                DROP TRIGGER IF EXISTS research_reports_no_delete;
                DROP TRIGGER IF EXISTS research_report_sources_immutable;
                DROP TRIGGER IF EXISTS research_report_sources_no_delete;
                DROP TRIGGER IF EXISTS research_tool_invocations_terminal_immutable;
                DROP TRIGGER IF EXISTS research_tool_invocations_terminal_no_delete;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_research_requests_research_reports_result_report_id",
                table: "research_requests");

            migrationBuilder.DropTable(
                name: "research_report_sources");

            migrationBuilder.DropTable(
                name: "research_subscriptions");

            migrationBuilder.DropTable(
                name: "research_tool_invocations");

            migrationBuilder.DropTable(
                name: "research_reports");

            migrationBuilder.DropTable(
                name: "research_runs");

            migrationBuilder.DropTable(
                name: "research_requests");

            migrationBuilder.UpdateData(
                table: "schema_metadata",
                keyColumn: "key",
                keyValue: "application_data_format_version",
                column: "value",
                value: "3");
        }
    }
