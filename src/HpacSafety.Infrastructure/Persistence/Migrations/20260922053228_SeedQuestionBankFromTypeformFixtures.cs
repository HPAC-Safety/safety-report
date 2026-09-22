using HpacSafety.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HpacSafety.Infrastructure.Persistence.Migrations
{
	/// <inheritdoc />
	public partial class SeedQuestionBankFromTypeformFixtures : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Writes whatever QuestionBankSeed.Questions currently holds, read
			// at Up()-execution time rather than baked in here — see
			// QuestionBankSeedWriter's remarks. This is the first migration to
			// call the current-schema Write(migrationBuilder); the schema
			// itself did not change, so there is nothing else in this file.
			QuestionBankSeedWriter.Write(migrationBuilder);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{

		}
	}
}
