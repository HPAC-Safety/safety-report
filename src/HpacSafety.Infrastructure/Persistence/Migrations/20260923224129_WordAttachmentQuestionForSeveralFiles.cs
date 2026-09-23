using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class WordAttachmentQuestionForSeveralFiles : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Rewords the seeded attachment question only where it still reads
			// exactly as seeded, revising or forking it as an Administrator's
			// edit would (ADR-0071). No schema change.
			migrationBuilder.Sql(SqlScript.Read("20260923224129_WordAttachmentQuestionForSeveralFiles.sql"));
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{

		}
	}
}
