using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     The rest of the record a report carries: media, its summary, the allowlist,
///     and the audit trail.
/// </summary>
public class ReportRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenUpload_WhenLands_ThenAwaitsStrippingBeforeAnyoneSees()
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
    public void GivenVideoUpload_WhenLands_ThenClassifiedAsVideo()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var file = report.AddFile("kJQP7kiw5Fk/original/clip.mp4", "video/mp4", 4096, Now);

        // Then
        file.Kind.ShouldBe(AttachmentKind.Video);
    }

    [Fact]
    public void GivenDocumentUpload_WhenLands_ThenClassifiedAsDocument()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var file = report.AddFile("dQw4w9WgXcQ/original/report.pdf", "application/pdf", 1024, Now);

        // Then — validated and kept private, never anonymized or published
        file.Kind.ShouldBe(AttachmentKind.Document);
    }

    [Fact]
    public void GivenUpload_WhenExifIsStripped_ThenDerivativeIsWhatReviewerSees()
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
    public void GivenUploadedFile_WhenLinkedToAnswer_ThenLinkIsRecorded()
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
    public void GivenReportWithNoSummary_WhenOneIsAttached_ThenRecorded()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        report.AttachSummary(Summary.Generate(report.Id, "One.", "Un.", "model", "v1", Now));

        // Then
        report.Summary.ShouldNotBeNull();
    }

    [Fact]
    public void GivenReportAlreadyHasSummary_WhenSecondIsAttached_ThenRefused()
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
    public void GivenApprovedSummary_WhenEnglishTextIsRewrittenByHand_ThenApprovalIsWithdrawn()
    {
        // Given
        var summary = Summary.Generate(TinyId.New(), "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
        summary.Approve("subject-officer", Now);

        // When
        summary.RewriteEn("A pilot landed hard in gusty conditions.", Now);

        // Then — editing after approval must not carry the approval forward
        summary.IsApproved.ShouldBeFalse();
        summary.AiSummaryEn.ShouldBe("A pilot landed hard in gusty conditions.");
    }

    [Fact]
    public void GivenApprovedSummary_WhenFrenchTextIsRewrittenByHand_ThenApprovalIsWithdrawn()
    {
        // Given
        var summary = Summary.Generate(TinyId.New(), "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
        summary.Approve("subject-officer", Now);

        // When
        summary.RewriteFr("Un pilote a atterri durement, dans des conditions venteuses.", Now);

        // Then
        summary.IsApproved.ShouldBeFalse();
        summary.AiSummaryFr.ShouldBe("Un pilote a atterri durement, dans des conditions venteuses.");
    }

    [Fact]
    public void GivenSummary_WhenEnglishTextIsRewrittenBlank_ThenRefused()
    {
        // Given
        var summary = Summary.Generate(TinyId.New(), "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);

        // When
        var rewriting = () => summary.RewriteEn("   ", Now);

        // Then
        rewriting.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void GivenFailedSummarization_WhenLaterSucceeds_ThenErrorIsCleared()
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
    public void GivenRejectedReport_WhenPublicationIsAttempted_ThenRefused()
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

    [Theory]
    [InlineData(MemberRole.User, MemberRole.SafetyOfficer)]
    [InlineData(MemberRole.SafetyOfficer, MemberRole.Administrator)]
    public void GivenTwoMemberRoles_WhenTheyAreCompared_ThenMorePrivilegedOneIsGreater(
        MemberRole lesser, MemberRole greater)
    {
        // Given / When
        var ordered = lesser < greater;

        // Then - the enum is ordered so "the highest role claim wins" is a comparison
        ordered.ShouldBeTrue();
    }

    [Fact]
    public void GivenModerationAction_WhenAudited_ThenRecordsWhoAndWhenAndNotContent()
    {
        // Given
        const string actorSubject = "auth0|synthetic-officer";
        var reportId = TinyId.New();

        // When
        var entry = new AuditLogEntry(actorSubject, AuditAction.ViewedRawReport, nameof(Report), reportId, Now);

        // Then — log identifiers, not report content
        entry.ActorSubject.ShouldBe(actorSubject);
        entry.TargetId.ShouldBe(reportId);
        entry.Action.ShouldBe(AuditAction.ViewedRawReport);
        entry.OccurredAt.ShouldBe(Now);
        entry.Detail.ShouldBeNull();
    }
}
