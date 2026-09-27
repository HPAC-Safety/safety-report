using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Adds each report's review version to <c>admin_report_queue</c>, so a
	///     reviewer can publish, unpublish, or delete from the list without an
	///     audited detail read (#568). It adds no table and changes no row.
	/// </summary>
	public partial class ListReportVersions : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260927172950_ListReportVersions.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260927172950_ListReportVersions.Down.sql"));
		}
	}
}
