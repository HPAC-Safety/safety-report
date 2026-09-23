using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
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
			reloaded.Choices.Select(choice => choice.Code).ShouldBe(["coopers", "elevation"]);
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
		var question = Question.Create("synthetic_pick", QuestionType.SingleSelect, "Pick", "Choisir", At);

		await using var context = PostgresFixture.ContextFor(connectionString);
		context.Questions.Add(question);
		await context.SaveChangesAsync();

		// When
		var inserting = () => context.Database.ExecuteSqlAsync(
			$"INSERT INTO question_choices (id, question_id, code, display_order, label_en, label_fr, added_by_reporter) VALUES ('choice00001', {question.Id.Value}, 'alpha', 0, 'Alpha', NULL, FALSE)");

		// Then
		await inserting.ShouldThrowAsync<Exception>();
	}
}
