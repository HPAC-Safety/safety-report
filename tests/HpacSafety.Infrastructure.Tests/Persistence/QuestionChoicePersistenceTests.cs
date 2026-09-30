using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     A question's own choices in <c>question_choices</c>: edited in place,
///     removed rows kept and loaded with the question, and a one-language
///     reporter choice stored as such (ADR-0095).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class QuestionChoicePersistenceTests(PostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenEditedChoices_WhenReloaded_ThenRemovedAndOneLanguageChoicesRoundTrip()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var question = Question.Create(
			"synthetic_site", QuestionType.Autocomplete, "Site", "Site", At, isActive: true,
			options: [new QuestionOptionInput("coopers", "Cooper's", "Cooper's"), new QuestionOptionInput("woodside", "Woodside", "Woodside")]);

		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			context.Questions.Add(question);
			await context.SaveChangesAsync();
		}

		// When — a reporter adds one, and an Administrator removes another, in place
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			var loaded = await context.Questions.Include(q => q.Revisions).Include(q => q.AllChoices)
				.SingleAsync(q => q.Id == question.Id);
			loaded.AddChoiceFromReporter("Élévation", Locale.FrCa);
			loaded.ReplaceChoices([new QuestionOptionInput("coopers", "Cooper's", "Cooper's"), new QuestionOptionInput("elevation", null, "Élévation")], At);
			await context.SaveChangesAsync();
		}

		// Then
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			var reloaded = await context.Questions.Include(q => q.Revisions).Include(q => q.AllChoices)
				.SingleAsync(q => q.Id == question.Id);

			reloaded.Revisions.Count.ShouldBe(1);
			reloaded.Choices.Select(choice => choice.Code).ShouldBe(["coopers", "elevation"], ignoreOrder: true);
			reloaded.AllChoices.Single(choice => choice.Code == "woodside").Deleted.ShouldNotBeNull();

			var elevation = reloaded.Choice("elevation")!;
			elevation.LabelEn.ShouldBeNull();
			elevation.ReporterLocale.ShouldBe(Locale.FrCa);
			elevation.AddedByReporter.ShouldBeTrue();
			reloaded.ReporterChoicesAwaitingReview.ShouldBe(1);
		}
	}

	[Fact]
	public async Task GivenAdministratorChoiceWithOneLanguage_WhenWrittenStraightToTheTable_ThenCheckConstraintRefusesIt()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		// A type-ahead, the one choice-taking type that may be saved with no choice yet.
		var question = Question.Create("synthetic_pick", QuestionType.Autocomplete, "Pick", "Choisir", At);

		await using var context = PostgresFixture.ContextFor(connectionString);
		context.Questions.Add(question);
		await context.SaveChangesAsync();

		// When
		var inserting = () => context.Database.ExecuteSqlAsync(
			$"INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_fr, added_by_reporter) VALUES ('choice00001', {question.Id.Value}, 'alpha', 0, 'Alpha', NULL, FALSE)");

		// Then
		await inserting.ShouldThrowAsync<Exception>();
	}

	[Fact]
	public async Task GivenChoiceAnswer_WhenReloaded_ThenItsOwnAccountReadsWithoutTheChoiceButItsOfficialValueDoesNot()
	{
		// Given — ADR-0175: the answer keeps its own submitted wording as a plain
		// column, readable without its choice; the choice's current official value
		// and the second language are still read through the loaded choice
		var connectionString = await postgres.CreateMigratedDatabase();
		var question = Question.Create(
			"synthetic_wing", QuestionType.SingleSelect, "Wing", "Aile", At, isActive: true,
			options: [new QuestionOptionInput("paraglider", "Paraglider", "Parapente")]);
		var report = new Report(Locale.EnCa, At);
		var answer = report.AnswerChoices(question, question.CurrentRevision, [question.Choices.Single().Id], At).Single();

		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			context.Questions.Add(question);
			context.Reports.Add(report);
			await context.SaveChangesAsync();
		}

		// When
		await using var reader = PostgresFixture.ContextFor(connectionString);
		var withChoice = await reader.ReportAnswers.AsNoTracking().Include(a => a.Choice).SingleAsync(a => a.Id == answer.Id);
		var withoutChoice = await reader.ReportAnswers.AsNoTracking().SingleAsync(a => a.Id == answer.Id);

		// Then
		withChoice.Value.ShouldBeNull();
		withChoice.Text.ShouldBe("Paraglider");
		withChoice.OfficialText.ShouldBe("Paraglider");
		withChoice.DisplayedTranslation.ShouldBe("Parapente");

		// The reporter's own account is a plain column: it reads without the choice.
		withoutChoice.IsAnswered.ShouldBeTrue();
		withoutChoice.Text.ShouldBe("Paraglider");
		withoutChoice.SubmittedWording.ShouldBe("Paraglider");

		// Its current official value and second language still need the choice —
		// refused rather than shown as skipped or silently blank.
		Should.Throw<InvalidOperationException>(() => withoutChoice.OfficialText);
		Should.Throw<InvalidOperationException>(() => withoutChoice.DisplayedTranslation);
	}
}
