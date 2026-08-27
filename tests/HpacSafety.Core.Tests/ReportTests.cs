using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// Consent gates publication, and it is the only answer a report reads by name.
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
        Should.Throw<DomainRuleViolationException>(() => report.MarkPublished(Now));
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
    public void Given_consent_and_an_approved_summary_When_publication_is_attempted_Then_it_succeeds()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);
        var officer = TinyId.New();

        var summary = Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
        summary.Approve(officer, Now);
        report.AttachSummary(summary);
        report.Approve();

        // When
        report.MarkPublished(Now);

        // Then
        report.Status.ShouldBe(ReportStatus.Published);
        report.PublishedAt.ShouldBe(Now);
    }

    [Fact]
    public void Given_the_summary_is_not_approved_When_publication_is_attempted_Then_it_is_blocked()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);

        var summary = Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
        report.AttachSummary(summary);
        report.Approve();

        // When
        var publishing = () => report.MarkPublished(Now);

        // Then — the human gate covers everything published
        report.IsPublishable.ShouldBeFalse();
        publishing.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_answer_When_it_is_recorded_Then_it_references_the_revision_it_was_asked_under()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);
        var askedUnder = question.CurrentRevision;
        var report = new Report(Locale.EnCa, Now);

        // When
        var answer = report.Answer(question, "Broken riser", Now);
        question.Revise(QuestionType.LongText, isRequired: false, "Describe the damage", "Décrivez les dommages", Now.AddDays(1));

        // Then — rewording tomorrow cannot change what an answer given today means
        answer.QuestionRevisionId.ShouldBe(askedUnder.Id);
        answer.QuestionRevisionId.ShouldNotBe(question.CurrentRevision.Id);
    }

    [Fact]
    public void Given_a_private_question_When_it_is_answered_Then_the_answer_snapshots_the_private_classification()
    {
        // Given
        var question = Question.Create("where", QuestionType.ShortText, "Where?", "Où ?", Now);
        var report = new Report(Locale.EnCa, Now);
        var answer = report.Answer(question, "A launch site", Now);

        // When / Then — the answer remains self-describing even though question privacy is immutable
        answer.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Given_an_unknown_option_code_When_it_is_answered_Then_it_is_refused()
    {
        // Given
        var question = Question.Create("time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now);
        question.CurrentRevision.AddOption("morning", "Morning", "Matin");
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
        Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now);
}
