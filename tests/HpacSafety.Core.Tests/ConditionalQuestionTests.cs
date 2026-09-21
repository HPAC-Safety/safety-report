using HpacSafety.Core.Features.QuestionBank;

using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// Conditional questions (ADR-0060) and the authored required flag (ADR-0061).
/// </summary>
public class ConditionalQuestionTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static Question Ordinary(string key, QuestionType type, TinyId? dependsOn = null) =>
        Question.Create(
            key, type, $"Question {key}", $"Question {key} (fr)", At, isActive: true, dependsOnQuestionId: dependsOn);

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
        Should.Throw<DomainRuleViolationException>(() => question.DependOn(question.Id, At.AddHours(1)));
    }

    [Fact]
    public void GivenPublicationConsent_WhenMadeConditional_ThenRejected()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);
        var other = Ordinary("were_you_injured", QuestionType.YesNo);

        // When / Then — a form that can skip asking consent cannot publish anything
        Should.Throw<DomainRuleViolationException>(() => consent.DependOn(other.Id, At.AddHours(1)));
    }

    [Fact]
    public void GivenConditionalQuestion_WhenDependencyIsCleared_ThenNewRevisionRecords()
    {
        // Given
        var parent = Ordinary("were_you_injured", QuestionType.YesNo);
        var child = Ordinary("injury_detail", QuestionType.LongText, parent.Id);

        // When
        child.DependOn(null, At.AddHours(1));

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
            QuestionDependencies.EnsureDependencyAllowed([parent], childId: null, parent.Id));

        cause.Message.ShouldContain("yes/no");
    }

    [Fact]
    public void GivenParentDoesNotExist_WhenBankChecksDependency_ThenRejected()
    {
        // Given
        var parent = Ordinary("were_you_injured", QuestionType.YesNo);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() =>
            QuestionDependencies.EnsureDependencyAllowed([], childId: null, parent.Id));
    }

    [Fact]
    public void GivenDeletedParent_WhenBankChecksDependency_ThenRejected()
    {
        // Given
        var parent = Ordinary("were_you_injured", QuestionType.YesNo);
        parent.Delete(At.AddHours(1));

        // When / Then
        Should.Throw<DomainRuleViolationException>(() =>
            QuestionDependencies.EnsureDependencyAllowed([parent], childId: null, parent.Id));
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
        first.DependOn(second.Id, At.AddHours(1));
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
            isPrivate: true, isActive: true, displayOrder: 0, At.AddHours(1), isRequired: false);

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
        QuestionType type, bool acceptsOptionSet)
    {
        // Given
        var question = Ordinary("authored", type);

        // When / Then
        question.CurrentRevision.AcceptsOptionSet.ShouldBe(acceptsOptionSet);
    }

    [Fact]
    public void GivenAutocompleteQuestion_WhenCreatedWithOptions_ThenStoresAndAcceptsThem()
    {
        // Given / When
        var question = Question.Create(
            "launch_site", QuestionType.Autocomplete, "Where from?", "D'où ?", At, isActive: true,
            options: [new QuestionOptionInput("golden", "Golden", "Golden")]);

        // Then
        question.CurrentRevision.Options.Count.ShouldBe(1);
        question.CurrentRevision.Accepts("golden").ShouldBeTrue();
        question.CurrentRevision.Accepts("lumby").ShouldBeFalse();
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
}
