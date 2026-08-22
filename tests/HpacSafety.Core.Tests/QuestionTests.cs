using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The question bank is data: an administrator adds, rewords, reorders, and
/// removes questions without a deploy. Publication consent is the one exception.
/// </summary>
public class QuestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_the_consent_question_When_deletion_is_attempted_Then_it_is_refused()
    {
        // Given
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish a de-identified version?", Now);

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
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish a de-identified version?", Now);

        // When
        var deactivating = consent.Deactivate;

        // Then
        deactivating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_injury_question_When_it_is_deleted_Then_it_is_retired_rather_than_refused()
    {
        // Given — everything except consent is ordinary data, including injury
        var injury = Question.Create(
            "pilot_injury", QuestionType.SingleSelect, Locale.EnCa, "Pilot injury", Now,
            role: QuestionRole.PilotInjury);

        // When
        injury.Delete(Now);

        // Then
        injury.DeletedAt.ShouldBe(Now);
        injury.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_question_with_only_English_wording_When_it_is_activated_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, Locale.EnCa, "Where did it happen?", Now);

        // When
        var activating = question.Activate;

        // Then — a reporter is never shown a half-translated form
        activating.ShouldThrow<DomainRuleViolationException>()
            .Message.ShouldContain("fr-CA");
    }

    [Fact]
    public void Given_a_machine_translated_counterpart_When_the_question_is_activated_Then_it_is_allowed()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, Locale.EnCa, "Where did it happen?", Now);
        question.CurrentVersion.AttachTranslation(Locale.FrCa, "Où cela s'est-il produit?", null, null, Now);

        // When
        question.Activate();

        // Then — generated is acceptable; absent is not
        question.IsActive.ShouldBeTrue();
        question.CurrentVersion.Translation(Locale.FrCa)!.IsMachineTranslated.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_question_authored_in_French_When_its_counterpart_is_attached_Then_French_remains_the_source()
    {
        // Given — authoring locale comes from the browser, and French is as valid a source as English
        var question = Question.Create("recit", QuestionType.LongText, Locale.FrCa, "Décrivez l'événement", Now);

        // When
        question.CurrentVersion.AttachTranslation(Locale.EnCa, "Describe the occurrence", null, null, Now);

        // Then
        question.CurrentVersion.SourceTranslation.Locale.ShouldBe(Locale.FrCa);
        question.CurrentVersion.Translation(Locale.EnCa)!.IsSource.ShouldBeFalse();
        question.CurrentVersion.Translations.Count(t => t.IsSource).ShouldBe(1);
    }

    [Fact]
    public void Given_a_generated_translation_When_an_admin_edits_it_Then_it_is_no_longer_marked_machine_translated()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, Locale.EnCa, "Where did it happen?", Now);
        var generated = question.CurrentVersion.AttachTranslation(Locale.FrCa, "Ou?", null, null, Now);

        // When
        generated.ReviseByHand("Où cela s'est-il produit?", null, null, Now.AddMinutes(5));

        // Then
        generated.IsMachineTranslated.ShouldBeFalse();
        generated.Label.ShouldBe("Où cela s'est-il produit?");
    }

    [Fact]
    public void Given_a_source_translation_When_rewording_it_directly_is_attempted_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, Locale.EnCa, "Where did it happen?", Now);

        // When
        var rewording = () => question.CurrentVersion.SourceTranslation.ReviseByHand("Where?", null, null, Now);

        // Then — changing what a question asks is a revision, and revisions are versioned
        rewording.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_question_is_reworded_When_a_new_version_is_created_Then_the_previous_wording_survives()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, Locale.EnCa, "Damage", Now);
        var original = question.CurrentVersion;

        // When
        question.Revise(QuestionType.LongText, isRequired: false, Locale.EnCa, "Describe the damage", Now.AddDays(1));

        // Then
        question.Versions.Count.ShouldBe(2);
        question.CurrentVersion.VersionNumber.ShouldBe(2);
        original.SourceTranslation.Label.ShouldBe("Damage");
        original.Type.ShouldBe(QuestionType.ShortText);
    }

    [Fact]
    public void Given_the_consent_question_When_a_type_change_is_attempted_Then_it_is_refused()
    {
        // Given
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish?", Now);

        // When
        var retyping = () => consent.Revise(QuestionType.LongText, isRequired: true, Locale.EnCa, "May we publish?", Now);

        // Then — its wording can change; its type cannot
        retyping.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_question_is_reordered_When_the_move_is_saved_Then_no_new_version_is_created()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, Locale.EnCa, "Damage", Now, displayOrder: 3);

        // When
        question.Reorder(1);

        // Then — moving a question does not change what any answer means
        question.DisplayOrder.ShouldBe(1);
        question.Versions.Count.ShouldBe(1);
    }

    [Fact]
    public void Given_an_option_is_relabelled_When_the_label_changes_Then_its_code_is_unchanged()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, Locale.EnCa, "Time of day", Now);
        var option = question.CurrentVersion.AddOption("mid_day", "Mid-day", Now);
        var french = option.AttachTranslation(Locale.FrCa, "Milieu de journee", Now);

        // When
        french.ReviseByHand("Milieu de journée", Now.AddMinutes(1));

        // Then — the code is what every historical answer points at
        option.Code.ShouldBe("mid_day");
        option.Translation(Locale.FrCa)!.Label.ShouldBe("Milieu de journée");
    }

    [Fact]
    public void Given_a_free_text_question_When_an_option_is_added_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, Locale.EnCa, "Where?", Now);

        // When
        var adding = () => question.CurrentVersion.AddOption("anything", "Anything", Now);

        // Then
        adding.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_yes_no_question_When_a_third_option_is_added_Then_it_is_refused()
    {
        // Given
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish?", Now);

        // When
        var adding = () => consent.CurrentVersion.AddOption("maybe", "Maybe", Now);

        // Then — yes or no, no default and no third state
        adding.ShouldThrow<DomainRuleViolationException>();
        consent.CurrentVersion.Accepts("yes").ShouldBeTrue();
        consent.CurrentVersion.Accepts("no").ShouldBeTrue();
        consent.CurrentVersion.Accepts("maybe").ShouldBeFalse();
    }

    [Fact]
    public void Given_the_consent_question_When_it_is_created_Then_it_is_required()
    {
        // Given / When
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish?", Now);

        // Then — the reporter must choose before continuing
        consent.CurrentVersion.IsRequired.ShouldBeTrue();
        consent.IsSystem.ShouldBeTrue();
        consent.Role.ShouldBe(QuestionRole.ConsentPublish);
    }

    [Fact]
    public void Given_a_new_question_When_no_tier_is_stated_Then_it_is_Restricted()
    {
        // Given / When
        var question = Question.Create("anything", QuestionType.ShortText, Locale.EnCa, "Anything", Now);

        // Then — if you are unsure which tier something belongs to, it is Restricted
        question.Sensitivity.ShouldBe(SensitivityTier.Restricted);
    }
}
