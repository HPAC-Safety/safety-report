using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// Publication consent is the only answer read by name. Every other question,
/// whatever role an administrator assigns it, is simply recorded — the admin
/// review DTO reads exact asked questions and answers directly.
/// </summary>
public class ProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_an_ordinary_question_When_it_is_answered_Then_the_answer_is_simply_recorded()
    {
        // Given
        var question = Question.Create("description", QuestionType.LongText, "Describe it", "Décrivez-le", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, "The wing collapsed on approach.", Now);

        // Then
        report.Answers.Count.ShouldBe(1);
        report.ConsentPublish.ShouldBeNull();
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void Given_a_consent_role_on_a_text_question_When_it_is_answered_Then_a_boolean_word_is_read(string given, bool expected)
    {
        // Given — the role can be moved to a question that is not the YesNo one
        var question = Question.Create(
            "consent", QuestionType.ShortText, "May we publish?", "Pouvons-nous publier ?", Now,
            role: QuestionRole.ConsentPublish);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, given, Now);

        // Then
        report.ConsentPublish.ShouldBe(expected);
    }

    [Fact]
    public void Given_a_free_text_question_When_it_is_answered_with_option_codes_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("description", QuestionType.LongText, "Describe it", "Décrivez-le", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(question, ["something"], Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_report_When_its_record_is_read_Then_answers_files_and_summary_are_all_there()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);

        // When
        report.Answer(question, "A broken riser", Now);
        report.AddFile("kJQP7kiw5Fk/original/clip.mp4", "video/mp4", 4096, Now);
        report.AttachSummary(Summary.Generate(report.Id, "A hang glider landed short.", "Un deltaplane a atterri court.", "model", "v1", Now));

        // Then
        report.Answers.Count.ShouldBe(1);
        report.Files.Count.ShouldBe(1);
        report.Summary.ShouldNotBeNull();
    }

    [Fact]
    public void Given_an_ordinary_question_When_it_is_deactivated_Then_it_stops_being_asked()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);
        question.Activate();

        // When
        question.Deactivate();

        // Then — every answer already given to it survives
        question.IsActive.ShouldBeFalse();
        question.Deleted.ShouldBeNull();
    }

    [Fact]
    public void Given_a_revision_When_its_contents_are_read_Then_options_are_exposed_in_both_languages()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now);
        var option = question.CurrentRevision.AddOption("morning", "Morning", "Matin");

        // When
        var revision = question.CurrentRevision;

        // Then
        revision.Options.Count.ShouldBe(1);
        option.LabelEn.ShouldBe("Morning");
        option.LabelFr.ShouldBe("Matin");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_no_code_at_all_When_it_is_parsed_Then_nothing_is_guessed(string? code)
    {
        // Given / When
        var parsed = EnumCode.TryParse<ReportStatus>(code, out var status);

        // Then
        parsed.ShouldBeFalse();
        status.ShouldBe(ReportStatus.Submitted);
    }

    [Fact]
    public void Given_consent_was_never_answered_When_publication_is_attempted_Then_the_refusal_says_so()
    {
        // Given — the strongest form of the gate: nobody said no, and nobody said yes
        var report = new Report(Locale.EnCa, Now);
        report.Approve();

        // When
        var publishing = () => report.MarkPublished(Now);

        // Then
        publishing.ShouldThrow<DomainRuleViolationException>()
            .Message.ShouldContain("unanswered consent is not a consent");
    }

    [Fact]
    public void Given_a_consent_role_on_a_text_question_When_the_answer_is_no_Then_consent_is_refused()
    {
        // Given
        var question = Question.Create(
            "consent", QuestionType.ShortText, "May we publish?", "Pouvons-nous publier ?", Now,
            role: QuestionRole.ConsentPublish);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, "no", Now);

        // Then
        report.ConsentPublish.ShouldBe(false);
        report.HasAnsweredConsent.ShouldBeTrue();
    }

    [Fact]
    public void Given_consent_and_approval_but_no_summary_When_publication_is_attempted_Then_it_is_blocked()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);
        var report = new Report(Locale.EnCa, Now);
        report.Answer(consent, ["yes"], Now);
        report.Approve();

        // When
        var publishing = () => report.MarkPublished(Now);

        // Then — there is nothing anonymized to publish yet
        report.IsPublishable.ShouldBeFalse();
        publishing.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_question_created_in_a_section_When_it_is_read_Then_the_section_key_is_normalized()
    {
        // Given / When
        var question = Question.Create(
            "manufacturer", QuestionType.ShortText, "Manufacturer", "Fabricant", Now,
            sectionKey: "Aircraft Details");

        // Then
        question.SectionKey.ShouldBe("aircraft_details");
    }

    [Fact]
    public void Given_an_ordinary_question_When_its_type_changes_Then_it_is_allowed()
    {
        // Given — only the consent question has a locked type
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);

        // When
        var revised = question.Revise(QuestionType.LongText, isRequired: true, "Describe the damage", "Décrivez les dommages", Now);

        // Then
        revised.Type.ShouldBe(QuestionType.LongText);
        revised.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Given_the_consent_question_When_it_is_reworded_at_the_same_type_Then_it_is_allowed()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

        // When
        var revised = consent.Revise(
            QuestionType.YesNo, isRequired: true,
            "Do you agree to HPAC publishing a de-identified version?",
            "Acceptez-vous que l'ACVL publie une version anonymisée ?", Now.AddDays(1));

        // Then
        revised.RevisionNumber.ShouldBe(2);
        consent.Type.ShouldBe(QuestionType.YesNo);
    }

    [Fact]
    public void Given_a_consent_role_on_a_text_question_When_the_answer_is_neither_yes_nor_no_Then_it_is_refused()
    {
        // Given
        var question = Question.Create(
            "consent", QuestionType.ShortText, "May we publish?", "Pouvons-nous publier ?", Now,
            role: QuestionRole.ConsentPublish);
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(question, "maybe later", Now);

        // Then — an unreadable consent is an error, never a quiet no
        answering.ShouldThrow<DomainRuleViolationException>()
            .Message.ShouldContain("no default and no third state");
        report.ConsentPublish.ShouldBeNull();
    }

    [Fact]
    public void Given_consent_and_an_approved_summary_but_no_officer_approval_When_publishability_is_checked_Then_it_is_false()
    {
        // Given — the human gate is separate from the consent gate
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);
        var report = new Report(Locale.EnCa, Now);
        report.Answer(consent, ["yes"], Now);

        var summary = Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
        summary.Approve(TinyId.New(), Now);
        report.AttachSummary(summary);

        // When
        var publishable = report.IsPublishable;

        // Then
        report.Status.ShouldBe(ReportStatus.Submitted);
        publishable.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_published_report_When_publishability_is_rechecked_Then_it_is_still_publishable()
    {
        // Given
        var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);
        var report = new Report(Locale.EnCa, Now);
        report.Answer(consent, ["yes"], Now);

        var summary = Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
        summary.Approve(TinyId.New(), Now);
        report.AttachSummary(summary);
        report.Approve();
        report.MarkPublished(Now);

        // When
        var publishable = report.IsPublishable;

        // Then — publishing does not invalidate the state that allowed it
        publishable.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_question_in_a_section_When_it_is_moved_out_of_it_Then_it_has_no_section()
    {
        // Given
        var question = Question.Create(
            "manufacturer", QuestionType.ShortText, "Manufacturer", "Fabricant", Now, sectionKey: "aircraft");

        // When
        question.MoveToSection(null);

        // Then
        question.SectionKey.ShouldBeNull();
    }

    [Fact]
    public void Given_a_domain_rule_violation_When_it_carries_a_cause_Then_the_cause_is_kept()
    {
        // Given
        var cause = new InvalidOperationException("the underlying problem");

        // When
        var exception = new DomainRuleViolationException("the rule that was broken", cause);

        // Then
        exception.Message.ShouldBe("the rule that was broken");
        exception.InnerException.ShouldBe(cause);
        new DomainRuleViolationException().Message.ShouldNotBeNullOrEmpty();
    }
}
