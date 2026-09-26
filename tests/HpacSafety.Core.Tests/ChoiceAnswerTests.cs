using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A single-select, multi-select, or type-ahead answer names its choice by
///     identifier — what the submission sends — and only a live choice of that
///     question (ADR-0128).
/// </summary>
public class ChoiceAnswerTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenNoChoiceNamed_WhenRecorded_ThenOneSkippedAnswer()
	{
		// Given
		var question = Conditions();

		// When
		var answers = new Report(Locale.EnCa, Now).AnswerChoices(question, question.CurrentRevision, [], Now);

		// Then
		answers.ShouldHaveSingleItem().IsAnswered.ShouldBeFalse();
		answers[0].ChoiceId.ShouldBeNull();
	}

	[Fact]
	public void GivenTwoChoicesForASingleSelect_WhenRecorded_ThenRefused()
	{
		// Given
		var question = Question.Create(
			"wing", QuestionType.SingleSelect, "Wing", "Aile", Now, isActive: true,
			options: [new QuestionOptionInput("paraglider", "Paraglider", "Parapente"), new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane")]);
		var both = question.Choices.Select(choice => choice.Id).ToList();

		// When
		var recording = () => new Report(Locale.EnCa, Now).AnswerChoices(question, question.CurrentRevision, both, Now);

		// Then
		recording.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("takes one answer");
	}

	[Fact]
	public void GivenTheSameChoiceTwice_WhenRecorded_ThenRefused()
	{
		// Given
		var question = Conditions();
		var gusty = question.Choices[0].Id;

		// When
		var recording = () => new Report(Locale.EnCa, Now).AnswerChoices(question, question.CurrentRevision, [gusty, gusty], Now);

		// Then
		recording.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("same choice more than once");
	}

	[Fact]
	public void GivenSeveralChoices_WhenRecorded_ThenOneAnswerNamesEach()
	{
		// Given
		var question = Conditions();
		var chosen = question.Choices.Select(choice => choice.Id).ToList();

		// When
		var answers = new Report(Locale.FrCa, Now).AnswerChoices(question, question.CurrentRevision, chosen, Now);

		// Then
		answers.Select(answer => answer.ChoiceId!.Value).ShouldBe(chosen);
		answers.Select(answer => answer.Text).ShouldBe(["Rafales", "Thermique"], ignoreOrder: true);
	}

	[Fact]
	public void GivenAQuestionThatTakesNoChoice_WhenAChoiceIsNamed_ThenRefused()
	{
		// Given
		var question = Question.Create("details", QuestionType.ShortText, "Details", "Détails", Now, isActive: true);
		var other = Conditions();

		// When
		var recording = () => new Report(Locale.EnCa, Now)
			.AnswerChoices(question, question.CurrentRevision, [other.Choices[0].Id], Now);

		// Then
		recording.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("does not take a choice");
	}

	[Fact]
	public void GivenAnotherQuestionsRevision_WhenAChoiceIsNamed_ThenRefused()
	{
		// Given
		var question = Conditions();
		var other = Conditions();

		// When
		var recording = () => new Report(Locale.EnCa, Now)
			.AnswerChoices(question, other.CurrentRevision, [question.Choices[0].Id], Now);

		// Then
		recording.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenAFrenchOnlyChoice_WhenItsOtherLabelIsAskedFor_ThenNone()
	{
		// Given
		var question = Question.Create("launch", QuestionType.Autocomplete, "Launch", "Décollage", Now, isActive: true);
		var choice = question.AddChoiceFromReporter("Élévation Sainte-Anne", Locale.FrCa);

		// When / Then
		choice.OtherLabel(Locale.FrCa).ShouldBeNull();
		choice.Label(Locale.EnCa).ShouldBe("Élévation Sainte-Anne");
	}

	[Fact]
	public void GivenAChoiceWithBothLanguages_WhenAMachineTranslationArrives_ThenNothingChanges()
	{
		// Given — ADR-0129: a person's wording is never overwritten by the Worker
		var choice = Conditions().Choice("gusty")!;

		// When
		var supplied = choice.SupplyAutoTranslation("Venteux");

		// Then
		supplied.ShouldBeFalse();
		choice.LabelFr.ShouldBe("Rafales");
		choice.LabelFrSource.ShouldBe(LabelSource.Human);
	}

	[Fact]
	public void GivenABlankMachineTranslation_WhenSupplied_ThenRefused()
	{
		// Given
		var question = Question.Create("launch", QuestionType.Autocomplete, "Launch", "Décollage", Now, isActive: true);
		var choice = question.AddChoiceFromReporter("Mount 7", Locale.EnCa);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => choice.SupplyAutoTranslation(" "));
		choice.LabelFr.ShouldBeNull();
	}

	[Fact]
	public void GivenAMachineTranslatedLabel_WhenAnAdministratorResavesOrRewritesIt_ThenItsSourceFollows()
	{
		// Given
		var question = Question.Create("launch", QuestionType.Autocomplete, "Launch", "Décollage", Now, isActive: true);
		var choice = question.AddChoiceFromReporter("Mount 7", Locale.EnCa);
		choice.SupplyAutoTranslation("Mont 7").ShouldBeTrue();

		// When — the editor resaves it unchanged
		question.ReplaceChoices([new QuestionOptionInput(choice.Code, "Mount 7", "Mont 7")], Now);

		// Then — still the machine's
		choice.LabelFrSource.ShouldBe(LabelSource.Auto);
		choice.LabelEnSource.ShouldBe(LabelSource.Human);

		// When — a person rewrites it
		question.ReplaceChoices([new QuestionOptionInput(choice.Code, "Mount 7", "Mont Sept")], Now);

		// Then — now theirs
		choice.LabelFrSource.ShouldBe(LabelSource.Human);
	}

	private static Question Conditions()
	{
		return Question.Create(
			"conditions", QuestionType.MultiSelect, "Conditions", "Conditions", Now, isActive: true,
			options: [new QuestionOptionInput("gusty", "Gusty", "Rafales"), new QuestionOptionInput("thermic", "Thermic", "Thermique")]);
	}
}
