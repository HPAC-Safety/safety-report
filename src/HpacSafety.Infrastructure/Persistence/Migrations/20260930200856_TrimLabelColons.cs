using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Removes a trailing <c>:</c> or <c> :</c> from every stored question label, in
	///     place, creating no revision (#697, ADR-0181). The interface adds the colon in
	///     the reader's locale from now on. A carved exception to invariant 1's rule
	///     that an answered question forks instead of being revised; changes no table
	///     and no column.
	/// </summary>
	public partial class TrimLabelColons : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20260930200856_TrimLabelColons.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Nothing to undo: the colon is drawn by the interface now, and which labels
			// once carried one is not recorded.
		}
	}
}
