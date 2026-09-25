using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Publication consent is the only answer read by name. Every other question,
///     whatever role an administrator assigns it, is simply recorded — the admin
///     review DTO reads exact asked questions and answers directly.
/// </summary>
public class ProjectionTests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenOrdinaryQuestion_WhenAnswered_ThenAnswerIsSimplyRecorded()
	{
		// Given
		var question = Question.Create("description", QuestionType.LongText, "Describe it", "Décrivez-le", Now);
		var report = new Report(Locale.EnCa, Now);

		// When
		report.Answer(question, "The wing collapsed on approach.", Now);

		// Then
		report.Answers.Count.ShouldBe(1);
		report.ConsentPublish.ShouldBeNull();
	}

	[Theory]
	[InlineData("yes", true)]
	[InlineData("no", false)]
	public void GivenConsentRoleOnTextQuestion_WhenAnswered_ThenBooleanWordIsRead(string given,
																				  bool expected)
	{
		// Given — "yes" and "no" are the invariant stored forms of every
		// boolean answer (ADR-0072); the role can be moved to a question that
		// is not the YesNo one
		var question = Question.Create(
			"consent", QuestionType.ShortText, "May we publish?", "Pouvons-nous publier ?", Now,
			role: QuestionRole.ConsentPublish);
		var report = new Report(Locale.EnCa, Now);

		// When
		report.Answer(question, given, Now);

		// Then
		report.ConsentPublish.ShouldBe(expected);
	}

	[Fact]
	public void GivenFreeTextQuestion_WhenAnsweredWithSeveralValues_ThenRefused()
	{
		// Given — multi-select is the only type that records more than one row
		var question = Question.Create("description", QuestionType.LongText, "Describe it", "Décrivez-le", Now);
		var report = new Report(Locale.EnCa, Now);

		// When
		var answering = () => report.Answer(question, ["something", "something else"], Now);

		// Then
		answering.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenReport_WhenRecordIsRead_ThenAnswersFilesAndSummaryAreAll()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);

		// When
		report.Answer(question, "A broken riser", Now);
		report.AddFile("kJQP7kiw5Fk/original/clip.mp4", "video/mp4", 4096, Now);
		report.AttachSummary(Summary.Generate(report.Id, "A hang glider landed short.", "Un deltaplane a atterri court.", "model", "v1", Now));

		// Then
		report.Answers.Count.ShouldBe(1);
		report.Files.Count.ShouldBe(1);
		report.Summary.ShouldNotBeNull();
	}

	[Fact]
	public void GivenOrdinaryQuestion_WhenDeactivated_ThenStopsBeingAsked()
	{
		// Given
		var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);
		question.Activate(Now);

		// When
		question.Deactivate(Now.AddDays(1));

		// Then — every answer already given to it survives
		question.IsActive.ShouldBeFalse();
		question.Deleted.ShouldBeNull();
	}

	[Fact]
	public void GivenQuestion_WhenChoicesAreRead_ThenExposedInBothLanguages()
	{
		// Given
		var question = Question.Create(
			"time_of_day", QuestionType.SingleSelect, "Time of day", "Moment de la journée", Now,
			options: [new QuestionOptionInput("morning", "Morning", "Matin")]);

		// When
		var option = question.Choice("morning")!;

		// Then
		question.Choices.Count.ShouldBe(1);
		option.LabelEn.ShouldBe("Morning");
		option.LabelFr.ShouldBe("Matin");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenNoCodeAtAll_WhenParsed_ThenNothingIsGuessed(string? code)
	{
		// Given / When
		var parsed = EnumCode.TryParse<ReportStatus>(code, out var status);

		// Then
		parsed.ShouldBeFalse();
		status.ShouldBe(ReportStatus.Submitted);
	}

	[Fact]
	public void GivenConsentWasNeverAnswered_WhenPublicationIsAttempted_ThenRefusalSaysSo()
	{
		// Given — the strongest form of the gate: nobody said no, and nobody said yes
		var report = new Report(Locale.EnCa, Now);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now));
		report.AwaitReview();

		// When
		var publishing = () => report.Publish("subject-officer", Now);

		// Then
		publishing.ShouldThrow<DomainRuleViolationException>()
			.Message.ShouldContain("unanswered consent is not a consent");
	}

	[Fact]
	public void GivenConsentRoleOnTextQuestion_WhenAnswerIsNo_ThenConsentIsRefused()
	{
		// Given
		var question = Question.Create(
			"consent", QuestionType.ShortText, "May we publish?", "Pouvons-nous publier ?", Now,
			role: QuestionRole.ConsentPublish);
		var report = new Report(Locale.EnCa, Now);

		// When
		report.Answer(question, "no", Now);

		// Then
		report.ConsentPublish.ShouldBe(false);
		report.HasAnsweredConsent.ShouldBeTrue();
	}

	[Fact]
	public void GivenConsentAndApprovalButNoSummary_WhenPublicationIsAttempted_ThenBlocked()
	{
		// Given
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);
		var report = new Report(Locale.EnCa, Now);
		report.Answer(consent, ["yes"], Now);

		// When
		var publishing = () => report.Publish("subject-officer", Now);

		// Then — there is nothing anonymized to publish yet
		report.IsPublishable.ShouldBeFalse();
		publishing.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenOrdinaryQuestion_WhenTypeChanges_ThenAllowed()
	{
		// Given — only the consent question has a locked type
		var question = Question.Create("damage", QuestionType.ShortText, "Damage", "Dommages", Now);

		// When
		var revised = question.Revise(
			QuestionType.LongText, "Describe the damage", "Décrivez les dommages",
			question.IsPrivate, question.IsActive, question.DisplayOrder, Now);

		// Then — invariant #1: an ordinary question is never required, no
		// matter what an earlier revision or caller asks for
		revised.Type.ShouldBe(QuestionType.LongText);
		revised.IsRequired.ShouldBeFalse();
	}

	[Fact]
	public void GivenConsentQuestion_WhenRewordedAtSameType_ThenAllowed()
	{
		// Given
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

		// When
		var revised = consent.Revise(
			QuestionType.YesNo,
			"Do you agree to HPAC publishing a de-identified version?",
			"Acceptez-vous que l'ACVL publie une version anonymisée ?",
			consent.IsPrivate, consent.IsActive, consent.DisplayOrder, Now.AddDays(1));

		// Then
		revised.RevisionNumber.ShouldBe(2);
		revised.IsRequired.ShouldBeTrue();
		consent.Type.ShouldBe(QuestionType.YesNo);
	}

	[Fact]
	public void GivenConsentRoleOnTextQuestion_WhenAnswerIsNeitherYesNorNo_ThenRefused()
	{
		// Given
		var question = Question.Create(
			"consent", QuestionType.ShortText, "May we publish?", "Pouvons-nous publier ?", Now,
			role: QuestionRole.ConsentPublish);
		var report = new Report(Locale.EnCa, Now);

		// When
		var answering = () => report.Answer(question, "maybe later", Now);

		// Then — an unreadable consent is an error, never a quiet no
		answering.ShouldThrow<DomainRuleViolationException>()
			.Message.ShouldContain("no default and no third state");
		report.ConsentPublish.ShouldBeNull();
	}

	[Fact]
	public void GivenConsentAndApprovedSummaryButNoOfficerApproval_WhenPublishabilityIsChecked_ThenFalse()
	{
		// Given — the human gate is separate from the consent gate
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);
		var report = new Report(Locale.EnCa, Now);
		report.Answer(consent, ["yes"], Now);

		var summary = Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now);
		summary.Approve("subject-officer", Now);
		report.AttachSummary(summary);

		// When
		var publishable = report.IsPublishable;

		// Then
		report.Status.ShouldBe(ReportStatus.Submitted);
		publishable.ShouldBeFalse();
	}

	[Fact]
	public void GivenPublishedReport_WhenPublishabilityIsRechecked_ThenStillPublishable()
	{
		// Given
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);
		var report = new Report(Locale.EnCa, Now);
		report.Answer(consent, ["yes"], Now);

		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "A pilot landed hard.", "Un pilote a atterri durement.", "model", "v1", Now));
		report.AwaitReview();
		report.Publish("subject-officer", Now);

		// When
		var publishable = report.IsPublishable;

		// Then — publishing does not invalidate the state that allowed it
		publishable.ShouldBeTrue();
	}

	[Fact]
	public void GivenDomainRuleViolation_WhenCarriesCause_ThenCauseIsKept()
	{
		// Given
		var cause = new InvalidOperationException("the underlying problem");

		// When
		var exception = new DomainRuleViolationException("the rule that was broken", cause);

		// Then
		exception.Message.ShouldBe("the rule that was broken");
		exception.InnerException.ShouldBe(cause);
		new DomainRuleViolationException().Message.ShouldNotBeNullOrEmpty();
	}
}
