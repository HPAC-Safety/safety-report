using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     public_reports gains language, the locale the reporter wrote the report
	///     in, so a report's own page can say a summary was translated from it
	///     (#682, ADR-0176). Changes no table; adds no row.
	/// </summary>
	public partial class ShowReportLanguageOnPublicReports : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260930011550_ShowReportLanguageOnPublicReports.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260930011550_ShowReportLanguageOnPublicReports.Down.sql"));
		}
	}
}
