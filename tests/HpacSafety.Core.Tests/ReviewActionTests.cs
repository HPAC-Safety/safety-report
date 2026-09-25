using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     The reviewer's commands on a report: publish, unpublish with an optional
///     note, edit the pair, and write a pair by hand after a failure; and the
///     Worker keeping a report without consent unpublished for good (ADR-0125,
///     REQ-DOM-001, REQ-DOM-014, REQ-DOM-015).
/// </summary>
public class ReviewActionTests
{
	private const string Officer = "subject-officer";
	private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Later = Now.AddHours(1);

	[Theory]
	[InlineData(ReportStatus.Pending)]
	[InlineData(ReportStatus.Unpublished)]
	public void GivenConsentedReport_WhenPublished_ThenPairApprovedAndPublic(ReportStatus from)
	{
		// Given
		var report = In(from);

		// When
		report.Publish(Officer, Later);

		// Then
		report.Status.ShouldBe(ReportStatus.Published);
		report.PublishedAt.ShouldBe(Later);
		report.UnpublishNote.ShouldBeNull();
		report.Summary!.ApprovedBySubject.ShouldBe(Officer);
		report.IsPublishable.ShouldBeTrue();
	}

	[Fact]
	public void GivenUnansweredConsent_WhenPublished_ThenRefused()
	{
		// Given — an older report with no consent answer at all
		var report = new Report(Locale.EnCa, Now);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", Now));
		report.AwaitReview();

		// When / Then — silence is not consent
		Should.Throw<DomainRuleViolationException>(() => report.Publish(Officer, Later)).Message.ShouldContain("no answer");
		report.Status.ShouldBe(ReportStatus.Pending);
	}

	[Fact]
	public void GivenRefusedConsentWithPair_WhenPublished_ThenRefused()
	{
		// Given — never reachable through the Worker, but the guard is the domain's
		var report = Summarized(false);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.Publish(Officer, Later)).Message.ShouldContain("did not consent");
		report.Status.ShouldBe(ReportStatus.Pending);
		report.Summary!.IsApproved.ShouldBeFalse();
	}

	[Theory]
	[InlineData(ReportStatus.Pending)]
	[InlineData(ReportStatus.Published)]
	public void GivenReport_WhenUnpublishedWithNote_ThenUnpublishedApprovalClearedAndNoteKeptTrimmed(ReportStatus from)
	{
		// Given
		var report = In(from);

		// When
		report.Unpublish("  Duplicate of an earlier report.  ");

		// Then
		report.Status.ShouldBe(ReportStatus.Unpublished);
		report.UnpublishNote.ShouldBe("Duplicate of an earlier report.");
		report.PublishedAt.ShouldBeNull();
		report.Summary!.IsApproved.ShouldBeFalse();
		report.IsPublishable.ShouldBeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("   ")]
	public void GivenPendingReport_WhenUnpublishedWithoutNote_ThenUnpublishedWithNoNote(string? note)
	{
		// Given
		var report = In(ReportStatus.Pending);

		// When
		report.Unpublish(note);

		// Then
		report.Status.ShouldBe(ReportStatus.Unpublished);
		report.UnpublishNote.ShouldBeNull();
	}

	[Fact]
	public void GivenTooLongNote_WhenUnpublished_ThenRefusedAndStillPending()
	{
		// Given
		var report = In(ReportStatus.Pending);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.Unpublish(new string('x', Report.UnpublishNoteMaxLength + 1)));
		report.Status.ShouldBe(ReportStatus.Pending);
	}

	[Fact]
	public void GivenUnpublishedReportWithNote_WhenPublishedAgain_ThenNoteCleared()
	{
		// Given
		var report = In(ReportStatus.Pending);
		report.Unpublish("Not enough detail.");

		// When
		report.Publish(Officer, Later);

		// Then
		report.UnpublishNote.ShouldBeNull();
	}

	[Theory]
	[InlineData(ReportStatus.Pending)]
	[InlineData(ReportStatus.Published)]
	[InlineData(ReportStatus.Unpublished)]
	public void GivenReviewableReport_WhenPairIsEdited_ThenBothTextsSavedApprovalClearedAndPending(ReportStatus from)
	{
		// Given
		var report = In(from);

		// When
		report.EditSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", Later);

		// Then
		report.Status.ShouldBe(ReportStatus.Pending);
		report.PublishedAt.ShouldBeNull();
		report.UnpublishNote.ShouldBeNull();
		report.Summary!.AiSummaryEn.ShouldBe("The pilot landed firmly.");
		report.Summary.AiSummaryFr.ShouldBe("Le pilote s'est posé fermement.");
		report.Summary.UpdatedAt.ShouldBe(Later);
		report.Summary.IsApproved.ShouldBeFalse();
		report.IsPublishable.ShouldBeFalse();
	}

	[Fact]
	public void GivenGeneratedPair_WhenOnlyEnglishIsEdited_ThenEnglishIsHumanAndFrenchStaysGenerated()
	{
		// Given
		var report = Summarized(true);

		// When
		report.EditSummary("The pilot landed firmly.", report.Summary!.AiSummaryFr, Later);

		// Then
		report.Summary.SourceEn.ShouldBe(SummaryTextSource.Human);
		report.Summary.SourceFr.ShouldBe(SummaryTextSource.Generated);
		report.Summary.IsApproved.ShouldBeFalse();
	}

	[Fact]
	public void GivenAcceptedTranslation_WhenPairIsSaved_ThenThatLanguageIsMachine()
	{
		// Given
		var report = Summarized(true);

		// When
		report.EditSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", Later, SummaryTextSource.Human, SummaryTextSource.Machine);

		// Then
		report.Summary!.SourceEn.ShouldBe(SummaryTextSource.Human);
		report.Summary.SourceFr.ShouldBe(SummaryTextSource.Machine);
	}

	[Fact]
	public void GivenUnchangedPair_WhenSaved_ThenSourcesAreKeptButApprovalStillClears()
	{
		// Given — a published pair saved without a change is still a review decision
		var report = In(ReportStatus.Published);
		var summary = report.Summary!;

		// When
		report.EditSummary(summary.AiSummaryEn, summary.AiSummaryFr, Later, SummaryTextSource.Machine, SummaryTextSource.Machine);

		// Then
		summary.SourceEn.ShouldBe(SummaryTextSource.Generated);
		summary.SourceFr.ShouldBe(SummaryTextSource.Generated);
		summary.IsApproved.ShouldBeFalse();
		report.Status.ShouldBe(ReportStatus.Pending);
	}

	[Fact]
	public void GivenFailedReport_WhenFrenchIsTypedAndEnglishTranslated_ThenSourcesSaySo()
	{
		// Given
		var report = Consented(true);
		report.BeginSummarizing();
		report.FailSummarization("The provider was unavailable.");

		// When
		report.WriteManualSummary("The pilot landed.", "Le pilote s'est posé.", Later, SummaryTextSource.Machine, SummaryTextSource.Human);

		// Then
		report.Summary!.SourceEn.ShouldBe(SummaryTextSource.Machine);
		report.Summary.SourceFr.ShouldBe(SummaryTextSource.Human);
	}

	[Fact]
	public void GivenReviewerClaimsGenerated_WhenPairIsEdited_ThenRefused()
	{
		// Given — only the Worker's model call produces generated text
		var report = Summarized(true);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			report.EditSummary("Changed.", report.Summary!.AiSummaryFr, Later, SummaryTextSource.Generated));
	}

	[Fact]
	public void GivenBlankText_WhenPairIsEdited_ThenRefused()
	{
		// Given
		var report = Summarized(true);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.EditSummary("Text.", " ", Later));
	}

	[Fact]
	public void GivenFailedReport_WhenPairIsWrittenByHand_ThenManualProvenanceAndPending()
	{
		// Given
		var report = Failed();

		// When
		report.WriteManualSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", Later);

		// Then
		report.Status.ShouldBe(ReportStatus.Pending);
		report.SummaryError.ShouldBeNull();
		report.Summary!.Model.ShouldBe("manual");
		report.Summary.PromptVersion.ShouldBe("manual");
		report.Summary.GeneratedAt.ShouldBe(Later);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void GivenUnconsentedReport_WhenKeptUnpublished_ThenUnpublishedForGoodWithNoSummary(bool claimed)
	{
		// Given
		var report = Consented(false);
		if (claimed)
		{
			report.BeginSummarizing();
		}

		// When
		report.KeepUnpublished();

		// Then
		report.Status.ShouldBe(ReportStatus.Unpublished);
		report.Summary.ShouldBeNull();
		report.IsUnpublishedForGood.ShouldBeTrue();
		report.IsPublishable.ShouldBeFalse();
	}

	[Theory]
	[InlineData("publish")]
	[InlineData("edit")]
	[InlineData("manual")]
	[InlineData("unpublish")]
	public void GivenUnpublishedForGood_WhenAnyReviewActionIsAttempted_ThenRefusedAndUnchanged(string action)
	{
		// Given
		var report = Consented(false);
		report.KeepUnpublished();

		// When
		var attempt = Attempt(report, action);

		// Then
		Should.Throw<ReviewTransitionException>(attempt).Message.ShouldContain(" cannot ");
		report.Status.ShouldBe(ReportStatus.Unpublished);
		report.Summary.ShouldBeNull();
	}

	[Fact]
	public void GivenUnpublishedForGood_WhenSoftDeleted_ThenDeleted()
	{
		// Given
		var report = Consented(false);
		report.KeepUnpublished();

		// When
		report.SoftDelete(Later);

		// Then
		report.Deleted.ShouldBe(Later);
	}

	[Fact]
	public void GivenUnansweredConsent_WhenKeptUnpublished_ThenUnpublishedForGoodAndCannotBePublished()
	{
		// Given — an older report with no consent answer: silence is not consent
		var report = new Report(Locale.EnCa, Now);

		// When
		report.KeepUnpublished();

		// Then
		report.IsUnpublishedForGood.ShouldBeTrue();
		Should.Throw<ReviewTransitionException>(() => report.Publish(Officer, Later));
	}

	[Theory]
	[InlineData("publish")]
	[InlineData("edit")]
	public void GivenPendingReportWithNoPair_WhenPairActionIsAttempted_ThenRefusedBecauseThereIsNoPair(string action)
	{
		// Given — pending with no pair attached, so there is nothing to approve or rewrite
		var report = Consented(true);
		report.BeginSummarizing();
		report.AwaitReview();

		// When / Then
		Should.Throw<DomainRuleViolationException>(Attempt(report, action)).Message.ShouldContain("no summary pair");
		report.Status.ShouldBe(ReportStatus.Pending);
	}

	[Fact]
	public void GivenConsentedReport_WhenKeptUnpublished_ThenRefused()
	{
		// Given
		var report = Consented(true);

		// When / Then
		Should.Throw<DomainRuleViolationException>(report.KeepUnpublished);
		report.Status.ShouldBe(ReportStatus.Submitted);
	}

	[Fact]
	public void GivenPendingReport_WhenKeptUnpublished_ThenRefusedAsATransition()
	{
		// Given
		var report = In(ReportStatus.Pending);

		// When / Then
		Should.Throw<ReviewTransitionException>(report.KeepUnpublished);
	}

	[Theory]
	[InlineData(ReportStatus.Submitted, "publish")]
	[InlineData(ReportStatus.SummaryFailed, "publish")]
	[InlineData(ReportStatus.Published, "publish")]
	[InlineData(ReportStatus.Submitted, "edit")]
	[InlineData(ReportStatus.SummaryFailed, "edit")]
	[InlineData(ReportStatus.Submitted, "unpublish")]
	[InlineData(ReportStatus.SummaryFailed, "unpublish")]
	[InlineData(ReportStatus.Unpublished, "unpublish")]
	[InlineData(ReportStatus.Pending, "manual")]
	[InlineData(ReportStatus.Published, "manual")]
	[InlineData(ReportStatus.Unpublished, "manual")]
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
	[InlineData("publish")]
	[InlineData("unpublish")]
	[InlineData("edit")]
	public void GivenDeletedReport_WhenAnyActionIsAttempted_ThenRefused(string action)
	{
		// Given
		var report = In(ReportStatus.Pending);
		report.SoftDelete(Now);

		// When / Then
		Should.Throw<DomainRuleViolationException>(Attempt(report, action));
	}

	[Fact]
	public void GivenTransitionMessage_WhenRead_ThenNamesStatusAndActionOnly()
	{
		// Given / When
		var refusal = new ReviewTransitionException("publish the pair", ReportStatus.SummaryFailed);

		// Then
		refusal.Message.ShouldBe("A report that is summary failed cannot publish the pair.");
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
			"publish" => () => report.Publish(Officer, Later),
			"unpublish" => () => report.Unpublish(),
			"edit" => () => report.EditSummary("en", "fr", Later),
			"manual" => () => report.WriteManualSummary("en", "fr", Later),
			_ => throw new ArgumentOutOfRangeException(nameof(action)),
		};
	}

	private static Report In(ReportStatus status)
	{
		var report = status switch
		{
			ReportStatus.Submitted => Consented(true),
			ReportStatus.SummaryFailed => Failed(),
			ReportStatus.Pending => Summarized(true),
			ReportStatus.Published => Published(),
			ReportStatus.Unpublished => Unpublished(),
			_ => throw new ArgumentOutOfRangeException(nameof(status)),
		};

		report.Status.ShouldBe(status);
		return report;
	}

	private static Report Failed()
	{
		var report = Consented(true);
		report.BeginSummarizing();
		report.FailSummarization("The provider was unavailable.");
		return report;
	}

	private static Report Published()
	{
		var report = Summarized(true);
		report.Publish(Officer, Now);
		return report;
	}

	private static Report Unpublished()
	{
		var report = Summarized(true);
		report.Unpublish();
		return report;
	}

	private static Report Summarized(bool consent)
	{
		var report = Consented(consent);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", Now));
		report.AwaitReview();
		return report;
	}

	private static Report Consented(bool consent)
	{
		var report = new Report(Locale.EnCa, Now);
		report.Answer(
			Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now),
			consent,
			Now);
		return report;
	}
}
