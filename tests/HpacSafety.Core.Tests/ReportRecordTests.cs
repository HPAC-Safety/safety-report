using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The rest of the record a report carries: aircraft, media, summaries, the
/// allowlist, and the audit trail.
/// </summary>
public class ReportRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_an_aircraft_When_it_is_added_Then_its_class_is_not_determined_until_something_normalizes_it()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);

        // When
        var aircraft = report.AddAircraft(Discipline.Paragliding, "Ozone", "Rush 6", "EN B (high)");

        // Then — nothing infers a class from a model name
        aircraft.Class.ShouldBe(AircraftClass.NotDetermined);
        aircraft.Manufacturer.ShouldBe("Ozone");
        aircraft.CertificationAnswer.ShouldBe("EN B (high)");
    }

    [Fact]
    public void Given_a_reporters_certification_answer_When_it_is_normalized_Then_the_class_is_recorded()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        var aircraft = report.AddAircraft(Discipline.Paragliding, "Ozone", "Rush 6", "high B");

        // When
        aircraft.Classify(AircraftClass.HighEnB);

        // Then
        aircraft.Class.ShouldBe(AircraftClass.HighEnB);
        report.Aircraft.Count.ShouldBe(1);
    }

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
    public void Given_two_summaries_in_one_language_When_the_second_is_added_Then_it_is_refused()
    {
        // Given
        var report = new Report(Locale.EnCa, Now);
        report.AddSummary(Summary.Generated(report.Id, Locale.EnCa, "One", "model", "v1", Now));

        // When
        var adding = () => report.AddSummary(Summary.Generated(report.Id, Locale.EnCa, "Two", "model", "v1", Now));

        // Then
        adding.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_an_approved_summary_When_it_is_rewritten_by_hand_Then_the_approval_is_withdrawn()
    {
        // Given
        var summary = Summary.Generated(Guid.NewGuid(), Locale.EnCa, "A pilot landed hard.", "model", "v1", Now);
        summary.Approve(Guid.NewGuid(), Now);

        // When
        summary.Rewrite("A pilot landed hard in gusty conditions.");

        // Then — editing after approval must not carry the approval forward
        summary.IsApproved.ShouldBeFalse();
        summary.Text.ShouldBe("A pilot landed hard in gusty conditions.");
    }

    [Fact]
    public void Given_a_summary_When_it_is_rewritten_blank_Then_it_is_refused()
    {
        // Given
        var summary = Summary.Generated(Guid.NewGuid(), Locale.EnCa, "A pilot landed hard.", "model", "v1", Now);

        // When
        var rewriting = () => summary.Rewrite("   ");

        // Then
        rewriting.ShouldThrow<DomainRuleViolationException>();
    }

    [Fact]
    public void Given_a_translated_summary_When_it_is_created_Then_it_points_at_the_one_it_came_from()
    {
        // Given
        var english = Summary.Generated(Guid.NewGuid(), Locale.EnCa, "A pilot landed hard.", "model", "v1", Now);

        // When
        var french = Summary.TranslatedFrom(english, Locale.FrCa, "Un pilote a atterri durement.", "model", "v1", Now);

        // Then — a reviewer must be able to tell an original from a translation
        french.IsSource.ShouldBeFalse();
        french.TranslatedFromSummaryId.ShouldBe(english.Id);
        french.ReportId.ShouldBe(english.ReportId);
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
        report.Answer(Question.CreateConsentPublish(Locale.EnCa, "May we publish?", Now), ["yes"], Now);

        // When
        report.Reject();

        // Then
        report.Status.ShouldBe(ReportStatus.Rejected);
        Should.Throw<DomainRuleViolationException>(report.MarkPublished);
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
        var adminId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

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
