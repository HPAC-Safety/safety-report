using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class AddReportPrivateAttachments : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "attachment_id",
				table: "report_private_note_revisions",
				type: "char(11)",
				fixedLength: true,
				maxLength: 11,
				nullable: true);

			migrationBuilder.CreateTable(
				name: "report_private_attachments",
				columns: table => new
				{
					id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					report_id = table.Column<string>(type: "char(11)", fixedLength: true, maxLength: 11, nullable: false),
					blob_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
					original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
					content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					byte_size = table.Column<long>(type: "bigint", nullable: false),
					description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
					added_by_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
					added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					deleted = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
					deleted_by_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("pk_report_private_attachments", x => x.id);
					table.CheckConstraint("ck_report_private_attachments_byte_size", "byte_size > 0");
					table.ForeignKey(
						name: "fk_report_private_attachments_reports_report_id",
						column: x => x.report_id,
						principalTable: "reports",
						principalColumn: "id",
						onDelete: ReferentialAction.Restrict);
				});

			migrationBuilder.CreateIndex(
				name: "ix_report_private_note_revisions_attachment_id",
				table: "report_private_note_revisions",
				column: "attachment_id");

			migrationBuilder.CreateIndex(
				name: "ix_report_private_attachments_blob_key",
				table: "report_private_attachments",
				column: "blob_key",
				unique: true);

			migrationBuilder.CreateIndex(
				name: "ix_report_private_attachments_report_id_added_at",
				table: "report_private_attachments",
				columns: new[] { "report_id", "added_at" });

			migrationBuilder.AddForeignKey(
				name: "fk_report_private_note_revisions_report_private_attachments_at~",
				table: "report_private_note_revisions",
				column: "attachment_id",
				principalTable: "report_private_attachments",
				principalColumn: "id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "fk_report_private_note_revisions_report_private_attachments_at~",
				table: "report_private_note_revisions");

			migrationBuilder.DropTable(
				name: "report_private_attachments");

			migrationBuilder.DropIndex(
				name: "ix_report_private_note_revisions_attachment_id",
				table: "report_private_note_revisions");

			migrationBuilder.DropColumn(
				name: "attachment_id",
				table: "report_private_note_revisions");
		}
	}
}
