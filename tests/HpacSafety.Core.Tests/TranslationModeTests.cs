using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Which answers get a second language, and where it comes from (ADR-0112).
/// </summary>
public class TranslationModeTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

	[Theory]
	[InlineData(QuestionType.LongText, true)]
	[InlineData(QuestionType.ShortText, false)]
	[InlineData(QuestionType.Email, false)]
	[InlineData(QuestionType.SingleSelect, false)]
	public void GivenNoFlag_WhenQuestionIsCreated_ThenTakesItsTypesDefault(QuestionType type,
																		 bool expected)
	{
		// Given / When
		var question = Question.Create("synthetic", type, "A question", "Une question", Now);

		// Then
		question.CurrentRevision.IsTranslatable.ShouldBe(expected);
	}

	[Theory]
	[InlineData(QuestionType.Email)]
	[InlineData(QuestionType.Phone)]
	[InlineData(QuestionType.Date)]
	[InlineData(QuestionType.YesNo)]
	[InlineData(QuestionType.MultiSelect)]
	[InlineData(QuestionType.Autocomplete)]
	public void GivenNonTextType_WhenMarkedTranslatable_ThenRefused(QuestionType type)
	{
		// Given / When
		var creating = () => Question.Create("synthetic", type, "A question", "Une question", Now, isTranslatable: true);

		// Then
		creating.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenTranslatableShortText_WhenReworded_ThenKeepsItsSetting()
	{
		// Given
		var question = Question.Create(
			"synthetic", QuestionType.ShortText, "What went wrong?", "Qu'est-ce qui n'a pas fonctionné?", Now,
			isTranslatable: true);

		// When — an edit that does not say, and keeps the type
		question.Revise(QuestionType.ShortText, "What went wrong, briefly?", "Qu'est-ce qui n'a pas fonctionné, en bref?",
			true, false, 0, Now.AddHours(1));

		// Then
		question.CurrentRevision.RevisionNumber.ShouldBe(2);
		question.CurrentRevision.IsTranslatable.ShouldBeTrue();
	}

	[Fact]
	public void GivenTranslatableLongText_WhenRetypedToEmail_ThenTakesTheNewTypesDefault()
	{
		// Given
		var question = Question.Create("synthetic", QuestionType.LongText, "Details", "Détails", Now);

		// When
		question.Revise(QuestionType.Email, "Email", "Courriel", true, false, 0, Now.AddHours(1));

		// Then — an email can never need translation
		question.CurrentRevision.IsTranslatable.ShouldBeFalse();
	}

	[Fact]
	public void GivenOnlyTheFlagChanges_WhenEditedUnanswered_ThenANewRevisionRecordsIt()
	{
		// Given
		var question = Question.Create("synthetic", QuestionType.ShortText, "Aircraft", "Aéronef", Now);
		var current = question.CurrentRevision;

		// When
		var live = question.ApplyEdit(
			false, current.Type, current.LabelEn, current.LabelFr, current.IsPrivate, current.IsActive,
			current.DisplayOrder, Now.AddHours(1), isTranslatable: true);

		// Then
		live.ShouldBeSameAs(question);
		question.CurrentRevision.RevisionNumber.ShouldBe(2);
		question.CurrentRevision.IsTranslatable.ShouldBeTrue();
	}

	[Theory]
	[InlineData(QuestionType.ShortText, "Avery")]
	[InlineData(QuestionType.Email, "avery@example.test")]
	[InlineData(QuestionType.Date, "2026-09-21")]
	[InlineData(QuestionType.YesNo, "yes")]
	public void GivenAnswerThatNeverHasASecondLanguage_WhenRecorded_ThenModeIsNone(QuestionType type,
																				 string value)
	{
		// Given
		var question = Question.Create("synthetic", type, "A question", "Une question", Now, isActive: true);
		var report = new Report(Locale.EnCa, Now);

		// When
		var answer = report.Answer(question, value, Now);

		// Then
		answer.TranslationMode.ShouldBe(TranslationMode.None);
		answer.NeedsTranslation.ShouldBeFalse();
		answer.TranslatedValue.ShouldBeNull();
	}

	[Fact]
	public void GivenModeNone_WhenAdministratorSuppliesATranslation_ThenRefused()
	{
		// Given
		var question = Question.Create("synthetic", QuestionType.Email, "Email", "Courriel", Now, isActive: true);
		var answer = new Report(Locale.EnCa, Now).Answer(question, "avery@example.test", Now);

		// When
		var supplying = () => answer.SupplyHumanTranslation("avery@example.test");

		// Then
		supplying.ShouldThrow<DomainRuleViolationException>();
		answer.DisplayedTranslation.ShouldBeNull();
		answer.ValueIn(Locale.FrCa).ShouldBe("avery@example.test");
	}

	[Fact]
	public void GivenTypeAheadValueNamingNoChoice_WhenRecorded_ThenLeftForTheWorker()
	{
		// Given
		var question = Question.Create(
			"launch", QuestionType.Autocomplete, "Launch", "Décollage", Now, isActive: true,
			options: [new QuestionOptionInput("mount_seven", "Mount Seven", "Mont Sept")]);

		// When
		var answer = new Report(Locale.EnCa, Now).Answer(question, "A ridge nobody listed", Now);

		// Then
		answer.TranslationMode.ShouldBe(TranslationMode.Machine);
		answer.NeedsTranslation.ShouldBeTrue();
		answer.TranslatedValue.ShouldBeNull();
	}

	[Fact]
	public void GivenSkippedSelect_WhenRecorded_ThenNothingIsCopiedOrQueued()
	{
		// Given
		var question = Question.Create(
			"province", QuestionType.SingleSelect, "Province", "Province", Now, isActive: true,
			options: [new QuestionOptionInput("alberta", "Alberta", "Alberta")]);

		// When
		var answer = new Report(Locale.EnCa, Now).Answer(question, (string?)null, Now);

		// Then
		answer.TranslationMode.ShouldBe(TranslationMode.Choice);
		answer.NeedsTranslation.ShouldBeFalse();
		answer.TranslatedValue.ShouldBeNull();
	}

	[Fact]
	public void GivenChoiceWithOneLanguage_WhenItsOtherLabelIsAskedFor_ThenNone()
	{
		// Given — a reporter-added choice has only the language it was typed in
		var question = Question.Create("launch", QuestionType.Autocomplete, "Launch", "Décollage", Now, isActive: true);
		question.AddChoiceFromReporter("Hidden Valley", Locale.EnCa);

		// When / Then
		question.OtherLabelOf("Hidden Valley", Locale.EnCa).ShouldBeNull();
		question.OtherLabelOf("Somewhere else", Locale.EnCa).ShouldBeNull();
	}
}
