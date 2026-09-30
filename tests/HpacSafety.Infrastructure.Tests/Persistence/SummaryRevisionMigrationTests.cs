using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>AddSummaryRevisions</c> folds every <c>summaries</c> row into revision 1
///     and moves the public read side onto the latest approved revision
///     (REQ-MOD-201, REQ-MOD-198, ADR-0177). Every report is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class SummaryRevisionMigrationTests(PostgresFixture postgres)
{
	private const string PriorMigration = "AddAttachmentCounts";

	[Fact]
	public async Task GivenExistingSummaries_WhenMigrationRuns_ThenEachBecomesRevisionOneKeepingItsSourcesAndApproval()
	{
		// Given — one approved and edited pair, one unapproved generated pair, one deleted pair
		var connectionString = await BeforeTheMigration();

		// When
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, null);
		}

		// Then
		await using var connection = await Open(connectionString);
		(await Scalar(connection, "SELECT count(*)::text FROM summary_revisions")).ShouldBe("3");
		(await Scalar(connection, "SELECT count(*)::text FROM summary_revisions WHERE sequence = 1 AND author_subject IS NULL AND restored_from_id IS NULL"))
			.ShouldBe("3");

		var approved = await Row(connection, "SELECT ai_summary_en, source_en, source_fr, model, prompt_version, approved_by_subject, to_char(created_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS') FROM summary_revisions WHERE summary_id = 'sapproved01'");
		approved.ShouldBe(["The pilot landed.", "human", "generated", "gemini-3.7-flash", "summarize-anonymize.v3", "auth0|synthetic-officer", "2026-09-20 15:35:00"]);

		(await Scalar(connection, "SELECT approved_at IS NULL FROM summary_revisions WHERE summary_id = 'sdraft00001'")).ShouldBe("True");
		(await Scalar(connection, "SELECT deleted IS NOT NULL FROM summary_revisions WHERE summary_id = 'sdeleted001'")).ShouldBe("True");
		(await Scalar(connection, "SELECT string_agg(column_name, ',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name = 'summaries'"))
			.ShouldBe("id,report_id,deleted");
	}

	[Fact]
	public async Task GivenExistingSummaries_WhenMigrationRuns_ThenThePublicFeedStillShowsTheApprovedPairAndNothingElse()
	{
		// Given
		var connectionString = await BeforeTheMigration();

		// When
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, null);
		}

		// Then
		await using var connection = await Open(connectionString);
		(await Scalar(connection, "SELECT string_agg(id, ',') FROM public_reports")).ShouldBe("rapproved01");
		(await Scalar(connection, "SELECT ai_summary_en FROM public_reports")).ShouldBe("The pilot landed.");
	}

	[Fact]
	public async Task GivenRevisions_WhenTheViewsAreRead_ThenLatestIsTheNewestAndLatestApprovedIsTheNewestApproved()
	{
		// Given — revision 1 approved, revision 2 a draft, revision 3 approved, revision 4 a draft
		var connectionString = await BeforeTheMigration();
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, null);
		}

		await using var connection = await Open(connectionString);
		await Execute(
			connection,
			"""
			INSERT INTO summary_revisions
				(id, summary_id, sequence, ai_summary_en, ai_summary_fr, source_en, source_fr, model, prompt_version, author_subject, created_at, approved_by_subject, approved_at)
			VALUES ('rev00000002', 'sapproved01', 2, 'Draft two.', 'Brouillon deux.', 'human', 'human', 'm', 'v', 'auth0|a', TIMESTAMPTZ '2026-09-21T00:00:00Z', NULL, NULL),
			       ('rev00000003', 'sapproved01', 3, 'Approved three.', 'Approuvé trois.', 'human', 'human', 'm', 'v', 'auth0|a', TIMESTAMPTZ '2026-09-22T00:00:00Z', 'auth0|a', TIMESTAMPTZ '2026-09-22T00:00:00Z'),
			       ('rev00000004', 'sapproved01', 4, 'Draft four.', 'Brouillon quatre.', 'human', 'human', 'm', 'v', 'auth0|a', TIMESTAMPTZ '2026-09-23T00:00:00Z', NULL, NULL);
			""");

		// When / Then
		(await Scalar(connection, "SELECT sequence::text FROM latest_summary_revisions WHERE summary_id = 'sapproved01'")).ShouldBe("4");
		(await Scalar(connection, "SELECT sequence::text FROM latest_approved_summary_revisions WHERE summary_id = 'sapproved01'")).ShouldBe("3");

		// The public reads the newest approved text, never the newer draft
		(await Scalar(connection, "SELECT ai_summary_en FROM public_reports WHERE id = 'rapproved01'")).ShouldBe("Approved three.");

		// A deleted summary is in neither view
		(await Scalar(connection, "SELECT count(*)::text FROM latest_summary_revisions WHERE summary_id = 'sdeleted001'")).ShouldBe("0");
		(await Scalar(connection, "SELECT count(*)::text FROM latest_approved_summary_revisions WHERE summary_id = 'sdeleted001'")).ShouldBe("0");
	}

	[Fact]
	public async Task GivenRevisions_WhenTheQueueVersionIsRead_ThenItEndsInTheLatestRevisionsXmin()
	{
		// Given
		var connectionString = await BeforeTheMigration();
		await using var context = PostgresFixture.ContextFor(connectionString);
		await MigrateTo(context, null);
		await using var connection = await Open(connectionString);

		// When
		var version = await Scalar(connection, "SELECT version FROM admin_report_queue WHERE id = 'rapproved01'");
		var latestXmin = await Scalar(connection, "SELECT xmin::text FROM summary_revisions WHERE summary_id = 'sapproved01' AND sequence = 1");

		// Then — the same number the API's concurrency token reads (ADR-0105)
		version.ShouldEndWith("." + latestXmin);
	}

	[Fact]
	public async Task GivenMigratedRevisions_WhenMigratedBack_ThenEachSummaryHoldsItsLatestRevision()
	{
		// Given
		var connectionString = await BeforeTheMigration();
		await using var context = PostgresFixture.ContextFor(connectionString);
		await MigrateTo(context, null);
		await using var connection = await Open(connectionString);
		await Execute(
			connection,
			"""
			INSERT INTO summary_revisions
				(id, summary_id, sequence, ai_summary_en, ai_summary_fr, source_en, source_fr, model, prompt_version, author_subject, created_at, approved_by_subject, approved_at)
			VALUES ('rev00000002', 'sapproved01', 2, 'Edited.', 'Modifié.', 'human', 'human', 'm', 'v', 'auth0|a', TIMESTAMPTZ '2026-09-21T00:00:00Z', NULL, NULL);
			""");

		// When
		await MigrateTo(context, PriorMigration);

		// Then — one row per report again, holding the current text, and the views read it
		(await Scalar(connection, "SELECT ai_summary_en FROM summaries WHERE id = 'sapproved01'")).ShouldBe("Edited.");
		(await Scalar(connection, "SELECT generated_at::text FROM summaries WHERE id = 'sapproved01'"))!.ShouldContain("2026-09-20");
		(await Scalar(connection, "SELECT to_regclass('summary_revisions')::text")).ShouldBeNull();
		(await Scalar(connection, "SELECT count(*)::text FROM public_reports")).ShouldBe("0");
	}

	/// <summary>A database one migration short, with an approved, a draft, and a deleted summary.</summary>
	private async Task<string> BeforeTheMigration()
	{
		var connectionString = await postgres.CreateDatabase();
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, PriorMigration);
		}

		await using var connection = await Open(connectionString);
		await Execute(
			connection,
			"""
			INSERT INTO reports (id, language, status, submitted_at, consent_publish)
			VALUES ('rapproved01', 'en-CA', 'published', TIMESTAMPTZ '2026-09-20T00:00:00Z', TRUE),
			       ('rdraft00001', 'en-CA', 'pending', TIMESTAMPTZ '2026-09-20T00:00:00Z', TRUE),
			       ('rdeleted001', 'en-CA', 'pending', TIMESTAMPTZ '2026-09-20T00:00:00Z', TRUE);
			UPDATE reports SET deleted = TIMESTAMPTZ '2026-09-25T00:00:00Z' WHERE id = 'rdeleted001';
			INSERT INTO summaries
				(id, report_id, ai_summary_en, ai_summary_fr, source_en, source_fr, model, prompt_version, approved_by_subject, approved_at, generated_at, updated_at, deleted)
			VALUES ('sapproved01', 'rapproved01', 'The pilot landed.', 'Le pilote s''est posé.', 'human', 'generated', 'gemini-3.7-flash', 'summarize-anonymize.v3',
			        'auth0|synthetic-officer', TIMESTAMPTZ '2026-09-21T00:00:00Z', TIMESTAMPTZ '2026-09-20T15:35:00Z', TIMESTAMPTZ '2026-09-21T00:00:00Z', NULL),
			       ('sdraft00001', 'rdraft00001', 'A draft.', 'Un brouillon.', 'generated', 'generated', 'gemini-3.7-flash', 'summarize-anonymize.v3',
			        NULL, NULL, TIMESTAMPTZ '2026-09-20T15:35:00Z', TIMESTAMPTZ '2026-09-20T15:35:00Z', NULL),
			       ('sdeleted001', 'rdeleted001', 'Deleted.', 'Supprimé.', 'generated', 'generated', 'gemini-3.7-flash', 'summarize-anonymize.v3',
			        NULL, NULL, TIMESTAMPTZ '2026-09-20T15:35:00Z', TIMESTAMPTZ '2026-09-20T15:35:00Z', TIMESTAMPTZ '2026-09-25T00:00:00Z');
			""");

		return connectionString;
	}

	private static async Task MigrateTo(HpacSafetyDbContext context,
										string? targetMigration)
	{
		await context.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(targetMigration);
	}

	private static async Task<NpgsqlConnection> Open(string connectionString)
	{
		var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		return connection;
	}

	private static async Task Execute(NpgsqlConnection connection,
									  string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<string?> Scalar(NpgsqlConnection connection,
											  string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		var value = await command.ExecuteScalarAsync();
		return value is null or DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
	}

	private static async Task<string?[]> Row(NpgsqlConnection connection,
											 string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();
		await reader.ReadAsync();
		return [.. Enumerable.Range(0, reader.FieldCount)
			.Select(index => reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))];
	}
}
