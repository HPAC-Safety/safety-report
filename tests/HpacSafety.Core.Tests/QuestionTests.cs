using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

using Shouldly;

namespace HpacSafety.Core.Tests;

public sealed class QuestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_an_ordinary_question_When_it_is_created_Then_the_complete_revision_is_immutable_and_optional()
    {
        // Given / When
        var question = Question.Create(
            "pilot_name",
            QuestionType.ShortText,
            "Pilot name",
            "Nom du pilote",
            Now,
            sortOrder: 4,
            isActive: true);

        // Then
        question.Revision.ShouldBe(1);
        question.LabelEn.ShouldBe("Pilot name");
        question.LabelFr.ShouldBe("Nom du pilote");
        question.SortOrder.ShouldBe(4);
        question.IsPrivate.ShouldBeTrue();
        question.IsActive.ShouldBeTrue();
        question.IsRequired.ShouldBeFalse();
        question.SupersedesQuestionId.ShouldBeNull();
    }

    [Fact]
    public void Given_a_question_When_any_configuration_changes_Then_a_new_complete_revision_is_created()
    {
        // Given
        var original = Question.Create(
            "description",
            QuestionType.LongText,
            "Description",
            "Description",
            Now,
            isPrivate: false,
            sortOrder: 10,
            isActive: true);

        // When
        var revised = original.Revise(
            QuestionType.LongText,
            "Occurrence description",
            "Description de l’événement",
            Now.AddMinutes(1),
            isPrivate: true,
            sortOrder: 3,
            isActive: false);

        // Then
        revised.Id.ShouldNotBe(original.Id);
        revised.Key.ShouldBe(original.Key);
        revised.Revision.ShouldBe(2);
        revised.SupersedesQuestionId.ShouldBe(original.Id);
        revised.IsPrivate.ShouldBeTrue();
        revised.SortOrder.ShouldBe(3);
        revised.IsActive.ShouldBeFalse();

        original.LabelEn.ShouldBe("Description");
        original.IsPrivate.ShouldBeFalse();
        original.SortOrder.ShouldBe(10);
        original.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_select_question_When_it_is_created_Then_each_option_has_both_languages_in_the_same_revision()
    {
        // Given / When
        var question = Question.Create(
            "time_of_day",
            QuestionType.SingleSelect,
            "Time of day",
            "Moment de la journée",
            Now,
            options:
            [
                new("morning", "Morning", "Matin"),
                new("evening", "Evening", "Soirée"),
            ]);

        // Then
        question.Options.Select(option => option.Code).ShouldBe(["morning", "evening"]);
        question.Options.Select(option => option.LabelFr).ShouldBe(["Matin", "Soirée"]);
        question.Accepts("morning").ShouldBeTrue();
        question.Accepts("midnight").ShouldBeFalse();
    }

    [Fact]
    public void Given_a_question_missing_a_language_When_it_is_created_Then_it_is_rejected()
    {
        // Given / When
        var creating = () => Question.Create(
            "damage",
            QuestionType.ShortText,
            "Damage",
            " ",
            Now);

        // Then
        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_the_reserved_consent_key_When_an_ordinary_question_is_created_Then_it_is_rejected()
    {
        var creating = () => Question.Create(
            QuestionKey.ConsentPublish,
            QuestionType.YesNo,
            "May we publish?",
            "Pouvons-nous publier?",
            Now);

        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData(QuestionType.ShortText, true, true)]
    [InlineData(QuestionType.YesNo, false, true)]
    [InlineData(QuestionType.YesNo, true, false)]
    public void Given_the_consent_question_When_any_system_invariant_is_removed_Then_the_revision_is_rejected(
        QuestionType type,
        bool isPrivate,
        bool isActive)
    {
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier?", Now);

        var revising = () => consent.Revise(
            type,
            "May we publish?",
            "Pouvons-nous publier?",
            Now.AddMinutes(1),
            isPrivate,
            sortOrder: 1,
            isActive);

        revising.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_the_consent_question_When_it_is_revised_Then_it_remains_required()
    {
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier?", Now);

        var revised = consent.Revise(
            QuestionType.YesNo,
            "May HPAC publish?",
            "L’ACVL peut-elle publier?",
            Now.AddMinutes(1),
            isPrivate: true,
            sortOrder: 2,
            isActive: true);

        revised.IsRequired.ShouldBeTrue();
        revised.IsSystem.ShouldBeTrue();
        revised.Accepts("YES").ShouldBeTrue();
        revised.Accepts("maybe").ShouldBeFalse();
    }

    [Fact]
    public void Given_a_yes_no_question_When_custom_options_are_supplied_Then_it_is_rejected()
    {
        var creating = () => Question.Create(
            "confirmed",
            QuestionType.YesNo,
            "Confirmed?",
            "Confirmé?",
            Now,
            options: [new("yes", "Yes", "Oui")]);

        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_text_question_When_options_are_supplied_Then_it_is_rejected()
    {
        var creating = () => Question.Create(
            "description",
            QuestionType.LongText,
            "Description",
            "Description",
            Now,
            options: [new("other", "Other", "Autre")]);

        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData(QuestionType.SingleSelect)]
    [InlineData(QuestionType.MultiSelect)]
    public void Given_a_select_question_When_no_options_are_supplied_Then_it_is_rejected(QuestionType type)
    {
        var creating = () => Question.Create(
            "conditions",
            type,
            "Conditions",
            "Conditions",
            Now);

        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_select_question_When_option_codes_repeat_Then_it_is_rejected()
    {
        var creating = () => Question.Create(
            "conditions",
            QuestionType.MultiSelect,
            "Conditions",
            "Conditions",
            Now,
            options:
            [
                new("wind", "Wind", "Vent"),
                new("WIND", "Strong wind", "Vent fort"),
            ]);

        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData("", "Vent")]
    [InlineData("Wind", " ")]
    public void Given_an_option_missing_a_translation_When_the_question_is_created_Then_it_is_rejected(
        string labelEn,
        string labelFr)
    {
        var creating = () => Question.Create(
            "conditions",
            QuestionType.SingleSelect,
            "Conditions",
            "Conditions",
            Now,
            options: [new("wind", labelEn, labelFr)]);

        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_optional_help_and_section_text_When_created_Then_values_are_normalized()
    {
        var question = Question.Create(
            " description ",
            QuestionType.LongText,
            " Description ",
            " Description ",
            Now,
            helpEn: " ",
            helpFr: " Décrivez l’événement. ",
            sectionKey: " Flight Details ");

        question.Key.ShouldBe("description");
        question.LabelEn.ShouldBe("Description");
        question.HelpEn.ShouldBeNull();
        question.HelpFr.ShouldBe("Décrivez l’événement.");
        question.SectionKey.ShouldBe("flight_details");
    }

    [Fact]
    public void Given_the_consent_question_When_it_is_created_Then_it_is_the_only_required_system_question()
    {
        // Given / When
        var consent = Question.CreateConsentPublish(
            "May HPAC publish an anonymized summary?",
            "L’ACVL peut-elle publier un résumé anonymisé?",
            Now,
            sortOrder: 20);

        // Then
        consent.Key.ShouldBe(QuestionKey.ConsentPublish);
        consent.Type.ShouldBe(QuestionType.YesNo);
        consent.IsSystem.ShouldBeTrue();
        consent.IsRequired.ShouldBeTrue();
        consent.IsPrivate.ShouldBeTrue();
        consent.IsActive.ShouldBeTrue();
        Question.YesNoCodes.ShouldBe(["yes", "no"]);
    }

}
