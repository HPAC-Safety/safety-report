using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     The database refuses a change to the reporter's account and to a summary
	///     revision (#669, ADR-0178): column-scoped <c>BEFORE UPDATE OR DELETE</c>
	///     triggers on <c>report_answers</c>, <c>report_files</c>, <c>reports</c>, and
	///     <c>summary_revisions</c>. Changes no table and no row; adds no column.
	/// </summary>
	public partial class RefuseChangesToTheReportersAccount : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260930051851_RefuseChangesToTheReportersAccount.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260930051851_RefuseChangesToTheReportersAccount.Down.sql"));
		}
	}
}
