using System;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     A published report's photos and video (ADR-0117): media consent on the
	///     report, a reviewer's hide on each file, the seeded consent_media system
	///     question, and the public_report_media view.
	/// </summary>
	public partial class ShowPublishedMedia : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_questions_role",
				table: "questions");

			migrationBuilder.AddColumn<bool>(
				name: "consent_media",
				table: "reports",
				type: "boolean",
				nullable: true);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "hidden_at",
				table: "report_files",
				type: "timestamp with time zone",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "hidden_by_subject",
				table: "report_files",
				type: "character varying(256)",
				maxLength: 256,
				nullable: true);

			migrationBuilder.AddCheckConstraint(
				name: "ck_report_files_hidden_coherence",
				table: "report_files",
				sql: "(hidden_at IS NULL) = (hidden_by_subject IS NULL)");

			migrationBuilder.AddCheckConstraint(
				name: "ck_questions_role",
				table: "questions",
				sql: "role IN ('none', 'consent_publish', 'consent_media')");

			migrationBuilder.Sql(SqlScript.Read("20260924190336_ShowPublishedMedia.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260924190336_ShowPublishedMedia.Down.sql"));

			migrationBuilder.DropCheckConstraint(
				name: "ck_report_files_hidden_coherence",
				table: "report_files");

			migrationBuilder.DropCheckConstraint(
				name: "ck_questions_role",
				table: "questions");

			migrationBuilder.DropColumn(
				name: "consent_media",
				table: "reports");

			migrationBuilder.DropColumn(
				name: "hidden_at",
				table: "report_files");

			migrationBuilder.DropColumn(
				name: "hidden_by_subject",
				table: "report_files");

			migrationBuilder.AddCheckConstraint(
				name: "ck_questions_role",
				table: "questions",
				sql: "role IN ('none', 'consent_publish')");
		}
	}
}
