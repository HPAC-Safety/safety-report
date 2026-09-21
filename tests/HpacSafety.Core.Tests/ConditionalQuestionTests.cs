using HpacSafety.Core;
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
    public void Given_a_parent_question_When_a_child_names_it_Then_the_child_records_the_dependency()
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
    public void Given_a_question_When_it_names_itself_Then_it_is_rejected()
    {
        // Given
        var question = Ordinary("were_you_injured", QuestionType.YesNo);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => question.DependOn(question.Id, At.AddHours(1)));
    }

    [Fact]
    public void Given_publication_consent_When_it_is_made_conditional_Then_it_is_rejected()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);
        var other = Ordinary("were_you_injured", QuestionType.YesNo);

        // When / Then — a form that can skip asking consent cannot publish anything
        Should.Throw<DomainRuleViolationException>(() => consent.DependOn(other.Id, At.AddHours(1)));
    }

    [Theory]
    [InlineData(QuestionType.Statement)]
    [InlineData(QuestionType.Group)]
    public void Given_a_question_that_collects_no_answer_When_it_is_made_conditional_Then_it_is_rejected(QuestionType type)
    {
        // Given
        var parent = Ordinary("were_you_injured", QuestionType.YesNo);
        var question = Ordinary("section", type);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => question.DependOn(parent.Id, At.AddHours(1)));
    }

    [Fact]
    public void Given_a_conditional_question_When_the_dependency_is_cleared_Then_a_new_revision_records_that()
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
    public void Given_a_non_boolean_parent_When_the_bank_checks_the_dependency_Then_it_is_rejected()
    {
        // Given
        var parent = Ordinary("glider_make", QuestionType.ShortText);

        // When / Then
        var cause = Should.Throw<DomainRuleViolationException>(() =>
            QuestionDependencies.EnsureDependencyAllowed([parent], childId: null, parent.Id));

        cause.Message.ShouldContain("yes/no");
    }

    [Fact]
    public void Given_a_parent_that_does_not_exist_When_the_bank_checks_the_dependency_Then_it_is_rejected()
    {
        // Given
        var parent = Ordinary("were_you_injured", QuestionType.YesNo);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() =>
            QuestionDependencies.EnsureDependencyAllowed([], childId: null, parent.Id));
    }

    [Fact]
    public void Given_a_deleted_parent_When_the_bank_checks_the_dependency_Then_it_is_rejected()
    {
        // Given
        var parent = Ordinary("were_you_injured", QuestionType.YesNo);
        parent.Delete(At.AddHours(1));

        // When / Then
        Should.Throw<DomainRuleViolationException>(() =>
            QuestionDependencies.EnsureDependencyAllowed([parent], childId: null, parent.Id));
    }

    [Fact]
    public void Given_a_child_When_the_bank_is_asked_to_make_its_parent_depend_on_it_Then_the_cycle_is_rejected()
    {
        // Given
        var parent = Ordinary("were_you_injured", QuestionType.YesNo);
        var child = Ordinary("still_flying", QuestionType.YesNo, parent.Id);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() =>
            QuestionDependencies.EnsureDependencyAllowed([parent, child], parent.Id, child.Id));
    }

    [Fact]
    public void Given_a_longer_chain_When_it_would_close_into_a_cycle_Then_it_is_rejected()
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
    public void Given_a_chain_that_does_not_close_When_the_bank_checks_it_Then_it_is_allowed()
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
    public void Given_a_question_that_is_its_own_parent_When_the_bank_checks_it_Then_it_is_rejected()
    {
        // Given
        var question = Ordinary("were_you_injured", QuestionType.YesNo);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() =>
            QuestionDependencies.EnsureDependencyAllowed([question], question.Id, question.Id));
    }

    [Fact]
    public void Given_an_ordinary_question_When_it_is_authored_required_Then_the_revision_records_it()
    {
        // Given / When
        var question = Question.Create(
            "occurrence_date", QuestionType.Date, "When?", "Quand ?", At, isActive: true, isRequired: true);

        // Then
        question.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Given_publication_consent_When_it_is_revised_as_optional_Then_it_stays_required()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);

        // When
        var revision = consent.Revise(
            QuestionType.YesNo, "May we publish a summary?", "Pouvons-nous publier un résumé ?",
            isPrivate: true, isActive: true, displayOrder: 0, sectionKey: null, At.AddHours(1), isRequired: false);

        // Then — consent cannot be made skippable, whatever the caller asks for
        revision.IsRequired.ShouldBeTrue();
        revision.IsSystem.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_required_question_When_it_is_reordered_Then_the_required_state_carries_forward()
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
    public void Given_a_question_type_When_its_option_behaviour_is_read_Then_it_matches_the_contract(
        QuestionType type, bool acceptsOptionSet)
    {
        // Given
        var question = Ordinary("authored", type);

        // When / Then
        question.CurrentRevision.AcceptsOptionSet.ShouldBe(acceptsOptionSet);
    }

    [Fact]
    public void Given_an_autocomplete_question_When_it_is_created_with_options_Then_it_stores_and_accepts_them()
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
    public void Given_a_time_question_When_it_is_created_with_options_Then_it_is_rejected()
    {
        // Given / When / Then
        Should.Throw<DomainRuleViolationException>(() => Question.Create(
            "occurrence_time", QuestionType.Time, "What time?", "À quelle heure ?", At, isActive: true,
            options: [new QuestionOptionInput("noon", "Noon", "Midi")]));
    }
}
