using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddReportPrivateNotes : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "report_private_notes",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					report_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_report_private_notes", x => x.id);
					table.ForeignKey(
						name: "fk_report_private_notes_reports_report_id",
						column: x => x.report_id,
						principalTable: "reports",
						principalColumn: "id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateTable(
				name: "report_private_note_revisions",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					note_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					number = table.Column<int>(type: "integer", nullable: false),
					text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
					author_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
					created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_report_private_note_revisions", x => x.id);
					table.CheckConstraint("ck_report_private_note_revisions_number", "number >= 1");
					table.ForeignKey(
						name: "fk_report_private_note_revisions_report_private_notes_note_id",
						column: x => x.note_id,
						principalTable: "report_private_notes",
						principalColumn: "id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "ix_report_private_note_revisions_note_id_number",
				table: "report_private_note_revisions",
				columns: new[] { "note_id", "number" },
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_report_private_notes_report_id_created_at",
				table: "report_private_notes",
				columns: new[] { "report_id", "created_at" });
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "report_private_note_revisions");

			migrationBuilder.DropTable(
				name: "report_private_notes");
		}
	}
}
