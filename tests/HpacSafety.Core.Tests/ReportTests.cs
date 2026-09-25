using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Consent gates publication, and it is the only answer a report reads by name.
/// </summary>
public class ReportTests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenReportWithoutConsent_WhenPublished_ThenRefusedAndNotPublishable()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		report.Answer(ConsentQuestion(), ["no"], Now);
		AwaitReviewWithPair(report);

		// When
		var publishing = () => report.Publish("subject-officer", Now);

		// Then — kept internally; never published
		publishing.ShouldThrow<DomainRuleViolationException>();
		report.ConsentPublish.ShouldBe(false);
		report.IsPublishable.ShouldBeFalse();
	}

	[Fact]
	public void GivenConsentIsUnanswered_WhenSubmissionIsAttempted_ThenRefused()
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
	public void GivenConsentIsAnsweredNo_WhenSubmissionIsChecked_ThenAllowed()
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
	public void GivenUnreadableConsentAnswer_WhenRecorded_ThenRefused()
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
	public void GivenConsentAndApprovedSummary_WhenPublicationIsAttempted_ThenSucceeds()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		report.Answer(ConsentQuestion(), ["yes"], Now);
		var officer = "subject-officer";

		AwaitReviewWithPair(report);

		// When
		report.Publish(officer, Now);

		// Then
		report.Summary!.ApprovedBySubject.ShouldBe(officer);
		report.Status.ShouldBe(ReportStatus.Published);
		report.PublishedAt.ShouldBe(Now);
	}

	[Fact]
	public void GivenPendingPairNobodyPublished_WhenPublishabilityIsChecked_ThenNotPublishable()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		report.Answer(ConsentQuestion(), ["yes"], Now);

		// When
		AwaitReviewWithPair(report);

		// Then — the human gate covers everything published
		report.Summary!.IsApproved.ShouldBeFalse();
		report.IsPublishable.ShouldBeFalse();
	}

	private static void AwaitReviewWithPair(Report report)
	{
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now));
		report.AwaitReview();
	}

	[Fact]
	public void GivenAnswer_WhenRecorded_ThenReferencesRevisionAskedUnder()
	{
		// Given
		var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);
		var askedUnder = question.CurrentRevision;
		var report = new Report(Locale.EnCa, Now);

		// When
		var answer = report.Answer(question, "Broken riser", Now);
		question.Revise(
			QuestionType.LongText, "Describe the damage", "Décrivez les dommages",
			question.IsPrivate, question.IsActive, question.DisplayOrder, Now.AddDays(1));

		// Then — rewording tomorrow cannot change what an answer given today means
		answer.QuestionRevisionId.ShouldBe(askedUnder.Id);
		answer.QuestionRevisionId.ShouldNotBe(question.CurrentRevision.Id);
	}

	[Fact]
	public void GivenPrivateQuestion_WhenAnswered_ThenAnswerSnapshotsPrivateClassification()
	{
		// Given
		var question = Question.Create("where", QuestionType.ShortText, "Where?", "Où ?", Now);
		var report = new Report(Locale.EnCa, Now);
		var answer = report.Answer(question, "A launch site", Now);

		// When / Then — the answer remains self-describing even though question privacy is immutable
		answer.IsPrivate.ShouldBeTrue();
	}

	[Fact]
	public void GivenUnknownOptionCode_WhenAnswered_ThenRefused()
	{
		// Given
		var question = Question.Create(
			"time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now,
			options: [new QuestionOptionInput("morning", "Morning", "Matin")]);
		var report = new Report(Locale.EnCa, Now);

		// When
		var answering = () => report.Answer(question, ["midnight"], Now);

		// Then
		answering.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenSummarizationFails_WhenFailureIsRecorded_ThenReportStillReachesHuman()
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

	private static Question ConsentQuestion()
	{
		return Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now);
	}
}
