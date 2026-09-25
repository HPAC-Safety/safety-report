using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class KeepSystemQuestionsPrivate : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Gives each system question that is not yet private a new, private
			// revision, as the domain now requires. No schema change.
			migrationBuilder.Sql(SqlScript.Read("20260925212255_KeepSystemQuestionsPrivate.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{

		}
	}
}
