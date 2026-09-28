using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <summary>
	///     Widens <c>ck_questions_role</c> to four new <c>QuestionRole</c> members,
	///     backfills them onto the seeded reporter and pilot name questions by
	///     their stable key, enforces that a role lives on at most one live
	///     question with <c>ix_questions_role</c>, and adds <c>reporter_name</c>
	///     and <c>pilot_name</c> to <c>admin_report_queue</c> (#571, ADR-0154).
	/// </summary>
	public partial class ShowReporterAndPilotNames : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropCheckConstraint(
				name: "ck_questions_role",
				table: "questions");

			migrationBuilder.AddCheckConstraint(
				name: "ck_questions_role",
				table: "questions",
				sql: "role IN ('none', 'consent_publish', 'consent_media', 'reporter_first_name', 'reporter_last_name', 'pilot_first_name', 'pilot_last_name')");

			migrationBuilder.Sql(SqlScript.Read("20260927231919_ShowReporterAndPilotNames.sql"));

			// After the backfill, not before: creating it first would only need
			// the four seeded rows to already be distinct, which they are, but
			// the backfill is the operation this index is guarding, so it reads
			// clearer proving the data clean before the constraint locks it.
			migrationBuilder.CreateIndex(
				name: "ix_questions_role",
				table: "questions",
				column: "role",
				unique: true,
				filter: "role <> 'none' AND deleted IS NULL");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropIndex(
				name: "ix_questions_role",
				table: "questions");

			migrationBuilder.Sql(SqlScript.Read("20260927231919_ShowReporterAndPilotNames.Down.sql"));

			migrationBuilder.DropCheckConstraint(
				name: "ck_questions_role",
				table: "questions");

			migrationBuilder.AddCheckConstraint(
				name: "ck_questions_role",
				table: "questions",
				sql: "role IN ('none', 'consent_publish', 'consent_media')");
		}
	}
}
