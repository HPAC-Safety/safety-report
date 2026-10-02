using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Restores the seeded group links a database created from scratch never got
	///     (#754, ADR-0187). <c>InitialSchema</c> writes the current seed through the
	///     legacy shape, which has no grouping column, so the later full seed skips
	///     every question as already present. Each seeded question that never had a
	///     group is grouped under the one the seed names while that group is live: a
	///     new revision when no answer references it, a fork with the same key when
	///     one does (ADR-0071). A re-run is a no-op. No table or column changes.
	/// </summary>
	public partial class RestoreSeededGroups : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(SqlScript.Read("20261002200257_RestoreSeededGroups.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Nothing to undo: no schema changed, and nothing is physically deleted
			// (invariant 8). An Administrator ungroups a question the way any edit
			// is made.
		}
	}
}
