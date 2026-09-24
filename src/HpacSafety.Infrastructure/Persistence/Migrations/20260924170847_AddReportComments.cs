using System;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Members' comments on published reports and their revisions, the
	///     <c>public_report_comments</c> view, and <c>public_reports.comment_count</c>
	///     (ADR-0114). Adds tables; changes no existing row.
	/// </summary>
	public partial class AddReportComments : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages");

			migrationBuilder.CreateTable(
				name: "report_comments",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					report_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					author_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
					created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					hidden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					hidden_by_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_report_comments", x => x.id);
					table.CheckConstraint("ck_report_comments_hidden_coherence", "(hidden_at IS NULL) = (hidden_by_subject IS NULL)");
					table.ForeignKey(
						name: "fk_report_comments_reports_report_id",
						column: x => x.report_id,
						principalTable: "reports",
						principalColumn: "id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "report_comment_revisions",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					comment_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					number = table.Column<int>(type: "integer", nullable: false),
					text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
					locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
					translated_text = table.Column<string>(type: "text", nullable: true),
					translation_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
					created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_report_comment_revisions", x => x.id);
					table.CheckConstraint("ck_report_comment_revisions_locale", "locale IN ('en-CA', 'fr-CA')");
					table.CheckConstraint("ck_report_comment_revisions_translation_coherence", "(translated_text IS NULL) = (translation_source IS NULL)");
					table.CheckConstraint("ck_report_comment_revisions_translation_source", "translation_source IS NULL OR translation_source = 'auto'");
					table.ForeignKey(
						name: "fk_report_comment_revisions_report_comments_comment_id",
						column: x => x.comment_id,
						principalTable: "report_comments",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.AddCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages",
				sql: "type IN ('summarize_report', 'process_attachment', 'translate_answers', 'translate_comment')");

			migrationBuilder.CreateIndex(
				name: "ix_report_comment_revisions_comment_id_number",
				table: "report_comment_revisions",
				columns: new[] { "comment_id", "number" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_report_comment_revisions_created_at",
				table: "report_comment_revisions",
				column: "created_at",
				filter: "translated_text IS NULL AND deleted IS NULL");

			migrationBuilder.CreateIndex(
				name: "ix_report_comments_report_id_created_at",
				table: "report_comments",
				columns: new[] { "report_id", "created_at" });

			migrationBuilder.Sql(SqlScript.Read("20260924170847_AddReportComments.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260924170847_AddReportComments.Down.sql"));
			migrationBuilder.Sql(SqlScript.Read("20260924161944_CreatePublicReportsView.sql"));

			migrationBuilder.DropTable(
				name: "report_comment_revisions");

			migrationBuilder.DropTable(
				name: "report_comments");

			migrationBuilder.DropCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages");

			migrationBuilder.AddCheckConstraint(
				name: "ck_outbox_messages_type",
				table: "outbox_messages",
				sql: "type IN ('summarize_report', 'process_attachment', 'translate_answers')");
		}
	}
}
