using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Turns the seeded Country question from a yes/no question into an optional
	///     single-select of every ISO 3166-1 country, with Canada and the United States
	///     pinned first, and makes Province conditional on Canada (#750, ADR-0186). The
	///     question is revised when no answer references it and forked, keeping its key,
	///     when one does (ADR-0071); an old yes/no answer is left as given. It changes
	///     nothing unless Country still reads as seeded, so a re-run is a no-op. No table
	///     or column changes.
	/// </summary>
	public partial class MakeCountryAPinnedPickList : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20261002181219_MakeCountryAPinnedPickList.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Nothing to undo: no schema changed, and nothing is physically deleted
			// (invariant 8). The revision, fork, and choices stay as history; an
			// Administrator changes the question back the way any edit is made.
		}
	}
}
