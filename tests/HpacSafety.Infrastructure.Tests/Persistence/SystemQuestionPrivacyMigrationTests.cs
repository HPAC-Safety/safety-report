using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>KeepSystemQuestionsPrivate</c>: a database seeded before system questions
///     had to be private gets a new, private revision of each one that is not, and
///     a database already in line is left alone (#450).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class SystemQuestionPrivacyMigrationTests(PostgresFixture postgres)
{
	private const string PriorMigration = "ReportIsPendingPublishedOrUnpublished";

	[Fact]
	public async Task GivenNonPrivateSystemQuestion_WhenMigrationRuns_ThenItGainsAPrivateRevision()
	{
		// Given — publication consent as it was seeded before this rule: not private
		var connectionString = await postgres.CreateDatabase();
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, PriorMigration);
		}

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		await Execute(connection, "UPDATE question_revisions SET is_private = FALSE WHERE is_system");
		var before = await CurrentRevisions(connection);
		before.ShouldNotBeEmpty();

		// When
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			await MigrateTo(context, null);
		}

		// Then — one new revision each, private, everything else carried forward
		var after = await CurrentRevisions(connection);
		after.Count.ShouldBe(before.Count);

		foreach (var (questionId, revision) in after)
		{
			revision.Number.ShouldBe(before[questionId].Number + 1);
			revision.IsPrivate.ShouldBeTrue();
			revision.LabelEn.ShouldBe(before[questionId].LabelEn);
			revision.IsActive.ShouldBeTrue();
		}
	}

	[Fact]
	public async Task GivenPrivateSystemQuestions_WhenScriptRunsAgain_ThenNothingChanges()
	{
		// Given — a database at current main
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		var before = await CurrentRevisions(connection);

		// When
		await Execute(connection, SqlScript.Read("20260925212255_KeepSystemQuestionsPrivate.sql"));

		// Then
		var after = await CurrentRevisions(connection);
		after.Values.ShouldAllBe(revision => revision.IsPrivate);
		after.Select(pair => (pair.Key, pair.Value.Number)).ShouldBe(before.Select(pair => (pair.Key, pair.Value.Number)), ignoreOrder: true);
	}

	private static async Task<Dictionary<string, Revision>> CurrentRevisions(NpgsqlConnection connection)
	{
		await using var command = new NpgsqlCommand(
			"""
			SELECT DISTINCT ON (r.question_id) r.question_id, r.revision_number, r.is_private, r.label_en, r.is_active
			FROM question_revisions r
			JOIN questions q ON q.id = r.question_id
			WHERE q.is_system AND q.deleted IS NULL AND r.deleted IS NULL
			ORDER BY r.question_id, r.revision_number DESC
			""",
			connection);
		await using var reader = await command.ExecuteReaderAsync();
		var revisions = new Dictionary<string, Revision>();

		while (await reader.ReadAsync())
		{
			revisions[reader.GetString(0)] = new Revision(reader.GetInt32(1), reader.GetBoolean(2), reader.GetString(3), reader.GetBoolean(4));
		}

		return revisions;
	}

	private static async Task MigrateTo(HpacSafetyDbContext context,
										string? targetMigration)
	{
		var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
		await migrator.MigrateAsync(targetMigration);
	}

	private static async Task Execute(NpgsqlConnection connection,
									  string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	private sealed record Revision(int Number, bool IsPrivate, string LabelEn, bool IsActive);
}
