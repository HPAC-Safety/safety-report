using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>RefuseChangesToTheReportersAccount</c> guards the reporter's account and
///     summary revisions in the database (REQ-DOM-018 to REQ-DOM-029, ADR-0178).
///     Which column each trigger refuses is proven, statement by statement, in the
///     acceptance scenarios; this proves the migration installs and removes them,
///     that they leave rows already stored untouched, and that the application's own
///     soft deletion of a report still commits under them.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class ReporterImmutabilityTriggerTests(PostgresFixture postgres)
{
	private const string PriorMigration = "AddSummaryRevisions";

	private static readonly DateTimeOffset At = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

	private static readonly string[] Triggers =
	[
		"report_answers_immutable",
		"report_answers_never_truncated",
		"report_files_immutable",
		"report_files_never_truncated",
		"reports_immutable",
		"reports_never_truncated",
		"summary_revisions_immutable",
		"summary_revisions_never_truncated",
	];

	[Fact]
	public async Task GivenAFreshDatabase_WhenMigrated_ThenEachGuardedTableHasItsTrigger()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();

		// When
		await using var connection = await Open(connectionString);

		// Then
		(await Scalar(connection, TriggerNames)).ShouldBe(string.Join(',', Triggers));
	}

	[Fact]
	public async Task GivenRowsStoredBeforeTheMigration_WhenItRunsAndIsUndone_ThenNoRowChangesAndNoTriggerOrFunctionRemains()
	{
		// Given — a report written before the triggers exist
		var connectionString = await postgres.CreateDatabase();
		await using var context = PostgresFixture.ContextFor(connectionString);
		await MigrateTo(context, PriorMigration);
		await using var connection = await Open(connectionString);
		await Execute(connection, "INSERT INTO reports (id, language, status, submitted_at, consent_publish) VALUES ('rprior00001', 'en-CA', 'pending', TIMESTAMPTZ '2026-09-20T00:00:00Z', TRUE)");
		// Without receipt_hash, a column a later migration adds (ADR-0196): this test is
		// about the triggers, not about what other migrations add to the row.
		var before = await Scalar(connection, "SELECT (to_jsonb(r) - 'receipt_hash')::text FROM reports AS r WHERE id = 'rprior00001'");

		// When
		await MigrateTo(context, null);

		// Then — installed, and the stored row is exactly as it was
		(await Scalar(connection, TriggerNames)).ShouldBe(string.Join(',', Triggers));
		(await Scalar(connection, "SELECT (to_jsonb(r) - 'receipt_hash')::text FROM reports AS r WHERE id = 'rprior00001'")).ShouldBe(before);

		// When
		await MigrateTo(context, PriorMigration);

		// Then — removed, with their functions, and a locked column is writable again
		(await Scalar(connection, TriggerNames)).ShouldBeNull();
		(await Scalar(connection, "SELECT count(*)::text FROM pg_proc WHERE proname LIKE 'enforce\\_%\\_immutability'")).ShouldBe("0");
		(await Scalar(connection, "SELECT count(*)::text FROM pg_proc WHERE proname = 'refuse_truncate_of_reporters_account'")).ShouldBe("0");
		await Execute(connection, "UPDATE reports SET language = 'fr-CA' WHERE id = 'rprior00001'");
	}

	[Fact]
	public async Task GivenAReportWithEveryOwnedRow_WhenSoftDeleted_ThenTheCascadeCommitsAndEveryRowIsStampedOnce()
	{
		// Given — a report with an answer, a file, and a summary revision, written through the model
		var connectionString = await postgres.CreateMigratedDatabase();
		string reportId;
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			var question = Question.Create("synthetic_answer", QuestionType.ShortText, "A synthetic question", "Une question synthétique", At);
			context.Questions.Add(question);
			var report = new Report(Locale.EnCa, At);
			report.Answer(question, "A synthetic answer.", At);
			report.AddFile("report/original/file", "image/jpeg", 1024, At);
			report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "model", "prompt.v1", At));
			context.Reports.Add(report);
			await context.SaveChangesAsync();
			reportId = report.Id.Value;
		}

		// When
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			var report = await context.Reports
				.Include(report => report.Answers)
				.Include(report => report.Files)
				.Include(report => report.Summary!)
				.ThenInclude(summary => summary.Revisions)
				.SingleAsync(report => report.Id == TinyId.Parse(reportId));
			report.SoftDelete(At.AddHours(1));
			await context.SaveChangesAsync();
		}

		// Then
		await using var connection = await Open(connectionString);
		(await Scalar(connection, "SELECT count(*)::text FROM reports WHERE deleted IS NOT NULL")).ShouldBe("1");
		(await Scalar(connection, "SELECT count(*)::text FROM report_answers WHERE deleted IS NOT NULL")).ShouldBe("1");
		(await Scalar(connection, "SELECT count(*)::text FROM report_files WHERE deleted IS NOT NULL")).ShouldBe("1");
		(await Scalar(connection, "SELECT count(*)::text FROM summary_revisions WHERE deleted IS NOT NULL")).ShouldBe("1");
		(await Scalar(connection, "SELECT count(*)::text FROM summaries WHERE deleted IS NOT NULL")).ShouldBe("1");
	}

	private const string TriggerNames =
		"SELECT string_agg(tgname, ',' ORDER BY tgname) FROM pg_trigger WHERE NOT tgisinternal AND (tgname LIKE '%\\_immutable' OR tgname LIKE '%\\_never\\_truncated')";

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
}
