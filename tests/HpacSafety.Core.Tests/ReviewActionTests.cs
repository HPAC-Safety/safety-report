using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     The reviewer's commands on a report: approve (publishing when consented),
///     reject with an optional note, reopen, unpublish, edit the pair, and write a
///     pair by hand after a failure (ADR-0105, REQ-DOM-001, REQ-DOM-014).
/// </summary>
public class ReviewActionTests
{
	private const string Officer = "subject-officer";
	private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Later = Now.AddHours(1);

	[Fact]
	public void GivenConsentedPendingReport_WhenPairIsApproved_ThenPublishedInTheSameAction()
	{
		// Given
		var report = Pending("yes");

		// When
		var published = report.ApprovePair(Officer, Later);

		// Then
		published.ShouldBeTrue();
		report.Status.ShouldBe(ReportStatus.Published);
		report.PublishedAt.ShouldBe(Later);
		report.Summary!.ApprovedBySubject.ShouldBe(Officer);
		report.IsPublishable.ShouldBeTrue();
	}

	[Fact]
	public void GivenUnconsentedPendingReport_WhenPairIsApproved_ThenApprovedAndNeverPublic()
	{
		// Given
		var report = Pending("no");

		// When
		var published = report.ApprovePair(Officer, Later);

		// Then
		published.ShouldBeFalse();
		report.Status.ShouldBe(ReportStatus.Approved);
		report.PublishedAt.ShouldBeNull();
		report.Summary!.IsApproved.ShouldBeTrue();
		report.IsPublishable.ShouldBeFalse();
	}

	[Fact]
	public void GivenPendingReport_WhenRejectedWithNote_ThenRejectedAndNoteKeptTrimmed()
	{
		// Given
		var report = Pending("yes");

		// When
		report.RejectReview("  Duplicate of an earlier report.  ");

		// Then
		report.Status.ShouldBe(ReportStatus.Rejected);
		report.RejectionNote.ShouldBe("Duplicate of an earlier report.");
		report.IsPublishable.ShouldBeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("   ")]
	public void GivenPendingReport_WhenRejectedWithoutNote_ThenRejectedWithNoNote(string? note)
	{
		// Given
		var report = Pending("yes");

		// When
		report.RejectReview(note);

		// Then
		report.Status.ShouldBe(ReportStatus.Rejected);
		report.RejectionNote.ShouldBeNull();
	}

	[Fact]
	public void GivenTooLongNote_WhenRejected_ThenRefusedAndStillPending()
	{
		// Given
		var report = Pending("yes");

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.RejectReview(new string('x', Report.RejectionNoteMaxLength + 1)));
		report.Status.ShouldBe(ReportStatus.PendingReview);
	}

	[Fact]
	public void GivenRejectedReport_WhenReopened_ThenPendingReviewAndNoteCleared()
	{
		// Given
		var report = Pending("yes");
		report.RejectReview("Not enough detail.");

		// When
		report.Reopen();

		// Then
		report.Status.ShouldBe(ReportStatus.PendingReview);
		report.RejectionNote.ShouldBeNull();
	}

	[Fact]
	public void GivenPublishedReport_WhenUnpublished_ThenPendingReviewApprovalClearedAndNotPublishable()
	{
		// Given
		var report = Pending("yes");
		report.ApprovePair(Officer, Now);

		// When
		report.Unpublish();

		// Then
		report.Status.ShouldBe(ReportStatus.PendingReview);
		report.PublishedAt.ShouldBeNull();
		report.Summary!.IsApproved.ShouldBeFalse();
		report.Summary.ApprovedBySubject.ShouldBeNull();
		report.IsPublishable.ShouldBeFalse();
	}

	[Theory]
	[InlineData(ReportStatus.PendingReview)]
	[InlineData(ReportStatus.Approved)]
	[InlineData(ReportStatus.Published)]
	public void GivenReviewableReport_WhenPairIsEdited_ThenBothTextsSavedApprovalClearedAndPendingReview(ReportStatus from)
	{
		// Given
		var report = In(from);

		// When
		report.EditSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", Later);

		// Then
		report.Status.ShouldBe(ReportStatus.PendingReview);
		report.PublishedAt.ShouldBeNull();
		report.Summary!.AiSummaryEn.ShouldBe("The pilot landed firmly.");
		report.Summary.AiSummaryFr.ShouldBe("Le pilote s'est posé fermement.");
		report.Summary.UpdatedAt.ShouldBe(Later);
		report.Summary.IsApproved.ShouldBeFalse();
		report.IsPublishable.ShouldBeFalse();
	}

	[Fact]
	public void GivenBlankText_WhenPairIsEdited_ThenRefused()
	{
		// Given
		var report = Pending("yes");

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.EditSummary("Text.", " ", Later));
	}

	[Fact]
	public void GivenFailedReport_WhenPairIsWrittenByHand_ThenManualProvenanceAndPendingReview()
	{
		// Given
		var report = Consented("yes");
		report.BeginSummarizing();
		report.FailSummarization("The provider was unavailable.");

		// When
		report.WriteManualSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", Later);

		// Then
		report.Status.ShouldBe(ReportStatus.PendingReview);
		report.SummaryError.ShouldBeNull();
		report.Summary!.Model.ShouldBe("manual");
		report.Summary.PromptVersion.ShouldBe("manual");
		report.Summary.GeneratedAt.ShouldBe(Later);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void GivenUnconsentedReport_WhenItGoesToReviewWithoutSummary_ThenPendingReviewWithNoSummary(bool claimed)
	{
		// Given
		var report = Consented("no");
		if (claimed)
		{
			report.BeginSummarizing();
		}

		// When
		report.ReviewWithoutSummary();

		// Then
		report.Status.ShouldBe(ReportStatus.PendingReview);
		report.Summary.ShouldBeNull();
		report.IsPublishable.ShouldBeFalse();
		Should.Throw<DomainRuleViolationException>(() => report.ApprovePair(Officer, Later));
	}

	[Fact]
	public void GivenUnsummarizedReport_WhenPairIsEdited_ThenRefusedBecauseThereIsNoPair()
	{
		// Given — a report without consent is never summarized (REQ-DOM-006)
		var report = Consented("no");
		report.ReviewWithoutSummary();

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.EditSummary("en", "fr", Later)).Message.ShouldContain("no summary pair");
	}

	[Fact]
	public void GivenUnansweredConsent_WhenPairIsApproved_ThenApprovedAndNotPublished()
	{
		// Given — an older report with no consent answer at all
		var report = new Report(Locale.EnCa, Now);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", Now));
		report.AwaitReview();

		// When
		var published = report.ApprovePair(Officer, Later);

		// Then — silence is not consent
		published.ShouldBeFalse();
		report.Status.ShouldBe(ReportStatus.Approved);
	}

	[Fact]
	public void GivenConsentedReport_WhenItGoesToReviewWithoutSummary_ThenRefused()
	{
		// Given
		var report = Consented("yes");

		// When / Then
		Should.Throw<DomainRuleViolationException>(report.ReviewWithoutSummary);
		report.Status.ShouldBe(ReportStatus.Submitted);
	}

	[Fact]
	public void GivenPendingReport_WhenItGoesToReviewWithoutSummary_ThenRefusedAsATransition()
	{
		// Given
		var report = Pending("no");

		// When / Then
		Should.Throw<ReviewTransitionException>(report.ReviewWithoutSummary);
	}

	[Theory]
	[InlineData(ReportStatus.Submitted, "approve")]
	[InlineData(ReportStatus.SummaryFailed, "approve")]
	[InlineData(ReportStatus.Rejected, "approve")]
	[InlineData(ReportStatus.Approved, "approve")]
	[InlineData(ReportStatus.Rejected, "edit")]
	[InlineData(ReportStatus.SummaryFailed, "edit")]
	[InlineData(ReportStatus.Published, "reject")]
	[InlineData(ReportStatus.Approved, "reject")]
	[InlineData(ReportStatus.PendingReview, "reopen")]
	[InlineData(ReportStatus.PendingReview, "unpublish")]
	[InlineData(ReportStatus.Approved, "unpublish")]
	[InlineData(ReportStatus.PendingReview, "manual")]
	public void GivenStateThatDoesNotAllowAction_WhenAttempted_ThenRefusedAndUnchanged(ReportStatus from,
																					   string action)
	{
		// Given
		var report = In(from);

		// When
		var attempt = Attempt(report, action);

		// Then
		Should.Throw<ReviewTransitionException>(attempt).Message.ShouldContain(" cannot ");
		report.Status.ShouldBe(from);
	}

	[Theory]
	[InlineData("approve")]
	[InlineData("reject")]
	[InlineData("edit")]
	public void GivenDeletedReport_WhenAnyActionIsAttempted_ThenRefused(string action)
	{
		// Given
		var report = Pending("yes");
		report.SoftDelete(Now);

		// When / Then
		Should.Throw<DomainRuleViolationException>(Attempt(report, action));
	}

	[Fact]
	public void GivenTransitionMessage_WhenRead_ThenNamesStatusAndActionOnly()
	{
		// Given / When
		var refusal = new ReviewTransitionException("approve the pair", ReportStatus.SummaryFailed);

		// Then
		refusal.Message.ShouldBe("A report that is summary failed cannot approve the pair.");
	}

	[Fact]
	public void GivenStandardConstructors_WhenUsed_ThenCarryTheirMessage()
	{
		// Given / When / Then
		new ReviewTransitionException().Message.ShouldNotBeNullOrWhiteSpace();
		new ReviewTransitionException("refused").Message.ShouldBe("refused");
		new ReviewTransitionException("refused", new InvalidOperationException()).InnerException.ShouldNotBeNull();
	}

	private static Action Attempt(Report report,
								  string action)
	{
		return action switch
		{
			"approve" => () => report.ApprovePair(Officer, Later),
			"reject" => () => report.RejectReview(null),
			"edit" => () => report.EditSummary("en", "fr", Later),
			"reopen" => report.Reopen,
			"unpublish" => report.Unpublish,
			"manual" => () => report.WriteManualSummary("en", "fr", Later),
			_ => throw new ArgumentOutOfRangeException(nameof(action)),
		};
	}

	private static Report In(ReportStatus status)
	{
		var report = status switch
		{
			ReportStatus.Submitted => Consented("yes"),
			ReportStatus.SummaryFailed => Failed(),
			ReportStatus.PendingReview => Pending("yes"),
			ReportStatus.Approved => Approved(),
			ReportStatus.Published => Published(),
			ReportStatus.Rejected => Rejected(),
			_ => throw new ArgumentOutOfRangeException(nameof(status)),
		};

		report.Status.ShouldBe(status);
		return report;
	}

	private static Report Failed()
	{
		var report = Consented("yes");
		report.BeginSummarizing();
		report.FailSummarization("The provider was unavailable.");
		return report;
	}

	private static Report Approved()
	{
		var report = Pending("no");
		report.ApprovePair(Officer, Now);
		return report;
	}

	private static Report Published()
	{
		var report = Pending("yes");
		report.ApprovePair(Officer, Now);
		return report;
	}

	private static Report Rejected()
	{
		var report = Pending("yes");
		report.RejectReview(null);
		return report;
	}

	private static Report Pending(string consent)
	{
		var report = Consented(consent);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", Now));
		report.AwaitReview();
		return report;
	}

	private static Report Consented(string consent)
	{
		var report = new Report(Locale.EnCa, Now);
		report.Answer(
			Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now),
			[consent],
			Now);
		return report;
	}
}
