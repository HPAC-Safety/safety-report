using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class RecordSummaryLanguageSources : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<string>(
				name: "source_en",
				table: "summaries",
				type: "character varying(64)",
				maxLength: 64,
				nullable: false,
				defaultValue: "generated");

			migrationBuilder.AddColumn<string>(
				name: "source_fr",
				table: "summaries",
				type: "character varying(64)",
				maxLength: 64,
				nullable: false,
				defaultValue: "generated");

			migrationBuilder.Sql(SqlScript.Read("20260924055856_RecordSummaryLanguageSources.sql"));

			migrationBuilder.AddCheckConstraint(
				name: "ck_summaries_source_en",
				table: "summaries",
				sql: "source_en IN ('generated', 'human', 'machine')");

			migrationBuilder.AddCheckConstraint(
				name: "ck_summaries_source_fr",
				table: "summaries",
				sql: "source_fr IN ('generated', 'human', 'machine')");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_summaries_source_en",
				table: "summaries");

			migrationBuilder.DropCheckConstraint(
				name: "ck_summaries_source_fr",
				table: "summaries");

			migrationBuilder.DropColumn(
				name: "source_en",
				table: "summaries");

			migrationBuilder.DropColumn(
				name: "source_fr",
				table: "summaries");
		}
	}
}
