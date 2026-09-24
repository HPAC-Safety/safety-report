using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>dotnet ef database update</c> against a clean PostgreSQL 17, and the
///     shape it leaves behind.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class SchemaTests(PostgresFixture postgres)
{
	private static readonly string[] ExpectedTables =
	[
		"audit_log",
		"outbox_messages",
		"pending_import_logic",
		"question_choices",
		"question_revisions",
		"questions",
		"report_answers",
		"report_comment_revisions",
		"report_comments",
		"report_files",
		"reports",
		"summaries",
	];

	[Fact]
	public async Task GivenCleanPostgres17_WhenMigrationsAreApplied_ThenEveryTableExists()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var tables = await QueryStrings(
			connectionString,
			"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory' ORDER BY table_name");

		// Then
		tables.ShouldBe(ExpectedTables);
	}

	[Fact]
	public async Task GivenCleanPostgres17_WhenMigrationsAreApplied_ThenPublicReportsViewCarriesOnlyTheAllowlist()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var views = await QueryStrings(
			connectionString,
			"SELECT table_name FROM information_schema.views WHERE table_schema = 'public' ORDER BY table_name");
		var columns = await QueryStrings(
			connectionString,
			"SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'public_reports' ORDER BY ordinal_position");

		// Then — the view's columns are the public DTO's allowlist (CON-DP-011).
		views.ShouldBe(["public_report_comments", "public_reports"]);
		columns.ShouldBe(["id", "ai_summary_en", "ai_summary_fr", "published_at", "comment_count"]);
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenPendingMigrationsAreChecked_ThenNone()
	{
		// Given — this proves EF's own bookkeeping, not that the migration's
		// content is safe to re-run. `dotnet ef database update` reads
		// __EFMigrationsHistory and never re-invokes a migration already
		// recorded there, so this alone cannot catch a non-idempotent
		// statement inside one. See the seed-reapplication tests below.
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		await using var context = PostgresFixture.ContextFor(connectionString);
		var pending = await context.Database.GetPendingMigrationsAsync();

		// Then
		pending.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenOutboxIndexIsRead_ThenCoversOnlyRowsWorkerMayClaim()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var definitions = await QueryStrings(
			connectionString,
			"SELECT indexdef FROM pg_indexes WHERE tablename = 'outbox_messages' AND indexname = 'ix_outbox_messages_claimable'");

		// Then — without the filter the claim query reads the whole processed
		// history for the rest of the system's life.
		definitions.Length.ShouldBe(1);
		definitions[0].ShouldContain("next_attempt_at");
		definitions[0].ShouldContain("processed_at IS NULL");
		definitions[0].ShouldContain("poisoned_at IS NULL");
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenSummariesTableIsRead_ThenExactlyOneRowMayExistPerReport()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var definitions = await QueryStrings(
			connectionString,
			"SELECT indexdef FROM pg_indexes WHERE tablename = 'summaries' AND indexdef LIKE '%UNIQUE%'");

		// Then — a reviewer never has to choose between two summaries of the
		// same report; the bilingual pair lives in one row.
		definitions.ShouldContain(d => d.Contains("report_id", StringComparison.Ordinal));
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenAnswerColumnsAreRead_ThenValueIsOneStringAndCodesAreGone()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var columns = await QueryStrings(
			connectionString,
			"SELECT column_name FROM information_schema.columns WHERE table_name = 'report_answers'");

		// Then — every answer is one string, in the reporter's language,
		// immutable, with a place for its second language and how it was
		// produced (ADR-0072, ADR-0080)
		columns.ShouldContain("value");
		columns.ShouldContain("locale");
		columns.ShouldContain("translated_value");
		columns.ShouldContain("translation_source");
		columns.ShouldNotContain("needs_translation");
		columns.ShouldNotContain("selected_option_codes");
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenQuestionKeyIndexIsRead_ThenUniqueAmongLiveRowsOnly()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var definitions = await QueryStrings(
			connectionString,
			"SELECT indexdef FROM pg_indexes WHERE tablename = 'questions' AND indexname = 'ix_questions_key'");

		// Then — a fork chain shares one key, and exactly one of them is live
		// (ADR-0071)
		definitions.Length.ShouldBe(1);
		definitions[0].ShouldContain("UNIQUE");
		definitions[0].ShouldContain("deleted IS NULL");
	}

	private static async Task<string[]> QueryStrings(string connectionString,
													 string sql)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();

		var values = new List<string>();
		while (await reader.ReadAsync())
		{
			values.Add(reader.GetString(0));
		}

		return [.. values];
	}
}
