using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     A summary is an append-only list of revisions: an edit adds one with its
///     author, a rollback adds one that copies an earlier one, and a live report
///     publishes what is saved (REQ-MOD-194..198, REQ-MOD-205, ADR-0177).
/// </summary>
public class SummaryRevisionTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset Later = Now.AddHours(1);
	private static readonly DateTimeOffset Latest = Now.AddHours(2);

	[Fact]
	public void GivenWorkerPair_WhenAttached_ThenItIsRevisionOneGeneratedWithNoAuthor()
	{
		// Given / When
		var report = Pending();

		// Then
		var revision = report.Summary!.Revisions.ShouldHaveSingleItem();
		revision.Sequence.ShouldBe(1);
		revision.AuthorSubject.ShouldBeNull();
		revision.SourceEn.ShouldBe(SummaryTextSource.Generated);
		revision.SourceFr.ShouldBe(SummaryTextSource.Generated);
		revision.CreatedAt.ShouldBe(Now);
		revision.RestoredFromId.ShouldBeNull();
	}

	[Fact]
	public void GivenFailedReport_WhenPairIsWrittenByHand_ThenItIsRevisionOneAuthoredByTheReviewer()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		report.BeginSummarizing();
		report.FailSummarization("The provider was unavailable.");

		// When
		report.WriteManualSummary("The pilot landed.", "Le pilote s'est posé.", "subject-writer", Later);

		// Then
		var revision = report.Summary!.Revisions.ShouldHaveSingleItem();
		revision.Sequence.ShouldBe(1);
		revision.AuthorSubject.ShouldBe("subject-writer");
		revision.Model.ShouldBe(Report.ManualProvenance);
		revision.PromptVersion.ShouldBe(Report.ManualProvenance);
	}

	[Fact]
	public void GivenPendingReport_WhenEditedTwice_ThenEachEditAddsARevisionAndKeepsTheEarlierOnesUntouched()
	{
		// Given
		var report = Pending();

		// When
		report.EditSummary("First edit.", report.Summary!.AiSummaryFr, "subject-a", Later);
		report.EditSummary("Second edit.", report.Summary.AiSummaryFr, "subject-b", Latest);

		// Then
		var revisions = report.Summary.Revisions;
		revisions.Select(revision => revision.Sequence).ShouldBe([1, 2, 3]);
		revisions.Select(revision => revision.AiSummaryEn).ShouldBe(["The pilot landed.", "First edit.", "Second edit."]);
		revisions.Select(revision => revision.AuthorSubject).ShouldBe([null, "subject-a", "subject-b"]);
		revisions.Select(revision => revision.CreatedAt).ShouldBe([Now, Later, Latest]);
		report.Status.ShouldBe(ReportStatus.Pending);
		report.Summary.Latest.SourceEn.ShouldBe(SummaryTextSource.Human);
		report.Summary.Latest.SourceFr.ShouldBe(SummaryTextSource.Generated);
	}

	[Fact]
	public void GivenPublishedReport_WhenEdited_ThenTheEarlierApprovedRevisionKeepsItsApproval()
	{
		// Given
		var report = Pending();
		report.Publish("subject-approver", Now);

		// When
		report.EditSummary("Corrected text.", report.Summary!.AiSummaryFr, "subject-editor", Later);

		// Then — history records who approved each version
		var revisions = report.Summary.Revisions;
		revisions[0].ApprovedBySubject.ShouldBe("subject-approver");
		revisions[1].ApprovedBySubject.ShouldBe("subject-editor");
		report.PublishedAt.ShouldBe(Now);
	}

	[Fact]
	public void GivenEarlierRevision_WhenRolledBack_ThenANewRevisionCopiesItsTextAndSourcesAndNamesIt()
	{
		// Given
		var report = Pending();
		report.EditSummary("Edited text.", "Texte modifié.", "subject-a", Later, SummaryTextSource.Human, SummaryTextSource.Machine);
		var first = report.Summary!.Revisions[0];

		// When
		report.RollBackSummary(first.Id, "subject-b", Latest);

		// Then — versions only move forward: the old one is untouched, a new one equals it
		var revisions = report.Summary.Revisions;
		revisions.Count.ShouldBe(3);
		revisions[0].AiSummaryEn.ShouldBe("The pilot landed.");
		var restored = revisions[2];
		restored.Sequence.ShouldBe(3);
		restored.AiSummaryEn.ShouldBe(first.AiSummaryEn);
		restored.AiSummaryFr.ShouldBe(first.AiSummaryFr);
		restored.SourceEn.ShouldBe(SummaryTextSource.Generated);
		restored.SourceFr.ShouldBe(SummaryTextSource.Generated);
		restored.RestoredFromId.ShouldBe(first.Id);
		restored.AuthorSubject.ShouldBe("subject-b");
		report.Summary.Latest.ShouldBeSameAs(restored);
	}

	[Fact]
	public void GivenPublishedReport_WhenRolledBack_ThenTheRestoredRevisionIsApprovedAndPublishedAtOnce()
	{
		// Given
		var report = Pending();
		report.Publish("subject-approver", Now);
		report.EditSummary("Regretted text.", report.Summary!.AiSummaryFr, "subject-editor", Later);

		// When
		report.RollBackSummary(report.Summary.Revisions[0].Id, "subject-restorer", Latest);

		// Then
		report.Status.ShouldBe(ReportStatus.Published);
		report.PublishedAt.ShouldBe(Now);
		report.Summary.LatestApproved!.AiSummaryEn.ShouldBe("The pilot landed.");
		report.Summary.LatestApproved.ApprovedBySubject.ShouldBe("subject-restorer");
		report.IsPublishable.ShouldBeTrue();
	}

	[Fact]
	public void GivenPendingReport_WhenRolledBack_ThenTheRestoredRevisionIsADraftThatNeedsApproval()
	{
		// Given
		var report = Pending();
		report.EditSummary("Edited text.", report.Summary!.AiSummaryFr, "subject-a", Later);

		// When
		report.RollBackSummary(report.Summary.Revisions[0].Id, "subject-b", Latest);

		// Then
		report.Status.ShouldBe(ReportStatus.Pending);
		report.Summary.IsApproved.ShouldBeFalse();
		report.Summary.LatestApproved.ShouldBeNull();
		report.IsPublishable.ShouldBeFalse();
	}

	[Fact]
	public void GivenPendingDraft_WhenPublished_ThenTheLatestRevisionIsApproved()
	{
		// Given
		var report = Pending();
		report.EditSummary("Edited text.", report.Summary!.AiSummaryFr, "subject-a", Later);

		// When
		report.Publish("subject-approver", Latest);

		// Then
		report.Summary.Latest.ApprovedBySubject.ShouldBe("subject-approver");
		report.Summary.Revisions[0].IsApproved.ShouldBeFalse();
		report.IsPublishable.ShouldBeTrue();
	}

	[Fact]
	public void GivenLatestRevision_WhenRolledBackToItself_ThenRefused()
	{
		// Given
		var report = Pending();

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.RollBackSummary(report.Summary!.Latest.Id, "subject-a", Later));
		report.Summary!.Revisions.Count.ShouldBe(1);
	}

	[Fact]
	public void GivenUnknownRevision_WhenRolledBack_ThenRefused()
	{
		// Given
		var report = Pending();

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.RollBackSummary(TinyId.New(), "subject-a", Later));
	}

	[Fact]
	public void GivenReportWithoutSummary_WhenRolledBack_ThenRefused()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		report.BeginSummarizing();
		report.FailSummarization("The provider was unavailable.");

		// When / Then
		Should.Throw<ReviewTransitionException>(() => report.RollBackSummary(TinyId.New(), "subject-a", Later));
	}

	[Fact]
	public void GivenPublishedReport_WhenUnpublished_ThenTheLatestRevisionLosesItsApprovalAndNothingElseChanges()
	{
		// Given
		var report = Pending();
		report.Publish("subject-approver", Now);
		report.EditSummary("Corrected text.", report.Summary!.AiSummaryFr, "subject-editor", Later);

		// When
		report.Unpublish();

		// Then
		report.Summary.Latest.IsApproved.ShouldBeFalse();
		report.Summary.Revisions.Count.ShouldBe(2);
		report.IsPublishable.ShouldBeFalse();
	}

	[Fact]
	public void GivenSummaryWithRevisions_WhenReportIsSoftDeleted_ThenEveryRevisionIsStamped()
	{
		// Given
		var report = Pending();
		report.EditSummary("Edited text.", report.Summary!.AiSummaryFr, "subject-a", Later);

		// When
		report.SoftDelete(Latest);

		// Then
		report.Summary.Deleted.ShouldBe(Latest);
		report.Summary.Revisions.ShouldAllBe(revision => revision.Deleted == Latest);
	}

	[Fact]
	public void GivenReviewerClaimsGenerated_WhenRolledBack_ThenTheCopiedSourceIsKeptAsRecorded()
	{
		// Given — a rollback copies sources; it is not a claim by the reviewer
		var report = Pending();
		report.EditSummary("Edited.", report.Summary!.AiSummaryFr, "subject-a", Later);

		// When
		report.RollBackSummary(report.Summary.Revisions[0].Id, "subject-b", Latest);

		// Then
		report.Summary.Latest.SourceEn.ShouldBe(SummaryTextSource.Generated);
	}

	private static Report Pending()
	{
		var report = new Report(Locale.EnCa, Now);
		report.Answer(
			Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now),
			true,
			Now);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", Now));
		report.AwaitReview();
		return report;
	}
}
