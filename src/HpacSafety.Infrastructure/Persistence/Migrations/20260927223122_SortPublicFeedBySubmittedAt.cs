using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     public_reports gains submitted_at, the public feed's new sort and
	///     keyset cursor key (#570, ADR-0153). Changes no table; adds no row.
	/// </summary>
	public partial class SortPublicFeedBySubmittedAt : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260927223122_SortPublicFeedBySubmittedAt.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260927223122_SortPublicFeedBySubmittedAt.Down.sql"));
		}
	}
}
