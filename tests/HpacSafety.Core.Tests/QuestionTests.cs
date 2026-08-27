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
        var deactivating = consent.Deactivate;

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
        question.Activate();

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
        question.Revise(QuestionType.LongText, isRequired: false, "Describe the damage", "Décrivez les dommages", Now.AddDays(1));

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
        var retyping = () => consent.Revise(QuestionType.LongText, isRequired: true, "May we publish?", "Pouvons-nous publier ?", Now);

        // Then — its wording can change; its type cannot
        retyping.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_question_is_reordered_When_the_move_is_saved_Then_no_new_revision_is_created()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now, displayOrder: 3);

        // When
        question.Reorder(1);

        // Then — moving a question does not change what any answer means
        question.DisplayOrder.ShouldBe(1);
        question.Revisions.Count.ShouldBe(1);
    }

    [Fact]
    public void Given_an_option_When_it_is_added_Then_its_code_is_invariant_and_its_wording_is_bilingual()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now);

        // When
        var option = question.CurrentRevision.AddOption("mid_day", "Mid-day", "Milieu de journée");

        // Then — the code is what every historical answer points at
        option.Code.ShouldBe("mid_day");
        option.LabelEn.ShouldBe("Mid-day");
        option.LabelFr.ShouldBe("Milieu de journée");
    }

    [Fact]
    public void Given_a_free_text_question_When_an_option_is_added_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, "Where?", "Où ?", Now);

        // When
        var adding = () => question.CurrentRevision.AddOption("anything", "Anything", "N'importe quoi");

        // Then
        adding.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_yes_no_question_When_a_third_option_is_added_Then_it_is_refused()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

        // When
        var adding = () => consent.CurrentRevision.AddOption("maybe", "Maybe", "Peut-être");

        // Then — yes or no, no default and no third state
        adding.ShouldThrow<DomainRuleViolationException>();
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
    public void Given_a_new_question_When_privacy_is_not_stated_Then_it_is_private()
    {
        // Given / When
        var question = Question.Create("anything", QuestionType.ShortText, "Anything", "N'importe quoi", Now);

        // Then — an administrator must deliberately opt a question into report content
        question.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_question_When_its_privacy_contract_is_inspected_Then_it_cannot_be_changed_after_creation()
    {
        // Given / When
        var property = typeof(Question).GetProperty(nameof(Question.IsPrivate));
        var publicMethods = typeof(Question).GetMethods().Select(method => method.Name);

        // Then — changing classification requires retiring this question and creating another
        property.ShouldNotBeNull();
        property.SetMethod.ShouldNotBeNull();
        property.SetMethod!.IsPrivate.ShouldBeTrue();
        publicMethods.ShouldNotContain("Reclassify");
    }
}
