using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A picker option is fixed in place or replaced, and a condition follows a
///     replacement (ADR-0128).
/// </summary>
public class PickerReplacementTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenAnOptionReplacedTwice_WhenItsCurrentChoiceIsAsked_ThenTheChainIsFollowedToTheEnd()
	{
		// Given
		var question = Wing();
		var original = question.Choice("paraglider")!.Id;
		Replace(question, "paraglider", "Paraglider (solo)");
		Replace(question, "paraglider_solo", "Paraglider (single)");

		// When
		var current = question.CurrentChoice(original);

		// Then
		current!.LabelEn.ShouldBe("Paraglider (single)");
		current.Deleted.ShouldBeNull();
	}

	[Fact]
	public void GivenAChoiceTheQuestionNeverHad_WhenItsCurrentChoiceIsAsked_ThenNone()
	{
		Wing().CurrentChoice(TinyId.New()).ShouldBeNull();
	}

	[Fact]
	public void GivenAReplacedOption_WhenTheQuestionForks_ThenTheCopiesKeepTheReplacementLink()
	{
		// Given
		var question = Wing();
		Replace(question, "paraglider", "Paraglider (solo)");
		var current = question.CurrentRevision;

		// When — a wording edit on an answered question forks it (ADR-0071)
		var live = question.ApplyEdit(
			true, current.Type, "Which wing?", current.LabelFr, current.IsPrivate, current.IsActive, current.DisplayOrder, At.AddDays(1));

		// Then — the copies are new rows, linked to each other
		live.ShouldNotBeSameAs(question);
		var retiredCopy = live.AllChoices.Single(choice => choice.Code == "paraglider");
		var replacementCopy = live.AllChoices.Single(choice => choice.Code == "paraglider_solo");
		retiredCopy.ReplacedByChoiceId.ShouldBe(replacementCopy.Id);
		live.CurrentChoice(retiredCopy.Id).ShouldBeSameAs(replacementCopy);
	}

	[Fact]
	public void GivenATypeAheadValue_WhenAskedToReplaceIt_ThenRefused()
	{
		// Given — a type-ahead value is corrected in place, never replaced (ADR-0129)
		var question = Question.Create(
			"launch", QuestionType.Autocomplete, "Launch", "Décollage", At, isActive: true,
			options: [new QuestionOptionInput("mount_7", "Mount 7", "Mont 7")]);

		// When
		var replacing = () => question.ReplaceChoices([new QuestionOptionInput("mount_7", "Mount Seven", "Mont Sept", Replace: true)], At);

		// Then
		replacing.ShouldThrow<DomainRuleViolationException>().Message.ShouldContain("corrected in place");
	}

	[Fact]
	public void GivenAReplacementWordedLikeAnOptionTheQuestionOnceHad_WhenSaved_ThenRefused()
	{
		// Given
		var question = Wing();
		Replace(question, "paraglider", "Paraglider (solo)");

		// When — replacing the new option back with the retired option's wording
		var replacing = () => Replace(question, "paraglider_solo", "Paraglider");

		// Then — a replacement is always a new option
		replacing.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenAReplacedOption_WhenAnAdministratorWritesItAgain_ThenItIsRevivedWithoutItsLink()
	{
		// Given
		var question = Wing();
		var original = question.Choice("paraglider")!;
		Replace(question, "paraglider", "Paraglider (solo)");

		// When — the retired option's code is written again, as a fix
		question.ReplaceChoices(
			[
				new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane"),
				new QuestionOptionInput("paraglider_solo", "Paraglider (solo)", "Parapente (solo)"),
				new QuestionOptionInput("paraglider", "Paraglider", "Parapente"),
			],
			At);

		// Then
		original.Deleted.ShouldBeNull();
		original.ReplacedByChoiceId.ShouldBeNull();
		question.CurrentChoice(original.Id).ShouldBeSameAs(original);
	}

	[Fact]
	public void GivenAConditionOnANonSingleSelectParent_WhenCheckedWithAChoice_ThenFalse()
	{
		// Given — a parent retyped away from single-select no longer enables anything by choice
		var parent = Wing();
		var child = Question.Create(
			"rating", QuestionType.ShortText, "Rating", "Qualification", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: parent.Choice("paraglider")!.Id);
		parent.ApplyEdit(false, QuestionType.MultiSelect, "Wing", "Aile", true, true, 0, At.AddDays(1));

		// When / Then
		child.CurrentRevision.IsEnabledGiven(parent, parent.Choice("paraglider")!.Id).ShouldBeFalse();
	}

	[Fact]
	public void GivenAnAnswerOnAReplacedOption_WhenRead_ThenItKeepsTheOldWording()
	{
		// Given
		var question = Wing();
		var answer = new Report(Locale.EnCa, At).Answer(question, "Paraglider", At);

		// When
		Replace(question, "paraglider", "Paraglider (solo)");

		// Then
		answer.Text.ShouldBe("Paraglider");
		question.OfferedChoiceLabelled("Paraglider", Locale.EnCa).ShouldBeNull();
	}

	private static Question Wing()
	{
		return Question.Create(
			"wing", QuestionType.SingleSelect, "Wing", "Aile", At, isActive: true,
			options: [new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane"), new QuestionOptionInput("paraglider", "Paraglider", "Parapente")]);
	}

	private static void Replace(Question question,
								string code,
								string wording)
	{
		question.ReplaceChoices(
			[
				.. question.Choices.Select(choice => choice.Code == code
					? new QuestionOptionInput(choice.Code, wording, $"{wording} (fr)", Replace: true)
					: new QuestionOptionInput(choice.Code, choice.LabelEn, choice.LabelFr)),
			],
			At);
	}
}
