using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The edges of editing a question bank: retired questions, roles moving
/// between questions, duplicate options, and answers that do not fit the type
/// they were given for.
/// </summary>
public class QuestionBankEdgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_deleted_question_When_it_is_edited_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);
        question.Delete(Now);

        // When
        var reordering = () => question.Reorder(2);

        // Then
        reordering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_question_deleted_twice_When_the_second_delete_runs_Then_the_first_time_stands()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);
        question.Delete(Now);

        // When
        question.Delete(Now.AddDays(1));

        // Then
        question.Deleted.ShouldBe(Now);
    }

    [Fact]
    public void Given_a_role_bearing_question_When_the_role_moves_to_another_question_Then_the_new_one_carries_it()
    {
        // Given — consent is the only role that must live somewhere, so moving
        // it away has to land it on another question, not clear it to None.
        var oldConsent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);
        var newConsent = Question.Create(
            "consent_v2", QuestionType.YesNo, "Do you agree?", "Êtes-vous d'accord ?", Now);

        // When
        newConsent.AssignRole(QuestionRole.ConsentPublish);

        // Then
        newConsent.Role.ShouldBe(QuestionRole.ConsentPublish);
        oldConsent.Role.ShouldBe(QuestionRole.ConsentPublish);
    }

    [Fact]
    public void Given_the_consent_question_When_its_role_is_reassigned_Then_it_is_refused()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

        // When
        var reassigning = () => consent.AssignRole(QuestionRole.None);

        // Then
        reassigning.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_question_When_it_is_moved_into_a_section_Then_the_section_key_is_normalized()
    {
        // Given
        var question = Question.Create("manufacturer", QuestionType.ShortText, "Manufacturer", "Fabricant", Now);

        // When
        question.MoveToSection("Aircraft details");

        // Then
        question.SectionKey.ShouldBe("aircraft_details");
    }

    [Fact]
    public void Given_a_non_private_question_When_it_is_answered_Then_the_answer_carries_that_classification()
    {
        // Given
        var question = Question.Create(
            "province", QuestionType.ShortText, "Province", "Province", Now, isPrivate: false);
        var report = new Report(Locale.EnCa, Now);

        // When
        var answer = report.Answer(question, "Alberta", Now);

        // Then
        answer.IsPrivate.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_duplicate_option_code_When_it_is_added_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now);
        question.CurrentRevision.AddOption("morning", "Morning", "Matin");

        // When
        var adding = () => question.CurrentRevision.AddOption("Morning", "Morning again", "Encore le matin");

        // Then — the code is normalized before the duplicate check
        adding.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_option_missing_French_wording_When_it_is_added_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now);

        // When
        var adding = () => question.CurrentRevision.AddOption("morning", "Morning", "   ");

        // Then
        adding.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_option_When_it_is_reordered_Then_it_moves_among_its_siblings()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now);
        question.CurrentRevision.AddOption("morning", "Morning", "Matin");
        var evening = question.CurrentRevision.AddOption("evening", "Evening", "Soirée");

        // When
        evening.Reorder(0);

        // Then
        evening.DisplayOrder.ShouldBe(0);
        question.Revisions.Count.ShouldBe(1);
    }

    [Fact]
    public void Given_a_select_question_When_it_is_answered_with_free_text_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now);
        question.CurrentRevision.AddOption("morning", "Morning", "Matin");
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(question, "in the morning", Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_statement_When_it_is_answered_Then_it_is_refused()
    {
        // Given
        var statement = Question.Create(
            "intro", QuestionType.Statement, "We will ask you 15 short questions.", "Nous vous poserons 15 courtes questions.", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(statement, "ok", Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_required_question_When_it_is_left_blank_Then_it_is_refused()
    {
        // Given
        var question = Question.Create(
            "description", QuestionType.LongText, "Describe the occurrence", "Décrivez l'événement", Now, isRequired: true);
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(question, "  ", Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_required_select_question_When_nothing_is_chosen_Then_it_is_refused()
    {
        // Given
        var question = Question.Create(
            "province", QuestionType.SingleSelect, "Province", "Province", Now, isRequired: true);
        question.CurrentRevision.AddOption("alberta", "Alberta", "Alberta");
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(question, [], Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_single_select_question_When_two_answers_are_given_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("province", QuestionType.SingleSelect, "Province", "Province", Now);
        question.CurrentRevision.AddOption("alberta", "Alberta", "Alberta");
        question.CurrentRevision.AddOption("ontario", "Ontario", "Ontario");
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(question, ["alberta", "ontario"], Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_multi_select_question_When_several_answers_are_given_Then_all_are_recorded()
    {
        // Given
        var question = Question.Create("ratings", QuestionType.MultiSelect, "Pilot's ratings", "Qualifications du pilote", Now);
        question.CurrentRevision.AddOption("p3", "P3", "P3");
        question.CurrentRevision.AddOption("paragliding_instructor", "Paragliding Instructor", "Instructeur de parapente");
        var report = new Report(Locale.EnCa, Now);

        // When
        var answer = report.Answer(question, ["p3", "paragliding_instructor"], Now);

        // Then
        answer.SelectedOptionCodes.Count.ShouldBe(2);
        answer.SingleOptionCode.ShouldBeNull();
    }

    [Fact]
    public void Given_a_key_of_only_punctuation_When_it_is_normalized_Then_it_is_refused()
    {
        // Given / When
        var normalizing = () => QuestionKey.Normalize("!!! ???");

        // Then — a key nobody chose is a key nobody can find again
        normalizing.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_blank_label_When_a_question_is_created_Then_it_is_refused()
    {
        // Given / When
        var creating = () => Question.Create("where", QuestionType.ShortText, "   ", "Où ?", Now);

        // Then
        creating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_supported_locale_code_When_it_is_parsed_Then_it_resolves()
    {
        // Given / When
        var parsed = Locale.TryParse("FR-ca", out var locale);

        // Then
        parsed.ShouldBeTrue();
        locale.ShouldBe(Locale.FrCa);
        Locale.Parse("en-CA").ToString().ShouldBe("en-CA");
    }
}
