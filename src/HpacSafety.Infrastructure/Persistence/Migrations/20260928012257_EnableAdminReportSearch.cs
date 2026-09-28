using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Enables <c>pg_trgm</c> and <c>unaccent</c>, and adds the
	///     <c>admin_report_search_document</c> view and
	///     <c>search_admin_reports(query)</c> function the admin search box reads
	///     (#573, ADR-0156). Adds no table and changes no row.
	/// </summary>
	public partial class EnableAdminReportSearch : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260928012257_EnableAdminReportSearch.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260928012257_EnableAdminReportSearch.Down.sql"));
		}
	}
}
