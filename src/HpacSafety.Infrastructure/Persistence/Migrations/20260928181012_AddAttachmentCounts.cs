using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     A viewer-scoped attachment count on the public feed and the admin list
	///     (issue #427, decisions 1-2). Appends public_attachment_count and
	///     full_attachment_count to public_reports, attachment_count to
	///     admin_report_queue, and the same two counts to search_public_reports.
	///     Changes no table; adds no row.
	/// </summary>
	public partial class AddAttachmentCounts : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260928181012_AddAttachmentCounts.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260928181012_AddAttachmentCounts.Down.sql"));
		}
	}
}
