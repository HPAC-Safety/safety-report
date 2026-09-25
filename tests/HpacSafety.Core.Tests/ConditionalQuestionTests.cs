using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Conditional questions (ADR-0060) and the authored required flag (ADR-0061).
/// </summary>
public class ConditionalQuestionTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

	private static Question Ordinary(string key,
									 QuestionType type,
									 TinyId? dependsOn = null)
	{
		// A single- or multi-select is never saved without a choice.
		IReadOnlyList<QuestionOptionInput>? options = type is QuestionType.SingleSelect or QuestionType.MultiSelect
			? [new QuestionOptionInput("first", "First", "Premier")]
			: null;

		return Question.Create(
			key, type, $"Question {key}", $"Question {key} (fr)", At, isActive: true, dependsOnQuestionId: dependsOn,
			options: options);
	}

	[Fact]
	public void GivenParentQuestion_WhenChildNames_ThenChildRecordsDependency()
	{
		// Given
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);

		// When
		var child = Ordinary("injury_detail", QuestionType.LongText, parent.Id);

		// Then — the stable question, so rewording the parent cannot break this
		child.DependsOnQuestionId.ShouldBe(parent.Id);
		child.CurrentRevision.DependsOnQuestionId.ShouldBe(parent.Id);
	}

	[Fact]
	public void GivenQuestion_WhenNamesItself_ThenRejected()
	{
		// Given
		var question = Ordinary("were_you_injured", QuestionType.YesNo);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => question.DependOn(question.Id, null, At.AddHours(1)));
	}

	[Fact]
	public void GivenPublicationConsent_WhenMadeConditional_ThenRejected()
	{
		// Given
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);
		var other = Ordinary("were_you_injured", QuestionType.YesNo);

		// When / Then — a form that can skip asking consent cannot publish anything
		Should.Throw<DomainRuleViolationException>(() => consent.DependOn(other.Id, null, At.AddHours(1)));
	}

	[Fact]
	public void GivenConditionalQuestion_WhenDependencyIsCleared_ThenNewRevisionRecords()
	{
		// Given
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		var child = Ordinary("injury_detail", QuestionType.LongText, parent.Id);

		// When
		child.DependOn(null, null, At.AddHours(1));

		// Then
		child.DependsOnQuestionId.ShouldBeNull();
		child.CurrentRevision.RevisionNumber.ShouldBe(2);
	}

	[Fact]
	public void GivenNonBooleanParent_WhenBankChecksDependency_ThenRejected()
	{
		// Given
		var parent = Ordinary("glider_make", QuestionType.ShortText);

		// When / Then
		var cause = Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([parent], null, parent.Id));

		cause.Message.ShouldContain("yes/no");
	}

	[Fact]
	public void GivenParentDoesNotExist_WhenBankChecksDependency_ThenRejected()
	{
		// Given
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([], null, parent.Id));
	}

	[Fact]
	public void GivenDeletedParent_WhenBankChecksDependency_ThenRejected()
	{
		// Given
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		parent.Delete(false, At.AddHours(1));

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([parent], null, parent.Id));
	}

	[Fact]
	public void GivenChild_WhenBankIsAskedToMakeParentDependOn_ThenCycleIsRejected()
	{
		// Given
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		var child = Ordinary("still_flying", QuestionType.YesNo, parent.Id);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([parent, child], parent.Id, child.Id));
	}

	[Fact]
	public void GivenLongerChain_WhenWouldCloseIntoCycle_ThenRejected()
	{
		// Given — first depends on nothing, second on first, third on second
		var first = Ordinary("first", QuestionType.YesNo);
		var second = Ordinary("second", QuestionType.YesNo, first.Id);
		var third = Ordinary("third", QuestionType.YesNo, second.Id);

		// When / Then — first depending on third would close the loop
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([first, second, third], first.Id, third.Id));
	}

	[Fact]
	public void GivenChainDoesNotClose_WhenBankChecks_ThenAllowed()
	{
		// Given
		var first = Ordinary("first", QuestionType.YesNo);
		var second = Ordinary("second", QuestionType.YesNo, first.Id);
		var unrelated = Ordinary("unrelated", QuestionType.LongText);

		// When / Then
		Should.NotThrow(() =>
			QuestionDependencies.EnsureDependencyAllowed([first, second, unrelated], unrelated.Id, second.Id));
	}

	[Fact]
	public void GivenExistingCycleAmongOtherQuestions_WhenCheckedAgainstUnrelatedTarget_ThenAllowed()
	{
		// Given — "first" and "second" already depend on each other, a shape
		// only this direct construction can produce (the higher-level API
		// never lets one form). Walking from "first" must notice it has
		// already visited "first" and stop, rather than loop forever or
		// wrongly report reaching "target".
		var first = Ordinary("first", QuestionType.YesNo);
		var second = Ordinary("second", QuestionType.YesNo, first.Id);
		first.DependOn(second.Id, null, At.AddHours(1));
		var target = Ordinary("target", QuestionType.LongText);

		// When / Then
		Should.NotThrow(() =>
			QuestionDependencies.EnsureDependencyAllowed([first, second, target], target.Id, first.Id));
	}

	[Fact]
	public void GivenQuestionIsOwnParent_WhenBankChecks_ThenRejected()
	{
		// Given
		var question = Ordinary("were_you_injured", QuestionType.YesNo);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([question], question.Id, question.Id));
	}

	[Fact]
	public void GivenOrdinaryQuestion_WhenAuthoredRequired_ThenRevisionRecords()
	{
		// Given / When
		var question = Question.Create(
			"occurrence_date", QuestionType.Date, "When?", "Quand ?", At, isActive: true, isRequired: true);

		// Then
		question.IsRequired.ShouldBeTrue();
	}

	[Fact]
	public void GivenPublicationConsent_WhenRevisedAsOptional_ThenStaysRequired()
	{
		// Given
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);

		// When
		var revision = consent.Revise(
			QuestionType.YesNo, "May we publish a summary?", "Pouvons-nous publier un résumé ?",
			true, true, 0, At.AddHours(1), isRequired: false);

		// Then — consent cannot be made skippable, whatever the caller asks for
		revision.IsRequired.ShouldBeTrue();
		revision.IsSystem.ShouldBeTrue();
	}

	[Fact]
	public void GivenRequiredQuestion_WhenReordered_ThenRequiredStateCarriesForward()
	{
		// Given
		var question = Question.Create(
			"occurrence_date", QuestionType.Date, "When?", "Quand ?", At, isActive: true, isRequired: true);

		// When
		question.Reorder(3, At.AddHours(1));

		// Then — a reorder changes position and nothing else
		question.IsRequired.ShouldBeTrue();
		question.DisplayOrder.ShouldBe(3);
	}

	[Theory]
	[InlineData(QuestionType.Autocomplete, true)]
	[InlineData(QuestionType.SingleSelect, true)]
	[InlineData(QuestionType.MultiSelect, true)]
	[InlineData(QuestionType.YesNo, false)]
	[InlineData(QuestionType.Time, false)]
	[InlineData(QuestionType.ShortText, false)]
	public void GivenQuestionType_WhenOptionBehaviourIsRead_ThenMatchesContract(
		QuestionType type,
		bool takesChoices)
	{
		// Given
		var question = Ordinary("authored", type);

		// When
		var saving = () => question.ReplaceChoices([new QuestionOptionInput("golden", "Golden", "Golden")], At);

		// Then
		if (takesChoices)
		{
			saving();
			question.Choices.Count.ShouldBe(1);
		}
		else
		{
			saving.ShouldThrow<DomainRuleViolationException>();
		}
	}

	[Fact]
	public void GivenAutocompleteQuestion_WhenCreatedWithOptions_ThenStoresAndAcceptsThem()
	{
		// Given / When
		var question = Question.Create(
			"launch_site", QuestionType.Autocomplete, "Where from?", "D'où ?", At, isActive: true,
			options: [new QuestionOptionInput("golden", "Golden", "Golden")]);

		// Then
		question.Choices.Count.ShouldBe(1);
		question.OfferedChoiceLabelled("Golden", Locale.EnCa).ShouldNotBeNull();
		question.OfferedChoiceLabelled("Lumby", Locale.EnCa).ShouldBeNull();
		question.CurrentRevision.TakesOneAnswer.ShouldBeTrue();
	}

	[Fact]
	public void GivenTimeQuestion_WhenCreatedWithOptions_ThenRejected()
	{
		// Given / When / Then
		Should.Throw<DomainRuleViolationException>(() => Question.Create(
			"occurrence_time", QuestionType.Time, "What time?", "À quelle heure ?", At, isActive: true,
			options: [new QuestionOptionInput("noon", "Noon", "Midi")]));
	}

	// ------------------------------------------ single-select parents (ADR-0074) --

	private static Question PilotType()
	{
		return Question.Create(
			"pilot_type", QuestionType.SingleSelect, "Hang glider or paraglider?", "Deltaplane ou parapente ?", At,
			isActive: true,
			options:
			[
				new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane"),
				new QuestionOptionInput("paraglider", "Paraglider", "Parapente"),
			]);
	}

	[Fact]
	public void GivenSingleSelectParentAndValidOption_WhenBankChecksDependency_ThenAllowed()
	{
		// Given
		var parent = PilotType();

		// When / Then
		Should.NotThrow(() =>
			QuestionDependencies.EnsureDependencyAllowed([parent], null, parent.Id, parent.Choice("hang_glider")!.Id));
	}

	[Fact]
	public void GivenSingleSelectParentAndUnofferedOption_WhenBankChecksDependency_ThenRejected()
	{
		// Given
		var parent = PilotType();

		// When / Then
		var cause = Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([parent], null, parent.Id, PilotType().Choice("hang_glider")!.Id));

		cause.Message.ShouldContain("does not currently offer");
	}

	[Fact]
	public void GivenSingleSelectParentAndNoOption_WhenBankChecksDependency_ThenRejected()
	{
		// Given
		var parent = PilotType();

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([parent], null, parent.Id));
	}

	[Fact]
	public void GivenYesNoParentAndAnOption_WhenBankChecksDependency_ThenRejected()
	{
		// Given — the yes/no condition is always "answered yes"; naming an
		// option alongside it would be a second, contradictory condition.
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([parent], null, parent.Id, TinyId.New()));
	}

	[Fact]
	public void GivenMultiSelectParent_WhenBankChecksDependency_ThenRejected()
	{
		// Given — "contains" is a different condition than "equals", and out
		// of scope for this decision.
		var parent = Question.Create(
			"aircraft_type", QuestionType.MultiSelect, "Aircraft type", "Type d'aéronef", At, isActive: true,
			options: [new QuestionOptionInput("hang_glider", "Hang glider", "Deltaplane")]);

		// When / Then
		var cause = Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([parent], null, parent.Id, parent.Choice("hang_glider")!.Id));

		cause.Message.ShouldContain("yes/no or single-select");
	}

	[Fact]
	public void GivenRequiredOptionWithNoParent_WhenQuestionIsCreated_ThenRejected()
	{
		// Given / When / Then
		Should.Throw<DomainRuleViolationException>(() => Question.Create(
			"injury_detail", QuestionType.LongText, "What was the injury?", "Quelle était la blessure ?", At,
			isActive: true, dependsOnChoiceId: TinyId.New()));
	}

	[Fact]
	public void GivenSingleSelectDependency_WhenChildRecordsIt_ThenItNamesTheChoiceById()
	{
		// Given
		var parent = PilotType();
		var hangGlider = parent.Choice("hang_glider")!.Id;

		// When
		var child = Question.Create(
			"rating", QuestionType.ShortText, "Rating", "Qualification", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: hangGlider);

		// Then — ADR-0128
		child.DependsOnChoiceId.ShouldBe(hangGlider);
	}

	// -------------------------------------------------- IsEnabledGiven (ADR-0074) --

	[Fact]
	public void GivenUnconditionalQuestion_WhenEnabledIsChecked_ThenAlwaysTrue()
	{
		// Given
		var question = Ordinary("occurrence_notes", QuestionType.LongText);

		// When / Then
		question.CurrentRevision.IsEnabledGiven(null, (TinyId?)null).ShouldBeTrue();
	}

	[Theory]
	[InlineData(true, true)]
	[InlineData(false, false)]
	[InlineData(null, false)]
	public void GivenYesNoParent_WhenEnabledIsChecked_ThenMatchesTheAnswer(bool? parentAnswer,
																		   bool expected)
	{
		// Given — only true enables it (ADR-0130)
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		var child = Ordinary("injury_detail", QuestionType.LongText, parent.Id);

		// When / Then
		child.CurrentRevision.IsEnabledGiven(parent, parentAnswer).ShouldBe(expected);
	}

	[Fact]
	public void GivenYesNoParent_WhenEnabledIsCheckedWithAChoice_ThenFalse()
	{
		// Given — a yes/no answer is a boolean, never a choice (ADR-0130)
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		var child = Ordinary("injury_detail", QuestionType.LongText, parent.Id);

		// When / Then
		child.CurrentRevision.IsEnabledGiven(parent, TinyId.New()).ShouldBeFalse();
	}

	[Fact]
	public void GivenSingleSelectParent_WhenEnabledIsCheckedWithMatchingAnswer_ThenTrue()
	{
		// Given
		var parent = PilotType();
		var child = Question.Create(
			"rating", QuestionType.ShortText, "Rating", "Qualification", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: parent.Choice("hang_glider")!.Id);

		// When / Then — the reporter's answer names the choice (ADR-0128)
		child.CurrentRevision.IsEnabledGiven(parent, parent.Choice("hang_glider")!.Id).ShouldBeTrue();
	}

	[Fact]
	public void GivenSingleSelectParent_WhenEnabledIsCheckedWithADifferentAnswer_ThenFalse()
	{
		// Given
		var parent = PilotType();
		var child = Question.Create(
			"rating", QuestionType.ShortText, "Rating", "Qualification", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: parent.Choice("hang_glider")!.Id);

		// When / Then
		child.CurrentRevision.IsEnabledGiven(parent, parent.Choice("paraglider")!.Id).ShouldBeFalse();
	}

	[Fact]
	public void GivenConditionalQuestion_WhenParentNotYetAnswered_ThenFalse()
	{
		// Given
		var parent = PilotType();
		var child = Question.Create(
			"rating", QuestionType.ShortText, "Rating", "Qualification", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: parent.Choice("hang_glider")!.Id);

		// When / Then
		child.CurrentRevision.IsEnabledGiven(parent, (TinyId?)null).ShouldBeFalse();
	}

	[Fact]
	public void GivenConditionalQuestion_WhenParentNotSupplied_ThenFalse()
	{
		// Given
		var parent = PilotType();
		var child = Question.Create(
			"rating", QuestionType.ShortText, "Rating", "Qualification", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: parent.Choice("hang_glider")!.Id);

		// When / Then — caller has not loaded the parent
		child.CurrentRevision.IsEnabledGiven(null, parent.Choice("hang_glider")!.Id).ShouldBeFalse();
	}

	[Fact]
	public void GivenUnconditionalQuestion_WhenEnabledIsCheckedWithABoolean_ThenAlwaysTrue()
	{
		// Given
		var question = Ordinary("occurrence_notes", QuestionType.LongText);

		// When / Then
		question.CurrentRevision.IsEnabledGiven(null, (bool?)null).ShouldBeTrue();
	}

	[Fact]
	public void GivenYesNoCondition_WhenParentNotSupplied_ThenFalse()
	{
		// Given
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);
		var child = Ordinary("injury_detail", QuestionType.LongText, parent.Id);

		// When / Then — caller has not loaded the parent
		child.CurrentRevision.IsEnabledGiven(null, true).ShouldBeFalse();
	}

	[Fact]
	public void GivenSingleSelectParent_WhenEnabledIsCheckedWithABoolean_ThenFalse()
	{
		// Given — only a yes/no parent is answered with a boolean (ADR-0130)
		var parent = PilotType();
		var child = Question.Create(
			"rating", QuestionType.ShortText, "Rating", "Qualification", At, isActive: true,
			dependsOnQuestionId: parent.Id, dependsOnChoiceId: parent.Choice("hang_glider")!.Id);

		// When / Then
		child.CurrentRevision.IsEnabledGiven(parent, true).ShouldBeFalse();
	}
}
