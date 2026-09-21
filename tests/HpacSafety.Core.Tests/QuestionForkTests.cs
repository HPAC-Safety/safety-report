using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// A question whose wording changed after somebody answered it is a different
/// question. Editing an unanswered one revises it; editing an answered one
/// retires it and creates its replacement, carrying the same stable key, so an
/// old answer always correlates to the wording it was given under. See
/// ADR-0071.
/// </summary>
public class QuestionForkTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenUnansweredQuestion_WhenEdited_ThenRevisedInPlace()
    {
        // Given
        var question = Injury();

        // When
        var live = question.ApplyEdit(
            hasBeenAnswered: false, QuestionType.YesNo, "Did you need medical attention?",
            "Avez-vous eu besoin de soins médicaux ?", isPrivate: true, isActive: true,
            displayOrder: 3, Now);

        // Then
        live.ShouldBeSameAs(question);
        question.Deleted.ShouldBeNull();
        question.Revisions.Count.ShouldBe(2);
        question.CurrentRevision.LabelEn.ShouldBe("Did you need medical attention?");
    }

    [Fact]
    public void GivenAnsweredQuestion_WhenEdited_ThenRetiredAndReplaced()
    {
        // Given
        var question = Injury();
        var originalWording = question.CurrentRevision.LabelEn;

        // When
        var live = question.ApplyEdit(
            hasBeenAnswered: true, QuestionType.YesNo, "Did you need medical attention?",
            "Avez-vous eu besoin de soins médicaux ?", isPrivate: true, isActive: true,
            displayOrder: 3, Now);

        // Then
        live.ShouldNotBeSameAs(question);
        live.Id.ShouldNotBe(question.Id);
        live.CurrentRevision.LabelEn.ShouldBe("Did you need medical attention?");

        // The retired question is frozen, and still says what it always said.
        question.Deleted.ShouldBe(Now);
        question.Revisions.Count.ShouldBe(1);
        question.CurrentRevision.LabelEn.ShouldBe(originalWording);
    }

    [Fact]
    public void GivenAnsweredQuestion_WhenEdited_ThenReplacementCarriesTheKey()
    {
        // Given
        var question = Injury();

        // When
        var live = Reword(question, hasBeenAnswered: true);

        // Then — exports resolve by key, so the key survives the fork and only
        // the live member of the chain answers to it.
        live.Key.ShouldBe(question.Key);
        live.Deleted.ShouldBeNull();
        question.Deleted.ShouldNotBeNull();
    }

    [Fact]
    public void GivenReplacementQuestion_WhenRead_ThenRevisionNumberingStartsAgain()
    {
        // Given — a question already on its third revision
        var question = Injury();
        Reword(question, hasBeenAnswered: false);
        Reword(question, hasBeenAnswered: false);
        question.CurrentRevision.RevisionNumber.ShouldBe(3);

        // When
        var live = Reword(question, hasBeenAnswered: true);

        // Then — the chain is no longer the history; the key is
        live.CurrentRevision.RevisionNumber.ShouldBe(1);
    }

    [Fact]
    public void GivenRetiredQuestion_WhenEditedAgain_ThenRefused()
    {
        // Given
        var question = Injury();
        Reword(question, hasBeenAnswered: true);

        // When
        var editing = () => Reword(question, hasBeenAnswered: true);

        // Then — there is no undelete, and no revising a frozen row
        editing.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void GivenAnsweredConsentQuestion_WhenEdited_ThenRevisedInPlace()
    {
        // Given — consent gates every publication path and can never be deleted
        var consent = Question.CreateConsentPublish(
            "May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now);

        // When
        var live = consent.ApplyEdit(
            hasBeenAnswered: true, QuestionType.YesNo, "May we publish an anonymized version?",
            "Pouvons-nous publier une version rendue anonyme ?", isPrivate: true, isActive: true,
            displayOrder: 0, Now);

        // Then
        live.ShouldBeSameAs(consent);
        consent.Deleted.ShouldBeNull();
        consent.CurrentRevision.LabelEn.ShouldBe("May we publish an anonymized version?");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void GivenOrdinaryQuestion_WhenAskedWhetherAnEditForks_ThenAnswersByAnswers(
        bool hasBeenAnswered, bool expected)
    {
        // Given / When / Then
        Injury().ForksWhenEdited(hasBeenAnswered).ShouldBe(expected);
    }

    [Fact]
    public void GivenConsentQuestion_WhenAskedWhetherAnEditForks_ThenNeverDoes()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

        // When / Then
        consent.ForksWhenEdited(hasBeenAnswered: true).ShouldBeFalse();
    }

    private static Question Injury() =>
        Question.Create(
            "injury", QuestionType.YesNo, "Were you injured?", "Avez-vous été blessé ?", Now,
            isActive: true, displayOrder: 3);

    private static Question Reword(Question question, bool hasBeenAnswered) =>
        question.ApplyEdit(
            hasBeenAnswered, question.Type, $"Were you injured? ({Guid.NewGuid():N})",
            "Avez-vous été blessé ?", isPrivate: true, isActive: true, displayOrder: 3,
            Now);
}
