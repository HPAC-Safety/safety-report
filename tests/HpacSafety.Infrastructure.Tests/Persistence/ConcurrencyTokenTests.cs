using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     The review version built from PostgreSQL's <c>xmin</c> on a report and its
///     summary, and the stale-save refusal it drives (ADR-0105). Every report is
///     synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class ConcurrencyTokenTests(PostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenReportWithSummary_WhenVersionIsRead_ThenItNamesBothRowVersions()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var id = await SeedPending(connectionString, withSummary: true);
		await using var context = PostgresFixture.ContextFor(connectionString);
		var report = await Load(context, id);

		// When
		var version = ConcurrencyToken.Of(context, report);

		// Then
		var parts = version.Split('.');
		parts.Length.ShouldBe(2);
		uint.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(0u);
		uint.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(0u);
	}

	[Fact]
	public async Task GivenReportWithoutSummary_WhenVersionIsRead_ThenTheSummaryPartIsZero()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var id = await SeedPending(connectionString, withSummary: false);
		await using var context = PostgresFixture.ContextFor(connectionString);
		var report = await Load(context, id);

		// When
		var version = ConcurrencyToken.Of(context, report);

		// Then
		version.ShouldEndWith(".0");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("7")]
	[InlineData("a.b")]
	[InlineData("1.2.3")]
	public async Task GivenVersionThisSystemNeverIssued_WhenExpected_ThenRefused(string? version)
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var id = await SeedPending(connectionString, withSummary: true);
		await using var context = PostgresFixture.ContextFor(connectionString);
		var report = await Load(context, id);

		// When / Then
		ConcurrencyToken.Expect(context, report, version).ShouldBeFalse();
	}

	[Fact]
	public async Task GivenCurrentVersion_WhenSaved_ThenTheChangeCommitsAndTheVersionMoves()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var id = await SeedPending(connectionString, withSummary: true);
		await using var context = PostgresFixture.ContextFor(connectionString);
		var report = await Load(context, id);
		var before = ConcurrencyToken.Of(context, report);

		// When
		ConcurrencyToken.Expect(context, report, before).ShouldBeTrue();
		report.EditSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", "subject-officer", At);
		await context.SaveChangesAsync(TestContext.Current.CancellationToken);

		// Then
		ConcurrencyToken.Of(context, report).ShouldNotBe(before);
	}

	[Fact]
	public async Task GivenAnotherSaveSinceTheViewLoaded_WhenApprovedWithTheOldVersion_ThenRefusedAsAConcurrencyConflict()
	{
		// Given — a first reviewer's view, then a second reviewer's saved edit
		var connectionString = await postgres.CreateMigratedDatabase();
		var id = await SeedPending(connectionString, withSummary: true);
		await using var first = PostgresFixture.ContextFor(connectionString);
		var loaded = await Load(first, id);
		var stale = ConcurrencyToken.Of(first, loaded);

		await using (var second = PostgresFixture.ContextFor(connectionString))
		{
			var theirs = await Load(second, id);
			theirs.EditSummary("Second reviewer.", "Second réviseur.", "subject-officer", At);
			await second.SaveChangesAsync(TestContext.Current.CancellationToken);
		}

		// When — the save itself refuses an approval of a revision that is no
		// longer the one the view loaded, even past the API's own before-save
		// comparison of the whole version
		await using var third = PostgresFixture.ContextFor(connectionString);
		var current = await Load(third, id);
		ConcurrencyToken.Of(third, current).ShouldNotBe(stale);
		ConcurrencyToken.Expect(third, current, stale).ShouldBeTrue();
		current.Publish("subject-officer", At);

		// Then
		await Should.ThrowAsync<DbUpdateConcurrencyException>(() => third.SaveChangesAsync());
	}

	[Fact]
	public async Task GivenTwoReviewersLoadedTheSameRevision_WhenBothSaveAnEdit_ThenTheSecondIsRefusedByTheSequenceIndex()
	{
		// Given — both loaded revision 1, so both mean to append revision 2
		var connectionString = await postgres.CreateMigratedDatabase();
		var id = await SeedPending(connectionString, withSummary: true);
		await using var first = PostgresFixture.ContextFor(connectionString);
		await using var second = PostgresFixture.ContextFor(connectionString);
		var firstView = await Load(first, id);
		var secondView = await Load(second, id);

		firstView.EditSummary("First reviewer.", "Premier réviseur.", "subject-officer", At);
		await first.SaveChangesAsync(TestContext.Current.CancellationToken);

		// When
		secondView.EditSummary("Second reviewer.", "Second réviseur.", "subject-officer", At);
		var saving = () => second.SaveChangesAsync();

		// Then — nothing is overwritten: the (summary, sequence) unique index refuses it
		var refusal = await Should.ThrowAsync<DbUpdateException>(saving);
		refusal.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("ix_summary_revisions_summary_id_sequence");
	}

	[Fact]
	public void GivenNullArguments_WhenUsed_ThenRefused()
	{
		// Given / When / Then
		Should.Throw<ArgumentNullException>(() => ConcurrencyToken.Of(null!, null!));
		Should.Throw<ArgumentNullException>(() => ConcurrencyToken.Expect(null!, null!, "1.1"));
	}

	private static Task<Report> Load(HpacSafetyDbContext context,
									 TinyId id)
	{
		return context.Reports.Include(report => report.Summary).SingleAsync(report => report.Id == id);
	}

	private static async Task<TinyId> SeedPending(string connectionString,
												  bool withSummary)
	{
		await using var context = PostgresFixture.ContextFor(connectionString);
		// The seeded system question, not a synthetic one: a role lives on at
		// most one live question (ix_questions_role, ADR-0154), and a fresh
		// migrated database already seeds the real one.
		var consent = await context.Questions.Include(question => question.Revisions)
			.SingleAsync(question => question.Role == QuestionRole.ConsentPublish);

		var report = new Report(Locale.EnCa, At);
		report.Answer(consent, withSummary, At);
		report.BeginSummarizing();

		if (withSummary)
		{
			report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", At));
			report.AwaitReview();
		}
		else
		{
			report.KeepUnpublished();
		}

		context.Reports.Add(report);
		await context.SaveChangesAsync();
		return report.Id;
	}
}
