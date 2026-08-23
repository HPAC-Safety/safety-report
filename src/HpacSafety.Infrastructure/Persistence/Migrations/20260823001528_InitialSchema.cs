using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    member_identifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    aggregate_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    poisoned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sensitivity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    section_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consent_publish = table.Column<bool>(type: "boolean", nullable: true),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: true),
                    occurred_at_local = table.Column<string>(type: "text", nullable: true),
                    province = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    time_of_day = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pilot_injury = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    passenger_injury = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    summary_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    admin_user_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    target_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_log_admin_users_admin_user_id",
                        column: x => x.admin_user_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_versions",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_versions_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_aircraft",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    report_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    discipline = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    certification_answer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_aircraft", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_aircraft_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_files",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    report_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    blob_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    stripped_blob_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    exif_stripped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_files_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "summaries",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    report_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prompt_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_source = table.Column<bool>(type: "boolean", nullable: false),
                    translated_from_summary_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: true),
                    approved_by = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_summaries", x => x.id);
                    table.ForeignKey(
                        name: "fk_summaries_admin_users_approved_by",
                        column: x => x.approved_by,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_summaries_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_summaries_summaries_translated_from_summary_id",
                        column: x => x.translated_from_summary_id,
                        principalTable: "summaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_options",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_version_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_options_question_versions_question_version_id",
                        column: x => x.question_version_id,
                        principalTable: "question_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_translations",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_version_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    help_text = table.Column<string>(type: "text", nullable: true),
                    placeholder = table.Column<string>(type: "text", nullable: true),
                    is_source = table.Column<bool>(type: "boolean", nullable: false),
                    is_machine_translated = table.Column<bool>(type: "boolean", nullable: false),
                    translated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_translations_question_versions_question_version_id",
                        column: x => x.question_version_id,
                        principalTable: "question_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_answers",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    report_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_version_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sensitivity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    selected_option_codes = table.Column<string[]>(type: "text[]", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_answers_question_versions_question_version_id",
                        column: x => x.question_version_id,
                        principalTable: "question_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_report_answers_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_report_answers_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_option_translations",
                columns: table => new
                {
                    id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    question_option_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
                    locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    is_source = table.Column<bool>(type: "boolean", nullable: false),
                    is_machine_translated = table.Column<bool>(type: "boolean", nullable: false),
                    translated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_option_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_question_option_translations_question_options_question_opti~",
                        column: x => x.question_option_id,
                        principalTable: "question_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_member_identifier",
                table: "admin_users",
                column: "member_identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_admin_user_id",
                table: "audit_log",
                column: "admin_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred_at",
                table: "audit_log",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_target_type_target_id",
                table: "audit_log",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_aggregate_id",
                table: "outbox_messages",
                column: "aggregate_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_claimable",
                table: "outbox_messages",
                column: "next_attempt_at",
                filter: "processed_at IS NULL AND poisoned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_question_option_translations_question_option_id_locale",
                table: "question_option_translations",
                columns: new[] { "question_option_id", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_question_options_question_version_id_code",
                table: "question_options",
                columns: new[] { "question_version_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_question_translations_question_version_id_locale",
                table: "question_translations",
                columns: new[] { "question_version_id", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_question_versions_question_id_version_number",
                table: "question_versions",
                columns: new[] { "question_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_is_active_display_order",
                table: "questions",
                columns: new[] { "is_active", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_key",
                table: "questions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_aircraft_report_id",
                table: "report_aircraft",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_question_id",
                table: "report_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_question_version_id",
                table: "report_answers",
                column: "question_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_answers_report_id",
                table: "report_answers",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_files_exif_stripped_at",
                table: "report_files",
                column: "exif_stripped_at",
                filter: "exif_stripped_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_report_files_report_id",
                table: "report_files",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_status_submitted_at",
                table: "reports",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_summaries_approved_by",
                table: "summaries",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "ix_summaries_report_id_language",
                table: "summaries",
                columns: new[] { "report_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_summaries_translated_from_summary_id",
                table: "summaries",
                column: "translated_from_summary_id");
            // The question bank, so a clean database asks exactly the question
            // set in docs/form-spec.md. Guarded row-by-row, so re-applying this
            // is a safe no-op. See ADR-0020.
            Seeding.QuestionBankSeedWriter.WriteLegacySensitivitySchema(migrationBuilder);

            // One obviously-fake local administrator, and only where the
            // database applying this says it wants one. Unset means no, which
            // is what production is. See ADR-0020.
            migrationBuilder.Sql(Seeding.DevelopmentAdminSeed.InsertSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "question_option_translations");

            migrationBuilder.DropTable(
                name: "question_translations");

            migrationBuilder.DropTable(
                name: "report_aircraft");

            migrationBuilder.DropTable(
                name: "report_answers");

            migrationBuilder.DropTable(
                name: "report_files");

            migrationBuilder.DropTable(
                name: "summaries");

            migrationBuilder.DropTable(
                name: "question_options");

            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "question_versions");

            migrationBuilder.DropTable(
                name: "questions");
        }
    }
}
