using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Every answer is one string — the words the reporter saw, in the language
///     they saw them, and immutable once written. Every answer with a value is
///     eventually translated into the other official language, mechanically by the
///     Worker or by an administrator; nothing on the submission path translates
///     anything. See ADR-0072 and ADR-0080.
/// </summary>
public class StringAnswerTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenPickerAnswer_WhenRecorded_ThenStoresTheLabelNotACode()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);

		// When
		var answer = report.Answer(Province(), "Alberta", Now);

		// Then
		answer.Value.ShouldBe("Alberta");
		answer.Locale.ShouldBe(Locale.EnCa);
	}

	[Fact]
	public void GivenPickerAnswer_WhenOptionIsRelabelledAfterwards_ThenAnswerIsUnchanged()
	{
		// Given
		var question = Province();
		var report = new Report(Locale.EnCa, Now);
		var answer = report.Answer(question, "Alberta", Now);

		// When — a later revision renames the choice entirely
		question.Revise(
			QuestionType.SingleSelect, "Province", "Province", true, true,
			1, Now,
			options: [new QuestionOptionInput("alberta", "Province of Alberta", "Province de l'Alberta")]);

		// Then — the answer carries its own words and resolves through nothing
		answer.Value.ShouldBe("Alberta");
	}

	[Fact]
	public void GivenFrenchReporter_WhenPickingCuratedChoice_ThenStoredInFrenchAndFlagged()
	{
		// Given
		var report = new Report(Locale.FrCa, Now);

		// When
		var answer = report.Answer(Province(), "Alberta", Now);

		// Then — the other language is not yet supplied, even though the
		// curated list already holds one
		answer.Locale.ShouldBe(Locale.FrCa);
		answer.NeedsTranslation.ShouldBeTrue();
		answer.TranslatedValue.ShouldBeNull();
		answer.TranslationSource.ShouldBeNull();
	}

	[Fact]
	public void GivenFlaggedAnswer_WhenWorkerSuppliesTranslation_ThenBothLanguagesAreHeldAndSourceIsAuto()
	{
		// Given
		var report = new Report(Locale.FrCa, Now);
		var answer = report.Answer(Province(), "Alberta", Now);

		// When
		answer.SupplyAutoTranslation("Alberta, as written in English");

		// Then — the reporter's own value is never touched
		answer.Value.ShouldBe("Alberta");
		answer.TranslatedValue.ShouldBe("Alberta, as written in English");
		answer.TranslationSource.ShouldBe(TranslationSource.Auto);
		answer.NeedsTranslation.ShouldBeFalse();
		answer.ValueIn(Locale.FrCa).ShouldBe("Alberta");
		answer.ValueIn(Locale.EnCa).ShouldBe("Alberta, as written in English");
	}

	[Fact]
	public void GivenFlaggedAnswer_WhenAdministratorSuppliesTranslation_ThenSourceIsHuman()
	{
		// Given
		var report = new Report(Locale.FrCa, Now);
		var answer = report.Answer(Province(), "Alberta", Now);

		// When
		answer.SupplyHumanTranslation("Alberta, as written in English");

		// Then
		answer.TranslatedValue.ShouldBe("Alberta, as written in English");
		answer.TranslationSource.ShouldBe(TranslationSource.Human);
	}

	[Fact]
	public void GivenAutoTranslatedAnswer_WhenAdministratorCorrectsIt_ThenSourceBecomesHuman()
	{
		// Given — the Worker already produced a draft
		var report = new Report(Locale.FrCa, Now);
		var answer = report.Answer(Province(), "Alberta", Now);
		answer.SupplyAutoTranslation("Alberta");

		// When — an administrator overwrites it
		answer.SupplyHumanTranslation("Alberta (corrected)");

		// Then — unlike the automatic path, a human correction may overwrite
		// an existing translation
		answer.TranslatedValue.ShouldBe("Alberta (corrected)");
		answer.TranslationSource.ShouldBe(TranslationSource.Human);
	}

	[Fact]
	public void GivenAlreadyAutoTranslatedAnswer_WhenWorkerSuppliesAnotherOne_ThenRefused()
	{
		// Given — idempotency: the Worker must not silently overwrite a
		// translation, its own or an administrator's
		var report = new Report(Locale.FrCa, Now);
		var answer = report.Answer(Province(), "Alberta", Now);
		answer.SupplyAutoTranslation("Alberta");

		// When
		var supplyingAgain = () => answer.SupplyAutoTranslation("Alberta (again)");

		// Then
		supplyingAgain.ShouldThrow<DomainRuleViolationException>();
		answer.TranslatedValue.ShouldBe("Alberta");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenFlaggedAnswer_WhenBlankTranslationIsSupplied_ThenRefused(string blank)
	{
		// Given — clearing the flag with nothing in the box would leave the
		// answer looking translated when half of it is missing
		var report = new Report(Locale.FrCa, Now);
		var answer = report.Answer(Province(), "Alberta", Now);

		// When
		var supplying = () => answer.SupplyHumanTranslation(blank);

		// Then
		supplying.ShouldThrow<DomainRuleViolationException>();
		answer.NeedsTranslation.ShouldBeTrue();
	}

	[Fact]
	public void GivenNarrativeAnswer_WhenRecorded_ThenAlsoFlaggedForTranslation()
	{
		// Given — ADR-0080 widens translation to every answer with a value,
		// narrative included; nothing on the submission path translates it
		var report = new Report(Locale.EnCa, Now);

		// When
		var answer = report.Answer(Narrative(), "Wind picked up on final.", Now);

		// Then
		answer.NeedsTranslation.ShouldBeTrue();
		answer.Value.ShouldBe("Wind picked up on final.");
	}

	[Fact]
	public void GivenNarrativeAnswer_WhenWorkerTranslatesIt_ThenTheOriginalIsNeverTouched()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		var answer = report.Answer(Narrative(), "Wind picked up on final.", Now);

		// When — the Worker's mechanical translation, never an LLM rewrite
		answer.SupplyAutoTranslation("Le vent s'est levé en finale.");

		// Then — the answer of record is untouched; only the second language
		// gained a value
		answer.Value.ShouldBe("Wind picked up on final.");
		answer.TranslatedValue.ShouldBe("Le vent s'est levé en finale.");
		answer.TranslationSource.ShouldBe(TranslationSource.Auto);
	}

	[Fact]
	public void GivenSkippedAnswer_WhenTranslationIsAttempted_ThenRefused()
	{
		// Given — nothing was said, so there is nothing to translate
		var report = new Report(Locale.EnCa, Now);
		var answer = report.Answer(Province(), value: null, Now);

		// When
		var supplying = () => answer.SupplyAutoTranslation("anything");

		// Then
		supplying.ShouldThrow<DomainRuleViolationException>();
	}

	[Theory]
	[InlineData("yes")]
	[InlineData("no")]
	public void GivenBooleanAnswer_WhenRecorded_ThenStoredFormIsInvariant(string given)
	{
		// Given — a French reporter's yes is still "yes" in the database
		var report = new Report(Locale.FrCa, Now);

		// When
		var answer = report.Answer(Injury(), given, Now);

		// Then
		answer.Value.ShouldBe(given);
		answer.ValueIn(Locale.EnCa).ShouldBe(given);
	}

	[Theory]
	[InlineData("oui")]
	[InlineData("non")]
	[InlineData("True")]
	public void GivenLocalizedOrTypedBoolean_WhenRecorded_ThenRefused(string given)
	{
		// Given
		var report = new Report(Locale.FrCa, Now);

		// When
		var answering = () => report.Answer(Injury(), given, Now);

		// Then — a stored "oui" is a bug, not an alternative spelling
		answering.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenValueTheRevisionNeverOffered_WhenRecorded_ThenRefused()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);

		// When
		var answering = () => report.Answer(Province(), "Saskatchewan", Now);

		// Then — the snapshot is still what a submitted value is checked against
		answering.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenSkippedQuestion_WhenRecorded_ThenValueIsNullAndNothingIsFlagged()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);

		// When
		var answer = report.Answer(Province(), value: null, Now);

		// Then — a skip is recorded, and nothing is synthesized in its place
		answer.Value.ShouldBeNull();
		answer.NeedsTranslation.ShouldBeFalse();
	}

	[Fact]
	public void GivenAnswerWithNoTranslationYet_WhenReadInEitherLanguage_ThenGivesWhatTheReporterWrote()
	{
		// Given — nobody has reached this one yet
		var report = new Report(Locale.FrCa, Now);
		var answer = report.Answer(Province(), "Alberta", Now);

		// When / Then — a half-filled answer reads as the reporter's words
		// rather than as a blank
		answer.ValueIn(Locale.FrCa).ShouldBe("Alberta");
		answer.ValueIn(Locale.EnCa).ShouldBe("Alberta");
	}


	[Fact]
	public void GivenRequiredQuestion_WhenLeftBlank_ThenRefused()
	{
		// Given
		var question = Question.Create(
			"narrative", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", Now,
			isRequired: true, isActive: true);
		var report = new Report(Locale.EnCa, Now);

		// When
		var answering = () => report.Answer(question, value: null, Now);

		// Then
		answering.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenMultiSelect_WhenNothingIsChosen_ThenOneSkippedAnswerIsRecorded()
	{
		// Given
		var question = Question.Create(
			"ratings", QuestionType.MultiSelect, "Ratings", "Qualifications", Now, isActive: true,
			options: [new QuestionOptionInput("p3", "P3", "P3")]);
		var report = new Report(Locale.EnCa, Now);

		// When
		var answers = report.Answer(question, [], Now);

		// Then — a skip is recorded, not omitted
		answers.Count.ShouldBe(1);
		answers[0].Value.ShouldBeNull();
	}

	[Theory]
	[InlineData(QuestionType.Date, "2026-09-21")]
	[InlineData(QuestionType.Time, "14:30")]
	public void GivenDateOrTimeAnswer_WhenRecorded_ThenStoredAsIso8601(QuestionType type, string given)
	{
		// Given
		var question = Question.Create(
			"occurred", type, "When did it happen?", "Quand est-ce arrivé ?", Now, isActive: true);
		var report = new Report(Locale.FrCa, Now);

		// When
		var answer = report.Answer(question, given, Now);

		// Then — machine-readable storage, localized only at render
		answer.Value.ShouldBe(given);
	}

	private static Question Province()
	{
		return Question.Create(
			"province", QuestionType.SingleSelect, "Province", "Province", Now, isActive: true, displayOrder: 1,
			options: [new QuestionOptionInput("alberta", "Alberta", "Alberta")]);
	}

	private static Question Injury()
	{
		return Question.Create(
			"injury", QuestionType.YesNo, "Were you injured?", "Avez-vous été blessé ?", Now, isActive: true);
	}

	private static Question Narrative()
	{
		return Question.Create(
			"narrative", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", Now, isActive: true);
	}
}
