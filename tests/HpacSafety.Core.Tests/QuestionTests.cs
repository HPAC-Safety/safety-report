using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The question bank is data: an administrator adds, rewords, reorders, and
/// removes questions without a deploy. Publication consent is the one
/// exception. Every revision is born complete in both official languages.
/// </summary>
public class QuestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_the_consent_question_When_deletion_is_attempted_Then_it_is_refused()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now);

        // When
        var deleting = () => consent.Delete(Now);

        // Then
        deleting.ShouldThrow<DomainRuleViolationException>()
            .Message.ShouldContain("cannot be deleted");
    }

    [Fact]
    public void Given_the_consent_question_When_deactivation_is_attempted_Then_it_is_refused()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now);

        // When
        var deactivating = () => consent.Deactivate(Now);

        // Then
        deactivating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_ordinary_question_When_it_is_deleted_Then_it_is_retired_rather_than_refused()
    {
        // Given — everything except consent is ordinary data
        var question = Question.Create("pilot_injury", QuestionType.SingleSelect, "Pilot injury", "Blessure du pilote", Now);

        // When
        question.Delete(Now);

        // Then
        question.Deleted.ShouldBe(Now);
        question.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_question_missing_French_wording_When_it_is_created_Then_it_is_refused()
    {
        // Given / When — a revision is born complete in both official languages
        var creating = () => Question.Create("where", QuestionType.ShortText, "Where did it happen?", "   ", Now);

        // Then
        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_complete_bilingual_question_When_it_is_activated_Then_it_is_allowed()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, "Where did it happen?", "Où cela s'est-il produit?", Now);

        // When
        question.Activate(Now);

        // Then
        question.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_question_is_reworded_When_a_new_revision_is_created_Then_the_previous_wording_survives()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);
        var original = question.CurrentRevision;

        // When
        question.Revise(
            QuestionType.LongText, "Describe the damage", "Décrivez les dommages",
            question.IsPrivate, question.IsActive, question.DisplayOrder, question.SectionKey, Now.AddDays(1));

        // Then
        question.Revisions.Count.ShouldBe(2);
        question.CurrentRevision.RevisionNumber.ShouldBe(2);
        original.LabelEn.ShouldBe("Damage");
        original.Type.ShouldBe(QuestionType.ShortText);
    }

    [Fact]
    public void Given_the_consent_question_When_a_type_change_is_attempted_Then_it_is_refused()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

        // When
        var retyping = () => consent.Revise(
            QuestionType.LongText, "May we publish?", "Pouvons-nous publier ?",
            consent.IsPrivate, consent.IsActive, consent.DisplayOrder, consent.SectionKey, Now);

        // Then — its wording can change; its type cannot
        retyping.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_question_is_reordered_When_the_move_is_saved_Then_a_new_revision_is_created()
    {
        // Given — order is a revision field: moving a question is a new,
        // complete revision, not an in-place edit of the old one.
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now, displayOrder: 3);

        // When
        question.Reorder(1, Now.AddDays(1));

        // Then
        question.DisplayOrder.ShouldBe(1);
        question.Revisions.Count.ShouldBe(2);
    }

    [Fact]
    public void Given_an_option_When_it_is_added_Then_its_code_is_invariant_and_its_wording_is_bilingual()
    {
        // Given / When — a revision is born with its complete option set
        var question = Question.Create(
            "time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now,
            options: [new QuestionOptionInput("mid_day", "Mid-day", "Milieu de journée")]);

        // Then — the code is what every historical answer points at
        var option = question.CurrentRevision.Option("mid_day")!;
        option.Code.ShouldBe("mid_day");
        option.LabelEn.ShouldBe("Mid-day");
        option.LabelFr.ShouldBe("Milieu de journée");
    }

    [Fact]
    public void Given_a_free_text_question_When_an_option_is_supplied_Then_creation_is_refused()
    {
        // Given / When
        var creating = () => Question.Create(
            "where", QuestionType.ShortText, "Where?", "Où ?", Now,
            options: [new QuestionOptionInput("anything", "Anything", "N'importe quoi")]);

        // Then
        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_yes_no_question_When_options_are_supplied_Then_creation_is_refused()
    {
        // Given / When — yes or no, no default and no third state
        var creating = () => Question.Create(
            "agree", QuestionType.YesNo, "Do you agree?", "Êtes-vous d'accord ?", Now,
            options: [new QuestionOptionInput("maybe", "Maybe", "Peut-être")]);

        // Then
        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_yes_no_question_When_answers_are_checked_Then_only_yes_and_no_are_accepted()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

        // Then
        consent.CurrentRevision.Accepts("yes").ShouldBeTrue();
        consent.CurrentRevision.Accepts("no").ShouldBeTrue();
        consent.CurrentRevision.Accepts("maybe").ShouldBeFalse();
    }

    [Fact]
    public void Given_the_consent_question_When_it_is_created_Then_it_is_required()
    {
        // Given / When
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

        // Then — the reporter must choose before continuing
        consent.CurrentRevision.IsRequired.ShouldBeTrue();
        consent.IsSystem.ShouldBeTrue();
        consent.Role.ShouldBe(QuestionRole.ConsentPublish);
    }

    [Fact]
    public void Given_an_ordinary_question_When_created_Then_it_is_never_required()
    {
        // Given / When — invariant #1: only consent_publish may be required.
        // There is no isRequired parameter to pass at all, ordinary or
        // otherwise — IsRequired is derived from system role, never
        // caller-controlled.
        var question = Question.Create("description", QuestionType.LongText, "Describe the occurrence", "Décrivez l'événement", Now);

        // Then
        question.CurrentRevision.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_new_question_When_privacy_is_not_stated_Then_it_is_private()
    {
        // Given / When
        var question = Question.Create("anything", QuestionType.ShortText, "Anything", "N'importe quoi", Now);

        // Then — an administrator must deliberately opt a question into report content
        question.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_question_When_its_privacy_contract_is_inspected_Then_it_has_no_public_setter()
    {
        // Given / When — privacy lives on the revision and is set once, at
        // that revision's construction (an init-only accessor). Changing it,
        // like every other revision field, means creating a new revision —
        // there is no public in-place mutator on either type.
        var questionProperty = typeof(Question).GetProperty(nameof(Question.IsPrivate));
        var revisionProperty = typeof(QuestionRevision).GetProperty(nameof(QuestionRevision.IsPrivate));

        // Then
        questionProperty.ShouldNotBeNull();
        questionProperty.SetMethod.ShouldBeNull();
        revisionProperty.ShouldNotBeNull();
        revisionProperty.SetMethod.ShouldNotBeNull();
        revisionProperty.SetMethod!.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_question_When_its_privacy_is_changed_Then_a_new_revision_carries_the_change()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, "Where?", "Où ?", Now, isPrivate: true);
        var original = question.CurrentRevision;

        // When
        var revised = question.Revise(
            question.Type, "Where?", "Où ?", isPrivate: false, question.IsActive, question.DisplayOrder, question.SectionKey, Now.AddDays(1));

        // Then — the old revision, already possibly referenced by an answer, is unchanged
        original.IsPrivate.ShouldBeTrue();
        revised.IsPrivate.ShouldBeFalse();
        question.IsPrivate.ShouldBeFalse();
    }
}
