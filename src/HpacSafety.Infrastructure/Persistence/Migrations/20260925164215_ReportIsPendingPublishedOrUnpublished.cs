using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     A report is Pending, Published, or Unpublished once the Worker is done
	///     with it (ADR-0125): the status codes, the unpublishing note, and the
	///     public and admin views that read them.
	/// </summary>
	public partial class ReportIsPendingPublishedOrUnpublished : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_reports_status",
				table: "reports");

			migrationBuilder.RenameColumn(
				name: "rejection_note",
				table: "reports",
				newName: "unpublish_note");

			migrationBuilder.Sql(SqlScript.Read("20260925164215_ReportIsPendingPublishedOrUnpublished.sql"));

			migrationBuilder.AddCheckConstraint(
				name: "ck_reports_status",
				table: "reports",
				sql: "status IN ('submitted', 'summarizing', 'summary_failed', 'pending', 'published', 'unpublished')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_reports_status",
				table: "reports");

			migrationBuilder.Sql(SqlScript.Read("20260925164215_ReportIsPendingPublishedOrUnpublished.Down.sql"));

			migrationBuilder.RenameColumn(
				name: "unpublish_note",
				table: "reports",
				newName: "rejection_note");

			migrationBuilder.AddCheckConstraint(
				name: "ck_reports_status",
				table: "reports",
				sql: "status IN ('submitted', 'summarizing', 'pending_review', 'summary_failed', 'approved', 'rejected', 'published')");
		}
	}
}
