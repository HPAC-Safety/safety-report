using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>Report soft deletion — REQ-DOM-007, REQ-DOM-003/004's deletion clause.</summary>
public class ReportSoftDeleteTests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenReportWithAnswersFilesAndSummary_WhenSoftDeleted_ThenEveryOwnedRowSharesOneTimestamp()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		var answer = report.Answer(ConsentQuestion(), true, Now);
		var file = report.AddFile("blob-key", "image/jpeg", 1024, Now);
		var summary = Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
		report.AttachSummary(summary);

		var deletedAt = Now.AddMinutes(5);

		// When
		report.SoftDelete(deletedAt);

		// Then
		report.Deleted.ShouldBe(deletedAt);
		answer.Deleted.ShouldBe(deletedAt);
		file.Deleted.ShouldBe(deletedAt);
		summary.Deleted.ShouldBe(deletedAt);
	}

	[Fact]
	public void GivenAnAlreadyDeletedReport_WhenSoftDeletedAgain_ThenTheOriginalTimestampIsKept()
	{
		// Given — irreversible, and idempotent: no second act of deletion
		var report = new Report(Locale.EnCa, Now);
		report.SoftDelete(Now);

		// When
		report.SoftDelete(Now.AddDays(1));

		// Then
		report.Deleted.ShouldBe(Now);
	}

	[Fact]
	public void GivenPendingReport_WhenSoftDeleted_ThenItCanNeverBePublished()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		report.Answer(ConsentQuestion(), true, Now);
		AwaitReviewWithPair(report);

		// When
		report.SoftDelete(Now);

		// Then
		report.IsPublishable.ShouldBeFalse();
		Should.Throw<DomainRuleViolationException>(() => report.Publish("subject-officer", Now));
	}

	[Fact]
	public void GivenAPublishedReport_WhenSoftDeleted_ThenItIsNoLongerPublishable()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		report.Answer(ConsentQuestion(), true, Now);
		AwaitReviewWithPair(report);
		report.Publish("subject-officer", Now);
		report.IsPublishable.ShouldBeTrue();

		// When
		report.SoftDelete(Now.AddMinutes(1));

		// Then — the public query re-evaluates IsPublishable; Status alone never gates it
		report.IsPublishable.ShouldBeFalse();
	}

	private static void AwaitReviewWithPair(Report report)
	{
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now));
		report.AwaitReview();
	}

	private static Question ConsentQuestion()
	{
		return Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now);
	}
}
