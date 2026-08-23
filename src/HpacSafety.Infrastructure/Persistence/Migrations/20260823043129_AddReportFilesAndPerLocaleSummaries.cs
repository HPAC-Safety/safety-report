using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportFilesAndPerLocaleSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_summaries_report_id",
                table: "summaries");

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

            migrationBuilder.CreateIndex(
                name: "ix_summaries_report_id_language",
                table: "summaries",
                columns: new[] { "report_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_files_exif_stripped_at",
                table: "report_files",
                column: "exif_stripped_at",
                filter: "exif_stripped_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_report_files_report_id",
                table: "report_files",
                column: "report_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_files");

            migrationBuilder.DropIndex(
                name: "ix_summaries_report_id_language",
                table: "summaries");

            migrationBuilder.CreateIndex(
                name: "ix_summaries_report_id",
                table: "summaries",
                column: "report_id",
                unique: true);
        }
    }
}
