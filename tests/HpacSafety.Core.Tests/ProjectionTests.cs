using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// What happens when an answer does not fit the property it projects onto. Every
/// one of these has to be a defined state: the question set is data, so an
/// administrator can produce any of them without touching code.
/// </summary>
public class ProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_passenger_fatality_When_it_is_recorded_Then_the_report_escalates()
    {
        // Given
        var question = Question.Create(
            "passenger_injury", QuestionType.SingleSelect, Locale.EnCa, "Passenger injury", Now,
            role: QuestionRole.PassengerInjury);
        question.CurrentVersion.AddOption("fatality", "Fatality", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, ["fatality"], Now);

        // Then
        report.PassengerInjury.ShouldBe(InjurySeverity.Fatality);
        report.InvolvesSeriousInjury.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_date_answer_that_is_not_a_date_When_it_is_recorded_Then_the_report_has_no_date()
    {
        // Given — an administrator may point the date role at a free-text question
        var question = Question.Create(
            "occurrence_date", QuestionType.ShortText, Locale.EnCa, "When did it happen?", Now,
            role: QuestionRole.OccurrenceDate);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, "last Saturday", Now);

        // Then — the answer is kept, and nothing invents a date from it
        report.OccurredOn.ShouldBeNull();
        report.Answers.Count.ShouldBe(1);
        report.Answers[0].Value.ShouldBe("last Saturday");
    }

    [Fact]
    public void Given_an_option_code_that_is_not_a_province_When_it_is_recorded_Then_the_province_stays_unanswered()
    {
        // Given
        var question = Question.Create(
            "province", QuestionType.SingleSelect, Locale.EnCa, "Province", Now, role: QuestionRole.Province);
        question.CurrentVersion.AddOption("somewhere_else", "Somewhere else", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, ["somewhere_else"], Now);

        // Then — nothing is guessed
        report.Province.ShouldBe(Province.NotAnswered);
    }

    [Fact]
    public void Given_an_injury_code_that_is_not_a_severity_When_it_is_recorded_Then_severity_stays_unanswered()
    {
        // Given
        var question = Question.Create(
            "pilot_injury", QuestionType.SingleSelect, Locale.EnCa, "Pilot injury", Now,
            role: QuestionRole.PilotInjury);
        question.CurrentVersion.AddOption("hurt_pride", "Hurt pride", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, ["hurt_pride"], Now);

        // Then — a severity the system does not know is not a severity it may act on
        report.PilotInjury.ShouldBe(InjurySeverity.NotAnswered);
        report.InvolvesSeriousInjury.ShouldBeFalse();
    }

    [Theory]
    [InlineData(QuestionRole.None)]
    [InlineData(QuestionRole.Narrative)]
    [InlineData(QuestionRole.AircraftType)]
    [InlineData(QuestionRole.AircraftCertification)]
    public void Given_a_question_whose_role_projects_nowhere_When_it_is_answered_Then_the_answer_is_simply_recorded(QuestionRole role)
    {
        // Given
        var question = Question.Create("description", QuestionType.LongText, Locale.EnCa, "Describe it", Now, role: role);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, "The wing collapsed on approach.", Now);

        // Then
        report.Answers.Count.ShouldBe(1);
        report.OccurredOn.ShouldBeNull();
        report.Province.ShouldBe(Province.NotAnswered);
        report.ConsentPublish.ShouldBeNull();
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void Given_a_consent_role_on_a_text_question_When_it_is_answered_Then_a_boolean_word_is_read(string given, bool expected)
    {
        // Given — the role can be moved to a question that is not the YesNo one
        var question = Question.Create(
            "consent", QuestionType.ShortText, Locale.EnCa, "May we publish?", Now,
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
        var question = Question.Create("description", QuestionType.LongText, Locale.EnCa, "Describe it", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(question, ["something"], Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_report_When_its_record_is_read_Then_answers_aircraft_files_and_summaries_are_all_there()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        var question = Question.Create("damage", QuestionType.ShortText, Locale.EnCa, "Damage", Now);

        // When
        report.Answer(question, "A broken riser", Now);
        report.AddAircraft(Discipline.HangGliding, "Wills Wing", "T3", "topless");
        report.AddFile("kJQP7kiw5Fk/original/clip.mp4", "video/mp4", 4096, Now);
        report.AddSummary(Summary.Generated(report.Id, Locale.EnCa, "A hang glider landed short.", "model", "v1", Now));

        // Then
        report.Answers.Count.ShouldBe(1);
        report.Aircraft.Count.ShouldBe(1);
        report.Files.Count.ShouldBe(1);
        report.Summaries.Count.ShouldBe(1);
    }

    [Fact]
    public void Given_an_ordinary_question_When_it_is_deactivated_Then_it_stops_being_asked()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, Locale.EnCa, "Damage", Now);
        question.CurrentVersion.AttachTranslation(Locale.FrCa, "Dommages", null, null, Now);
        question.Activate();

        // When
        question.Deactivate();

        // Then — every answer already given to it survives
        question.IsActive.ShouldBeFalse();
        question.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Given_a_version_When_its_contents_are_read_Then_options_and_translations_are_exposed()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, Locale.EnCa, "Time of day", Now);
        var option = question.CurrentVersion.AddOption("morning", "Morning", Now);
        option.AttachTranslation(Locale.FrCa, "Matin", Now);
        question.CurrentVersion.AttachTranslation(Locale.FrCa, "Moment de la journée", null, null, Now);

        // When
        var version = question.CurrentVersion;

        // Then
        version.Options.Count.ShouldBe(1);
        version.Translations.Count.ShouldBe(2);
        option.Translations.Count.ShouldBe(2);
        version.IsFullyTranslated.ShouldBeTrue();
        version.MissingLocales.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_source_option_label_When_rewording_it_directly_is_attempted_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, Locale.EnCa, "Time of day", Now);
        var option = question.CurrentVersion.AddOption("morning", "Morning", Now);

        // When
        var rewording = () => option.Translation(Locale.EnCa)!.ReviseByHand("Early morning", Now);

        // Then
        rewording.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_generated_option_label_When_it_is_blanked_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, Locale.EnCa, "Time of day", Now);
        var option = question.CurrentVersion.AddOption("morning", "Morning", Now);
        var french = option.AttachTranslation(Locale.FrCa, "Matin", Now);

        // When
        var blanking = () => french.ReviseByHand("   ", Now);

        // Then
        blanking.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_generated_option_label_When_it_is_created_blank_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, Locale.EnCa, "Time of day", Now);
        var option = question.CurrentVersion.AddOption("morning", "Morning", Now);

        // When
        var attaching = () => option.AttachTranslation(Locale.FrCa, "  ", Now);

        // Then
        attaching.ShouldThrow<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_no_code_at_all_When_it_is_parsed_Then_nothing_is_guessed(string? code)
    {
        // Given / When
        var parsed = EnumCode.TryParse<InjurySeverity>(code, out var severity);

        // Then
        parsed.ShouldBeFalse();
        severity.ShouldBe(InjurySeverity.NotAnswered);
    }

    [Fact]
    public void Given_consent_was_never_answered_When_publication_is_attempted_Then_the_refusal_says_so()
    {
        // Given — the strongest form of the gate: nobody said no, and nobody said yes
        var report = new Report(Locale.EnCa, Now);
        report.Approve();

        // When
        var publishing = report.MarkPublished;

        // Then
        publishing.ShouldThrow<DomainRuleViolationException>()
            .Message.ShouldContain("unanswered consent is not a consent");
    }

    [Fact]
    public void Given_a_consent_role_on_a_text_question_When_the_answer_is_no_Then_consent_is_refused()
    {
        // Given
        var question = Question.Create(
            "consent", QuestionType.ShortText, Locale.EnCa, "May we publish?", Now,
            role: QuestionRole.ConsentPublish);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(question, "no", Now);

        // Then
        report.ConsentPublish.ShouldBe(false);
        report.HasAnsweredConsent.ShouldBeTrue();
    }

    [Fact]
    public void Given_consent_and_approval_but_no_summaries_When_publication_is_attempted_Then_it_is_blocked()
    {
        // Given
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish?", Now);
        var report = new Report(Locale.EnCa, Now);
        report.Answer(consent, ["yes"], Now);
        report.Approve();

        // When
        var publishing = report.MarkPublished;

        // Then — there is nothing anonymized to publish yet
        report.IsPublishable.ShouldBeFalse();
        publishing.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_question_created_in_a_section_When_it_is_read_Then_the_section_key_is_normalized()
    {
        // Given / When
        var question = Question.Create(
            "manufacturer", QuestionType.ShortText, Locale.EnCa, "Manufacturer", Now,
            sectionKey: "Aircraft Details");

        // Then
        question.SectionKey.ShouldBe("aircraft_details");
    }

    [Fact]
    public void Given_a_question_whose_options_are_only_half_translated_When_it_is_activated_Then_it_is_refused()
    {
        // Given — the question itself has both languages; one of its choices does not
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, Locale.EnCa, "Time of day", Now);
        question.CurrentVersion.AttachTranslation(Locale.FrCa, "Moment de la journée", null, null, Now);
        question.CurrentVersion.AddOption("morning", "Morning", Now);

        // When
        var activating = question.Activate;

        // Then — a reporter is never shown a half-translated choice either
        question.CurrentVersion.IsFullyTranslated.ShouldBeFalse();
        question.CurrentVersion.MissingLocales.ShouldBeEmpty();
        activating.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_ordinary_question_When_its_type_changes_Then_it_is_allowed()
    {
        // Given — only the consent question has a locked type
        var question = Question.Create("damage", QuestionType.ShortText, Locale.EnCa, "Damage", Now);

        // When
        var revised = question.Revise(QuestionType.LongText, isRequired: true, Locale.EnCa, "Describe the damage", Now);

        // Then
        revised.Type.ShouldBe(QuestionType.LongText);
        revised.IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Given_the_consent_question_When_it_is_reworded_at_the_same_type_Then_it_is_allowed()
    {
        // Given
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish?", Now);

        // When
        var revised = consent.Revise(
            QuestionType.YesNo, isRequired: true, Locale.EnCa,
            "Do you agree to HPAC publishing a de-identified version?", Now.AddDays(1));

        // Then
        revised.VersionNumber.ShouldBe(2);
        consent.Type.ShouldBe(QuestionType.YesNo);
    }

    [Fact]
    public void Given_a_consent_role_on_a_text_question_When_the_answer_is_neither_yes_nor_no_Then_it_is_refused()
    {
        // Given
        var question = Question.Create(
            "consent", QuestionType.ShortText, Locale.EnCa, "May we publish?", Now,
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
    public void Given_consent_and_approved_summaries_but_no_officer_approval_When_publishability_is_checked_Then_it_is_false()
    {
        // Given — the human gate is separate from the consent gate
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish?", Now);
        var report = new Report(Locale.EnCa, Now);
        report.Answer(consent, ["yes"], Now);

        var english = Summary.Generated(report.Id, Locale.EnCa, "A pilot landed hard.", "model", "v1", Now);
        var french = Summary.TranslatedFrom(english, Locale.FrCa, "Un pilote a atterri durement.", "model", "v1", Now);
        var officer = TinyId.New();
        english.Approve(officer, Now);
        french.Approve(officer, Now);
        report.AddSummary(english);
        report.AddSummary(french);

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
        var consent = Question.CreateConsentPublish(Locale.EnCa, "May we publish?", Now);
        var report = new Report(Locale.EnCa, Now);
        report.Answer(consent, ["yes"], Now);

        var english = Summary.Generated(report.Id, Locale.EnCa, "A pilot landed hard.", "model", "v1", Now);
        var french = Summary.TranslatedFrom(english, Locale.FrCa, "Un pilote a atterri durement.", "model", "v1", Now);
        var officer = TinyId.New();
        english.Approve(officer, Now);
        french.Approve(officer, Now);
        report.AddSummary(english);
        report.AddSummary(french);
        report.Approve();
        report.MarkPublished();

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
            "manufacturer", QuestionType.ShortText, Locale.EnCa, "Manufacturer", Now, sectionKey: "aircraft");

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
