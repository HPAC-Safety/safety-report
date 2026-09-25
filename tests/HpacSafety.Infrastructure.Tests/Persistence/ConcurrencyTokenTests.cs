using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
		report.EditSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", At);
		await context.SaveChangesAsync();

		// Then
		ConcurrencyToken.Of(context, report).ShouldNotBe(before);
	}

	[Fact]
	public async Task GivenAnotherSaveSinceTheViewLoaded_WhenSavedWithTheOldVersion_ThenRefusedAsAConcurrencyConflict()
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
			theirs.EditSummary("Second reviewer.", "Second réviseur.", At);
			await second.SaveChangesAsync();
		}

		// When — the save itself refuses a stale edit to the same row, even past
		// the API's own before-save comparison of the whole version
		await using var third = PostgresFixture.ContextFor(connectionString);
		var current = await Load(third, id);
		ConcurrencyToken.Of(third, current).ShouldNotBe(stale);
		ConcurrencyToken.Expect(third, current, stale).ShouldBeTrue();
		current.EditSummary("Third reviewer.", "Troisième réviseur.", At);

		// Then
		await Should.ThrowAsync<DbUpdateConcurrencyException>(() => third.SaveChangesAsync());
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
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);
		context.Questions.Add(consent);

		var report = new Report(Locale.EnCa, At);
		report.Answer(consent, [withSummary ? "yes" : "no"], At);
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
