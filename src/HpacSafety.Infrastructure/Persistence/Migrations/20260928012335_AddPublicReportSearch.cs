using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     search_public_reports, the fuzzy public search's one read (#574,
	///     ADR-0157). It reads only public_reports and public_report_comments,
	///     so it can never return a private answer, a name, or an unpublished
	///     report. Adds no table and changes no row.
	/// </summary>
	public partial class AddPublicReportSearch : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260928012335_AddPublicReportSearch.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260928012335_AddPublicReportSearch.Down.sql"));
		}
	}
}
