using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// Consent gates publication, and the answers that drive logic reach typed
/// properties through a question's role rather than through a hardcoded key.
/// </summary>
public class ReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_a_report_without_consent_When_it_is_approved_Then_it_is_not_publishable()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["no"], Now);

        // When
        report.Approve();

        // Then — stored, summarized, and counted internally; never published
        report.ConsentPublish.ShouldBe(false);
        report.IsPublishable.ShouldBeFalse();
        Should.Throw<DomainRuleViolationException>(report.MarkPublished);
    }

    [Fact]
    public void Given_consent_is_unanswered_When_submission_is_attempted_Then_it_is_refused()
    {
        // Given — no default, no third state: the reporter must choose
        var report = new Report(Locale.EnCa, Now);

        // When
        var submitting = report.EnsureReadyForSubmission;

        // Then
        report.ConsentPublish.ShouldBeNull();
        report.HasAnsweredConsent.ShouldBeFalse();
        submitting.ShouldThrow<DomainRuleViolationException>()
            .Message.ShouldContain("answered yes or no");
    }

    [Fact]
    public void Given_consent_is_answered_no_When_submission_is_checked_Then_it_is_allowed()
    {
        // Given — "no" is a complete answer; it only blocks publication
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["no"], Now);

        // When
        report.EnsureReadyForSubmission();

        // Then
        report.HasAnsweredConsent.ShouldBeTrue();
        report.ConsentPublish.ShouldBe(false);
    }

    [Fact]
    public void Given_an_unreadable_consent_answer_When_it_is_recorded_Then_it_is_refused()
    {
        // Given
        var consent = ConsentQuestion();
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(consent, ["maybe"], Now);

        // Then — an unreadable consent is an error, not a quiet no
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_consent_and_both_approved_summaries_When_publication_is_attempted_Then_it_succeeds()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);
        var officer = TinyId.New();

        var english = Summary.Generated(report.Id, Locale.EnCa, "A pilot landed hard.", "model", "v1", Now);
        var french = Summary.TranslatedFrom(english, Locale.FrCa, "Un pilote a atterri durement.", "model", "v1", Now);
        english.Approve(officer, Now);
        french.Approve(officer, Now);
        report.AddSummary(english);
        report.AddSummary(french);
        report.Approve();

        // When
        report.MarkPublished();

        // Then
        report.Status.ShouldBe(ReportStatus.Published);
    }

    [Fact]
    public void Given_only_the_English_summary_is_approved_When_publication_is_attempted_Then_it_is_blocked()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);

        var english = Summary.Generated(report.Id, Locale.EnCa, "A pilot landed hard.", "model", "v1", Now);
        var french = Summary.TranslatedFrom(english, Locale.FrCa, "Un pilote a atterri durement.", "model", "v1", Now);
        english.Approve(TinyId.New(), Now);
        report.AddSummary(english);
        report.AddSummary(french);
        report.Approve();

        // When
        var publishing = report.MarkPublished;

        // Then — the human gate covers everything published, in both languages
        report.IsPublishable.ShouldBeFalse();
        publishing.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_serious_injury_answer_When_it_is_recorded_Then_the_report_reports_a_serious_injury()
    {
        // Given
        var injury = Question.Create(
            "pilot_injury", QuestionType.SingleSelect, Locale.EnCa, "Pilot injury", Now,
            role: QuestionRole.PilotInjury);
        injury.CurrentVersion.AddOption("serious", "Serious injury (secondary medical aid)", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(injury, ["serious"], Now);

        // Then
        report.PilotInjury.ShouldBe(InjurySeverity.Serious);
        report.InvolvesSeriousInjury.ShouldBeTrue();
    }

    [Fact]
    public void Given_no_injury_question_on_the_form_When_a_report_is_filed_Then_severity_is_unanswered_rather_than_assumed()
    {
        // Given — every role except consent is optional, and its absence is a defined state
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);

        // When
        var severity = report.PilotInjury;

        // Then
        severity.ShouldBe(InjurySeverity.NotAnswered);
        report.InvolvesSeriousInjury.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_role_bearing_date_question_When_it_is_answered_Then_the_date_reaches_the_report()
    {
        // Given
        var date = Question.Create(
            "occurrence_date", QuestionType.Date, Locale.EnCa, "Date of the occurrence", Now,
            role: QuestionRole.OccurrenceDate);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(date, "2026-07-04", Now);

        // Then
        report.OccurredOn.ShouldBe(new DateOnly(2026, 7, 4));
    }

    [Fact]
    public void Given_a_province_answer_When_it_is_recorded_Then_the_invariant_code_resolves_to_the_enum()
    {
        // Given
        var province = Question.Create(
            "province", QuestionType.SingleSelect, Locale.EnCa, "Province", Now, role: QuestionRole.Province);
        province.CurrentVersion.AddOption("british_columbia", "British Columbia", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        report.Answer(province, ["british_columbia"], Now);

        // Then — display text never reaches the database
        report.Province.ShouldBe(Province.BritishColumbia);
    }

    [Fact]
    public void Given_an_answer_When_it_is_recorded_Then_it_references_the_version_it_was_asked_under()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, Locale.EnCa, "Damage", Now);
        var askedUnder = question.CurrentVersion;
        var report = new Report(Locale.EnCa, Now);

        // When
        var answer = report.Answer(question, "Broken riser", Now);
        question.Revise(QuestionType.LongText, isRequired: false, Locale.EnCa, "Describe the damage", Now.AddDays(1));

        // Then — rewording tomorrow cannot change what an answer given today means
        answer.QuestionVersionId.ShouldBe(askedUnder.Id);
        answer.QuestionVersionId.ShouldNotBe(question.CurrentVersion.Id);
    }

    [Fact]
    public void Given_a_private_question_When_it_is_answered_Then_the_answer_snapshots_the_private_classification()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, Locale.EnCa, "Where?", Now);
        var report = new Report(Locale.EnCa, Now);
        var answer = report.Answer(question, "A launch site", Now);

        // When / Then — the answer remains self-describing even though question privacy is immutable
        answer.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Given_an_unknown_option_code_When_it_is_answered_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, Locale.EnCa, "Time of day", Now);
        question.CurrentVersion.AddOption("morning", "Morning", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(question, ["midnight"], Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_summarization_fails_When_the_failure_is_recorded_Then_the_report_still_reaches_a_human()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.BeginSummarizing();

        // When
        report.FailSummarization("the model returned 503");

        // Then — a report can never become invisible
        report.Status.ShouldBe(ReportStatus.SummaryFailed);
        report.SummaryError.ShouldBe("the model returned 503");
    }

    private static Question ConsentQuestion() =>
        Question.CreateConsentPublish(Locale.EnCa, "May we publish a de-identified version?", Now);
}
