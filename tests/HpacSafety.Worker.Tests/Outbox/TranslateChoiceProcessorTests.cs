using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     ADR-0129 against a real database: the Worker supplies the missing language
///     of a type-ahead value a reporter added, marked machine-translated, and
///     leaves the reporter's wording and any value a person already completed
///     alone.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedWorkerPostgres.Name)]
public sealed class TranslateChoiceProcessorTests(WorkerPostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenTranslateChoiceOutboxType_WhenAsked_ThenHandlesTranslateChoice()
	{
		// Given
		var processor = new TranslateChoiceProcessor(null!, new StubTranslator());

		// Then
		processor.HandlesType.ShouldBe(OutboxMessageType.TranslateChoice);
	}

	[Fact]
	public async Task GivenFrenchOnlyReporterValue_WhenProcessed_ThenItsEnglishIsMachineTranslated()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var (question, choice) = await SeedReporterValue(connectionString, "Élévation Sainte-Anne", Locale.FrCa);
		var translator = new StubTranslator();

		// When
		await using (var context = WorkerPostgresFixture.ContextFor(connectionString))
		{
			await new TranslateChoiceProcessor(context, translator).Process(Message(question, choice), CancellationToken.None);
			await context.SaveChangesAsync();
		}

		// Then
		var call = translator.Calls.ShouldHaveSingleItem();
		call.Texts.ShouldBe(["Élévation Sainte-Anne"]);
		(call.Source, call.Target).ShouldBe((Locale.FrCa, Locale.EnCa));

		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var stored = await reader.QuestionChoices.SingleAsync(candidate => candidate.Id == choice);
		stored.LabelEn.ShouldBe("[en-CA] Élévation Sainte-Anne");
		stored.LabelEnSource.ShouldBe(LabelSource.Auto);
		stored.LabelFr.ShouldBe("Élévation Sainte-Anne");
		stored.LabelFrSource.ShouldBe(LabelSource.Human);
		stored.NeedsTranslation.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenEnglishOnlyReporterValue_WhenProcessed_ThenItsFrenchIsMachineTranslated()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var (question, choice) = await SeedReporterValue(connectionString, "Mount 7", Locale.EnCa);
		var translator = new StubTranslator();

		// When
		await using (var context = WorkerPostgresFixture.ContextFor(connectionString))
		{
			await new TranslateChoiceProcessor(context, translator).Process(Message(question, choice), CancellationToken.None);
			await context.SaveChangesAsync();
		}

		// Then
		(translator.Calls.ShouldHaveSingleItem().Source, translator.Calls[0].Target).ShouldBe((Locale.EnCa, Locale.FrCa));
		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var stored = await reader.QuestionChoices.SingleAsync(candidate => candidate.Id == choice);
		stored.LabelFr.ShouldBe("[fr-CA] Mount 7");
		stored.LabelFrSource.ShouldBe(LabelSource.Auto);
	}

	[Fact]
	public async Task GivenValueAPersonAlreadyCompleted_WhenProcessed_ThenNothingIsSent()
	{
		// Given — a reviewer wrote the French before the Worker got to it
		var connectionString = await postgres.CreateMigratedDatabase();
		var (question, choice) = await SeedReporterValue(connectionString, "Mount 7", Locale.EnCa);

		await using (var context = WorkerPostgresFixture.ContextFor(connectionString))
		{
			var loaded = await context.Questions.Include(q => q.Revisions).Include(q => q.AllChoices)
				.SingleAsync(q => q.Id == question);
			loaded.ReplaceChoices([new QuestionOptionInput(loaded.AllChoices.Single().Code, "Mount 7", "Mont 7")], At);
			await context.SaveChangesAsync();
		}

		var translator = new StubTranslator();

		// When
		await using (var context = WorkerPostgresFixture.ContextFor(connectionString))
		{
			await new TranslateChoiceProcessor(context, translator).Process(Message(question, choice), CancellationToken.None);
			await context.SaveChangesAsync();
		}

		// Then
		translator.Calls.ShouldBeEmpty();
		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var stored = await reader.QuestionChoices.SingleAsync(candidate => candidate.Id == choice);
		stored.LabelFr.ShouldBe("Mont 7");
		stored.LabelFrSource.ShouldBe(LabelSource.Human);
	}

	[Fact]
	public async Task GivenAValueThatNoLongerExists_WhenProcessed_ThenNothingIsSent()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		var translator = new StubTranslator();

		// When
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		await new TranslateChoiceProcessor(context, translator)
			.Process(new OutboxMessage(TinyId.New(), OutboxMessageType.TranslateChoice, TinyId.New().Value, At), CancellationToken.None);

		// Then
		translator.Calls.ShouldBeEmpty();
	}

	private static OutboxMessage Message(TinyId question,
										 TinyId choice)
	{
		return new OutboxMessage(question, OutboxMessageType.TranslateChoice, choice.Value, At);
	}

	private static async Task<(TinyId Question, TinyId Choice)> SeedReporterValue(string connectionString,
																				   string typed,
																				   Locale locale)
	{
		var question = Question.Create("synthetic_site", QuestionType.Autocomplete, "Site", "Site", At, isActive: true);
		var choice = question.AddChoiceFromReporter(typed, locale);

		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		context.Questions.Add(question);
		await context.SaveChangesAsync();

		return (question.Id, choice.Id);
	}
}
