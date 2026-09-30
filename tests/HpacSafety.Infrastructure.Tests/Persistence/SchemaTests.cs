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
		"question_choice_parents",
		"question_choices",
		"question_revisions",
		"questions",
		"report_answers",
		"report_comment_revisions",
		"report_comments",
		"report_files",
		"report_private_attachments",
		"report_private_note_revisions",
		"report_private_notes",
		"reports",
		"summaries",
		"summary_revisions",
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
	public async Task GivenCleanPostgres17_WhenMigrationsAreApplied_ThenPublicReportsViewCarriesOnlyTheAllowlistPlusSubmittedAt()
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

		// Then — the view's columns are the public DTO's allowlist (CON-DP-011)
		// plus submitted_at, the feed's sort and keyset cursor key (#570,
		// ADR-0153), the two viewer-scoped attachment counts (#427), and language
		// (#682, ADR-0176; the detail read only, never the feed), none of
		// which the API serializes as-is — it reads only whichever count fits
		// the viewer's role.
		views.ShouldBe([
			"admin_pending_counts", "admin_report_queue", "admin_report_search_document", "answers_awaiting_translation",
			"latest_approved_summary_revisions", "latest_summary_revisions",
			"public_report_comments", "public_report_media", "public_reports",
		]);
		columns.ShouldBe([
			"id", "ai_summary_en", "ai_summary_fr", "published_at", "comment_count", "submitted_at",
			"public_attachment_count", "full_attachment_count", "language",
		]);
	}

	[Theory]
	[InlineData("admin_report_queue", "id,submitted_at,status,language,consent_publish,is_stuck,needs_action,version,reporter_name,pilot_name,attachment_count")]
	[InlineData("answers_awaiting_translation", "id,question_key,value,locale,answered_at")]
	[InlineData("admin_pending_counts", "reports_needing_action,answers_awaiting_translation,type_ahead_values_awaiting_review")]
	[InlineData("public_report_media", "id,report_id,kind,content_type,stripped_blob_key,document_blob_key,uploaded_at")]
	public async Task GivenCleanPostgres17_WhenMigrationsAreApplied_ThenAdminViewCarriesOnlyItsColumns(string view,
		string expected)
	{
		ArgumentNullException.ThrowIfNull(expected);

		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var columns = await QueryStrings(
			connectionString,
			$"SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{view}' ORDER BY ordinal_position");

		// Then — the report queue carries state and timing only, never answer
		// or summary text (REQ-MOD-030, ADR-0116).
		columns.ShouldBe(expected.Split(','));
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
		// same report; its revisions hang from that one row (ADR-0177).
		definitions.ShouldContain(d => d.Contains("report_id", StringComparison.Ordinal));
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenSummaryRevisionsAreRead_ThenSequenceIsUniquePerSummary()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var definitions = await QueryStrings(
			connectionString,
			"SELECT indexdef FROM pg_indexes WHERE tablename = 'summary_revisions' AND indexdef LIKE '%UNIQUE%'");

		// Then — two reviewers cannot both append the same next revision
		definitions.ShouldContain(d => d.Contains("(summary_id, sequence)", StringComparison.Ordinal));
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenSummariesColumnsAreRead_ThenTextAndApprovalLiveOnTheRevisions()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var columns = await QueryStrings(
			connectionString,
			"SELECT column_name FROM information_schema.columns WHERE table_name = 'summaries' ORDER BY ordinal_position");

		// Then — the row is only the identity its revisions hang from (ADR-0177)
		columns.ShouldBe(["id", "report_id", "deleted"]);
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

	[Fact]
	public async Task GivenMigratedDatabase_WhenQuestionRoleIndexIsRead_ThenUniqueAmongLiveNonNoneRowsOnly()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var definitions = await QueryStrings(
			connectionString,
			"SELECT indexdef FROM pg_indexes WHERE tablename = 'questions' AND indexname = 'ix_questions_role'");

		// Then — a role lives on at most one live question (Question.AssignRole),
		// enforced in the database the same way ix_questions_key already is, so a
		// fork's momentary second row sharing a role never collides and a bug can
		// never silently give two live questions the same role (ADR-0154)
		definitions.Length.ShouldBe(1);
		definitions[0].ShouldContain("UNIQUE");
		definitions[0].ShouldContain("role)::text <> 'none'");
		definitions[0].ShouldContain("deleted IS NULL");
	}

	[Fact]
	public async Task GivenCleanPostgres17_WhenMigrationsAreApplied_ThenSearchExtensionsAndFunctionExist()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		var extensions = await QueryStrings(
			connectionString,
			"SELECT extname FROM pg_extension WHERE extname IN ('pg_trgm', 'unaccent') ORDER BY extname");
		var functions = await QueryStrings(
			connectionString,
			"SELECT proname FROM pg_proc WHERE proname = 'search_admin_reports'");

		// Then — the admin search box's engine (REQ-MOD-130, ADR-0156): no new
		// service, both extensions enabled, and the ranking function they back.
		extensions.ShouldBe(["pg_trgm", "unaccent"]);
		functions.ShouldBe(["search_admin_reports"]);
	}

	[Fact]
	public async Task GivenMigratedDatabase_WhenSearchedWithNoMatch_ThenTheFunctionReturnsNoRowsRatherThanErroring()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		await using var command = new NpgsqlCommand("SELECT * FROM search_admin_reports(@query)", connection);
		command.Parameters.AddWithValue("query", "zzsynthnothingmatchesanything");

		// When
		await using var reader = await command.ExecuteReaderAsync();

		// Then
		(await reader.ReadAsync()).ShouldBeFalse();
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
