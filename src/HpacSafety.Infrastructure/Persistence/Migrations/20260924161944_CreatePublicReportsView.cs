using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     The <c>public_reports</c> view the public feed and detail read (#28).
	///     It adds no table and changes no row.
	/// </summary>
	public partial class CreatePublicReportsView : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260924161944_CreatePublicReportsView.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260924161944_CreatePublicReportsView.Down.sql"));
		}
	}
}
