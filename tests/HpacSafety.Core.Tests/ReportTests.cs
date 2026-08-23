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

    [Theory]
    [InlineData(QuestionType.Statement)]
    [InlineData(QuestionType.Group)]
    public void Given_a_non_answering_question_When_text_is_recorded_Then_it_is_rejected(QuestionType type)
    {
        var report = new Report(Locale.EnCa, Now);
        var question = Question.Create("context", type, "Context", "Contexte", Now);

        var answering = () => report.Answer(question, "answer", Now);

        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_option_question_When_text_is_recorded_Then_it_is_rejected()
    {
        var report = new Report(Locale.EnCa, Now);
        var question = SelectQuestion(QuestionType.SingleSelect);

        var answering = () => report.Answer(question, "wind", Now);

        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_text_question_When_option_codes_are_recorded_Then_it_is_rejected()
    {
        var report = new Report(Locale.EnCa, Now);
        var question = Question.Create("description", QuestionType.LongText, "Description", "Description", Now);

        var answering = () => report.Answer(question, ["wind"], Now);

        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData(QuestionType.SingleSelect)]
    [InlineData(QuestionType.YesNo)]
    public void Given_a_single_value_question_When_multiple_codes_are_recorded_Then_it_is_rejected(QuestionType type)
    {
        var report = new Report(Locale.EnCa, Now);
        var question = type == QuestionType.YesNo ? ConsentQuestion() : SelectQuestion(type);

        var answering = () => report.Answer(question, ["yes", "no"], Now);

        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_consent_When_no_code_is_recorded_Then_it_is_rejected()
    {
        var report = new Report(Locale.EnCa, Now);

        var answering = () => report.Answer(ConsentQuestion(), [], Now);

        answering.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_multi_select_question_When_valid_codes_are_recorded_Then_the_codes_are_normalized()
    {
        var report = new Report(Locale.EnCa, Now);

        var answer = report.Answer(SelectQuestion(QuestionType.MultiSelect), ["WIND", "rain"], Now);

        answer.SelectedOptionCodes.ShouldBe(["wind", "rain"]);
        answer.SingleOptionCode.ShouldBeNull();
        report.ConsentPublish.ShouldBeNull();
    }

    [Fact]
    public void Given_the_same_question_revision_twice_When_both_answers_are_recorded_Then_the_second_is_rejected()
    {
        var report = new Report(Locale.EnCa, Now);
        var question = Question.Create("description", QuestionType.LongText, "Description", "Description", Now);
        report.Answer(question, "First answer", Now);

        var answeringAgain = () => report.Answer(question, "Second answer", Now);

        answeringAgain.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_positive_consent_and_both_locale_summaries_approved_When_publication_is_attempted_Then_it_succeeds()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);
        var en = Summary.Generated(report.Id, Locale.EnCa, "The pilot landed hard.", "model", "v4", Now);
        en.Approve(TinyId.New(), Now);
        report.AddSummary(en);
        var fr = Summary.Generated(report.Id, Locale.FrCa, "Le pilote a atterri durement.", "model", "v4", Now);
        fr.Approve(TinyId.New(), Now);
        report.AddSummary(fr);
        report.Approve();

        // When
        report.MarkPublished();

        // Then
        report.Status.ShouldBe(ReportStatus.Published);
        report.IsPublishable.ShouldBeTrue();
        report.Summaries.ShouldBe([en, fr]);
    }

    [Fact]
    public void Given_a_report_When_a_file_is_added_Then_it_belongs_to_that_report_and_starts_unstripped()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var file = report.AddFile("blob-key", "image/jpeg", 1024, Now);

        // Then
        report.Files.ShouldContain(file);
        file.ReportId.ShouldBe(report.Id);
        file.ExifStrippedAt.ShouldBeNull();
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

    [Fact]
    public void Given_summarization_fails_without_a_safe_error_When_recorded_Then_a_content_free_error_is_used()
    {
        var report = new Report(Locale.EnCa, Now);

        report.FailSummarization(" ");

        report.Status.ShouldBe(ReportStatus.SummaryFailed);
        report.SummaryError.ShouldBe("Summarization failed.");
    }

    [Fact]
    public void Given_a_failed_summary_When_it_moves_to_review_Then_the_error_is_cleared()
    {
        var report = new Report(Locale.EnCa, Now);
        report.FailSummarization("provider unavailable");

        report.AwaitReview();

        report.Status.ShouldBe(ReportStatus.PendingReview);
        report.SummaryError.ShouldBeNull();
    }

    [Fact]
    public void Given_a_report_without_every_publication_gate_When_publication_is_attempted_Then_it_is_rejected()
    {
        var report = new Report(Locale.EnCa, Now);

        var publishing = report.MarkPublished;

        publishing.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_positive_consent_but_no_summary_When_the_report_is_approved_Then_it_is_not_publishable()
    {
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);
        report.Approve();

        report.IsPublishable.ShouldBeFalse();
    }

    [Fact]
    public void Given_positive_consent_and_an_unapproved_summary_When_the_report_is_approved_Then_it_is_not_publishable()
    {
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);
        report.AddSummary(Summary.Generated(report.Id, Locale.EnCa, "The pilot landed hard.", "model", "v4", Now));
        report.Approve();

        report.IsPublishable.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_report_already_holding_an_English_summary_When_a_second_English_summary_is_added_Then_it_is_refused()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.AddSummary(Summary.Generated(report.Id, Locale.EnCa, "The pilot landed hard.", "model", "v4", Now));

        // When
        Action addingASecond = () =>
            report.AddSummary(Summary.Generated(report.Id, Locale.EnCa, "A second candidate.", "model", "v4", Now));

        // Then
        addingASecond.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_positive_consent_and_only_one_locale_approved_When_the_report_is_approved_Then_it_is_not_publishable()
    {
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);
        var en = Summary.Generated(report.Id, Locale.EnCa, "The pilot landed hard.", "model", "v4", Now);
        en.Approve(TinyId.New(), Now);
        report.AddSummary(en);
        report.AddSummary(Summary.Generated(report.Id, Locale.FrCa, "Le pilote a atterri durement.", "model", "v4", Now));
        report.Approve();

        report.IsPublishable.ShouldBeFalse();
    }

    [Fact]
    public void Given_positive_consent_and_an_approved_summary_without_report_approval_When_checked_Then_it_is_not_publishable()
    {
        var report = new Report(Locale.EnCa, Now);
        report.Answer(ConsentQuestion(), ["yes"], Now);
        var summary = Summary.Generated(report.Id, Locale.EnCa, "The pilot landed hard.", "model", "v4", Now);
        summary.Approve(TinyId.New(), Now);
        report.AddSummary(summary);

        report.IsPublishable.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_report_is_rejected_When_the_decision_is_recorded_Then_its_status_changes()
    {
        var report = new Report(Locale.EnCa, Now);

        report.Reject();

        report.Status.ShouldBe(ReportStatus.Rejected);
    }

    private static Question ConsentQuestion() =>
        Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier?", Now);

    private static Question SelectQuestion(QuestionType type) =>
        Question.Create(
            "conditions",
            type,
            "Conditions",
            "Conditions",
            Now,
            options:
            [
                new("wind", "Wind", "Vent"),
                new("rain", "Rain", "Pluie"),
            ]);
}
