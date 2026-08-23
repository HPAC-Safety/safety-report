using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;

using Shouldly;

namespace HpacSafety.Core.Tests;

public sealed class ReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_an_optional_question_is_skipped_When_the_report_is_recorded_Then_the_exact_asked_revision_remains_visible()
    {
        // Given
        var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);
        var report = new Report(Locale.EnCa, Now);

        // When
        var answer = report.Answer(question, value: null, Now);

        // Then
        answer.QuestionId.ShouldBe(question.Id);
        answer.QuestionKey.ShouldBe("damage");
        answer.Value.ShouldBeNull();
        report.Answers.ShouldContain(answer);
    }

    [Fact]
    public void Given_a_question_is_revised_after_submission_When_the_answer_is_read_Then_it_still_references_the_original_revision()
    {
        // Given
        var asked = Question.Create("description", QuestionType.LongText, "Description", "Description", Now);
        var report = new Report(Locale.EnCa, Now);
        var answer = report.Answer(asked, "A hard landing.", Now);

        // When
        var current = asked.Revise(
            QuestionType.LongText,
            "Occurrence description",
            "Description de l’événement",
            Now.AddMinutes(1),
            isPrivate: false,
            sortOrder: 1,
            isActive: true);

        // Then
        answer.QuestionId.ShouldBe(asked.Id);
        answer.QuestionId.ShouldNotBe(current.Id);
    }

    [Fact]
    public void Given_consent_is_unanswered_When_submission_is_attempted_Then_it_is_refused()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var submitting = report.EnsureReadyForSubmission;

        // Then
        submitting.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_consent_is_answered_no_When_submission_is_checked_Then_the_report_is_accepted_but_not_publishable()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["no"], Now);

        // When
        report.EnsureReadyForSubmission();

        // Then
        report.ConsentPublish.ShouldBe(false);
        report.IsPublishable.ShouldBeFalse();
    }

    [Fact]
    public void Given_an_unknown_consent_value_When_it_is_recorded_Then_it_is_rejected()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var answering = () => report.Answer(ConsentQuestion(), ["maybe"], Now);

        // Then
        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_positive_consent_and_one_approved_summary_When_publication_is_attempted_Then_it_succeeds()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);
        var summary = Summary.Generated(report.Id, Locale.EnCa, "The pilot landed hard.", "model", "v4", Now);
        summary.Approve(TinyId.New(), Now);
        report.AddSummary(summary);
        report.Approve();

        // When
        report.MarkPublished();

        // Then
        report.Status.ShouldBe(ReportStatus.Published);
    }

    [Fact]
    public void Given_the_data_driven_report_When_its_public_surface_is_inspected_Then_only_consent_is_projected()
    {
        // Given / When
        var publicMembers = typeof(Report).GetMembers().Select(member => member.Name).ToArray();

        // Then
        publicMembers.ShouldNotContain("Aircraft");
        publicMembers.ShouldNotContain("PilotInjury");
        publicMembers.ShouldNotContain("PassengerInjury");
        publicMembers.ShouldNotContain("Province");
        publicMembers.ShouldNotContain("OccurredOn");
        publicMembers.ShouldNotContain("OccurredAtLocal");
        publicMembers.ShouldNotContain("TimeOfDay");
        publicMembers.ShouldNotContain("InvolvesSeriousInjury");
    }

    [Fact]
    public void Given_summarization_fails_When_the_failure_is_recorded_Then_the_report_remains_visible_for_review()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.BeginSummarizing();

        // When
        report.FailSummarization("provider unavailable");

        // Then
        report.Status.ShouldBe(ReportStatus.SummaryFailed);
        report.SummaryError.ShouldBe("provider unavailable");
    }

    private static Question ConsentQuestion() =>
        Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier?", Now);
}
