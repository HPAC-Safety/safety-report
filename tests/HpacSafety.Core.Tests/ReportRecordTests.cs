using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The rest of the record a report carries: media, its summary, the allowlist,
/// and the audit trail.
/// </summary>
public class ReportRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_an_upload_When_it_lands_Then_it_awaits_stripping_before_anyone_sees_it()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var file = report.AddFile("dQw4w9WgXcQ/original/photo.jpg", "image/jpeg", 2048, Now);

        // Then — GPS above all
        file.AwaitsStripping.ShouldBeTrue();
        file.StrippedBlobKey.ShouldBeNull();
        file.ByteSize.ShouldBe(2048);
        file.Kind.ShouldBe(AttachmentKind.Image);
    }

    [Fact]
    public void Given_a_video_upload_When_it_lands_Then_it_is_classified_as_video()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var file = report.AddFile("kJQP7kiw5Fk/original/clip.mp4", "video/mp4", 4096, Now);

        // Then
        file.Kind.ShouldBe(AttachmentKind.Video);
    }

    [Fact]
    public void Given_a_document_upload_When_it_lands_Then_it_is_classified_as_a_document()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var file = report.AddFile("dQw4w9WgXcQ/original/report.pdf", "application/pdf", 1024, Now);

        // Then — validated and kept private, never anonymized or published
        file.Kind.ShouldBe(AttachmentKind.Document);
    }

    [Fact]
    public void Given_an_upload_When_exif_is_stripped_Then_the_derivative_is_what_a_reviewer_sees()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        var file = report.AddFile("dQw4w9WgXcQ/original/photo.jpg", "image/jpeg", 2048, Now);

        // When
        file.RecordStripped("dQw4w9WgXcQ/stripped/photo.jpg", Now.AddSeconds(30));

        // Then
        file.AwaitsStripping.ShouldBeFalse();
        file.StrippedBlobKey.ShouldBe("dQw4w9WgXcQ/stripped/photo.jpg");
        file.ExifStrippedAt.ShouldBe(Now.AddSeconds(30));
    }

    [Fact]
    public void Given_an_uploaded_file_When_it_is_linked_to_its_answer_Then_the_link_is_recorded()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        var file = report.AddFile("dQw4w9WgXcQ/original/photo.jpg", "image/jpeg", 2048, Now);
        var answerId = TinyId.New();

        // When
        file.LinkToAnswer(answerId);

        // Then
        file.ReportAnswerId.ShouldBe(answerId);
    }

    [Fact]
    public void Given_a_report_with_no_summary_When_one_is_attached_Then_it_is_recorded()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        report.AttachSummary(Summary.Generate(report.Id, "One.", "Un.", "model", "v1", Now));

        // Then
        report.Summary.ShouldNotBeNull();
    }

    [Fact]
    public void Given_a_report_that_already_has_a_summary_When_a_second_is_attached_Then_it_is_refused()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.AttachSummary(Summary.Generate(report.Id, "One.", "Un.", "model", "v1", Now));

        // When
        var attaching = () => report.AttachSummary(Summary.Generate(report.Id, "Two.", "Deux.", "model", "v1", Now));

        // Then
        attaching.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_approved_summary_When_its_English_text_is_rewritten_by_hand_Then_the_approval_is_withdrawn()
    {
        // Given
        var summary = Summary.Generate(TinyId.New(), "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
        summary.Approve(TinyId.New(), Now);

        // When
        summary.RewriteEn("A pilot landed hard in gusty conditions.", Now);

        // Then — editing after approval must not carry the approval forward
        summary.IsApproved.ShouldBeFalse();
        summary.AiSummaryEn.ShouldBe("A pilot landed hard in gusty conditions.");
    }

    [Fact]
    public void Given_an_approved_summary_When_its_French_text_is_rewritten_by_hand_Then_the_approval_is_withdrawn()
    {
        // Given
        var summary = Summary.Generate(TinyId.New(), "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
        summary.Approve(TinyId.New(), Now);

        // When
        summary.RewriteFr("Un pilote a atterri durement, dans des conditions venteuses.", Now);

        // Then
        summary.IsApproved.ShouldBeFalse();
        summary.AiSummaryFr.ShouldBe("Un pilote a atterri durement, dans des conditions venteuses.");
    }

    [Fact]
    public void Given_a_summary_When_its_English_text_is_rewritten_blank_Then_it_is_refused()
    {
        // Given
        var summary = Summary.Generate(TinyId.New(), "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);

        // When
        var rewriting = () => summary.RewriteEn("   ", Now);

        // Then
        rewriting.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_failed_summarization_When_it_later_succeeds_Then_the_error_is_cleared()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.BeginSummarizing();
        report.FailSummarization("the model returned 503");

        // When
        report.AwaitReview();

        // Then
        report.Status.ShouldBe(ReportStatus.PendingReview);
        report.SummaryError.ShouldBeNull();
    }

    [Fact]
    public void Given_a_rejected_report_When_publication_is_attempted_Then_it_is_refused()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.Answer(Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now), ["yes"], Now);

        // When
        report.Reject();

        // Then
        report.Status.ShouldBe(ReportStatus.Rejected);
        Should.Throw<DomainRuleViolationException>(() => report.MarkPublished(Now));
    }

    [Fact]
    public void Given_an_administrator_When_they_are_revoked_Then_they_may_no_longer_edit_questions()
    {
        // Given
        var admin = new AdminUser("member-1", AdminRole.Administrator, Now);
        admin.MayEditQuestions.ShouldBeTrue();

        // When
        admin.Revoke();

        // Then
        admin.IsActive.ShouldBeFalse();
        admin.MayEditQuestions.ShouldBeFalse();
    }

    [Fact]
    public void Given_a_safety_officer_When_their_permissions_are_checked_Then_they_may_not_edit_questions()
    {
        // Given
        var officer = new AdminUser("member-2", AdminRole.SafetyOfficer, Now);

        // When
        var mayEdit = officer.MayEditQuestions;

        // Then
        mayEdit.ShouldBeFalse();

        // And when promoted
        officer.ChangeRole(AdminRole.Administrator);
        officer.MayEditQuestions.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_moderation_action_When_it_is_audited_Then_it_records_who_and_when_and_not_the_content()
    {
        // Given
        var adminId = TinyId.New();
        var reportId = TinyId.New();

        // When
        var entry = new AuditLogEntry(adminId, AuditAction.ViewedRawReport, nameof(Report), reportId, Now);

        // Then — log identifiers, not report content
        entry.AdminUserId.ShouldBe(adminId);
        entry.TargetId.ShouldBe(reportId);
        entry.Action.ShouldBe(AuditAction.ViewedRawReport);
        entry.OccurredAt.ShouldBe(Now);
        entry.Detail.ShouldBeNull();
    }
}
