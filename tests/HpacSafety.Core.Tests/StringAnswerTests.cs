using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// Every answer is one string — the words the reporter saw, in the language
/// they saw them. A select value is flagged for an administrator to supply the
/// other language; a boolean and a date have one invariant written form in both.
/// See ADR-0072.
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
            QuestionType.SingleSelect, "Province", "Province", isPrivate: true, isActive: true,
            displayOrder: 1, sectionKey: null, Now,
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

        // Then — the other language is an administrator's to supply, even
        // though the curated list already holds one
        answer.Locale.ShouldBe(Locale.FrCa);
        answer.NeedsTranslation.ShouldBeTrue();
        answer.TranslatedValue.ShouldBeNull();
    }

    [Fact]
    public void GivenFlaggedAnswer_WhenAdministratorSuppliesTranslation_ThenBothLanguagesAreHeld()
    {
        // Given
        var report = new Report(Locale.FrCa, Now);
        var answer = report.Answer(Province(), "Alberta", Now);

        // When
        answer.SupplyTranslation("Alberta, as written in English");

        // Then — the reporter's own value is never touched
        answer.Value.ShouldBe("Alberta");
        answer.TranslatedValue.ShouldBe("Alberta, as written in English");
        answer.NeedsTranslation.ShouldBeFalse();
        answer.ValueIn(Locale.FrCa).ShouldBe("Alberta");
        answer.ValueIn(Locale.EnCa).ShouldBe("Alberta, as written in English");
    }

    [Fact]
    public void GivenAnswerNotAwaitingTranslation_WhenOneIsSupplied_ThenRefused()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        var answer = report.Answer(Narrative(), "Wind picked up on final.", Now);

        // When
        var supplying = () => answer.SupplyTranslation("Le vent s'est levé en finale.");

        // Then — a narrative is never translated
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
        answer.NeedsTranslation.ShouldBeFalse();
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
        answer.NeedsTranslation.ShouldBeFalse();
    }

    private static Question Province() =>
        Question.Create(
            "province", QuestionType.SingleSelect, "Province", "Province", Now, isActive: true, displayOrder: 1,
            options: [new QuestionOptionInput("alberta", "Alberta", "Alberta")]);

    private static Question Injury() =>
        Question.Create(
            "injury", QuestionType.YesNo, "Were you injured?", "Avez-vous été blessé ?", Now, isActive: true);

    private static Question Narrative() =>
        Question.Create(
            "narrative", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", Now, isActive: true);
}
