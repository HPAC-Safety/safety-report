using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Media consent is the second system question and the second answer read by
///     name (ADR-0117): it projects onto the report, is never optional, and can
///     never be removed, retyped, made conditional, or given another role.
/// </summary>
public class MediaConsentTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenMediaConsentQuestion_WhenCreated_ThenItIsARequiredSystemYesNo()
	{
		// When
		var question = MediaConsent();

		// Then
		question.Key.ShouldBe(QuestionKey.ConsentMedia);
		question.Role.ShouldBe(QuestionRole.ConsentMedia);
		question.IsSystem.ShouldBeTrue();
		question.IsRequired.ShouldBeTrue();
		question.CurrentRevision.Type.ShouldBe(QuestionType.YesNo);
		question.DependsOnQuestionId.ShouldBeNull();
	}

	[Theory]
	[InlineData("yes", true)]
	[InlineData("no", false)]
	public void GivenMediaConsentAnswer_WhenAnswered_ThenItProjectsOntoReport(string given,
																		   bool expected)
	{
		// Given
		var report = new Report(Locale.EnCa, Now);

		// When
		report.Answer(MediaConsent(), given, Now);

		// Then
		report.ConsentMedia.ShouldBe(expected);
		report.ConsentPublish.ShouldBeNull();
	}

	[Fact]
	public void GivenUnansweredMediaConsent_WhenReportIsRead_ThenItIsNotConsent()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);

		// When
		report.Answer(Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now), "yes", Now);

		// Then
		report.ConsentMedia.ShouldBeNull();
	}

	[Fact]
	public void GivenUnreadableMediaConsent_WhenAnswered_ThenRefused()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.Answer(MediaConsent(), "maybe", Now));
	}

	[Fact]
	public void GivenMediaConsentRoleOnTextQuestion_WhenAnsweredWithAnythingElse_ThenRefusedByName()
	{
		// Given — the role is what is read, whichever question carries it
		var question = Question.Create("media_ok", QuestionType.ShortText, "Media?", "Médias ?", Now, role: QuestionRole.ConsentMedia);
		var report = new Report(Locale.EnCa, Now);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => report.Answer(question, "sure", Now))
			.Message.ShouldContain("Media consent");
	}

	[Fact]
	public void GivenMediaConsentQuestion_WhenRemovedRetypedOrRekeyed_ThenEveryAttemptIsRefused()
	{
		// Given
		var question = MediaConsent();

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => question.Delete(false, Now));
		Should.Throw<DomainRuleViolationException>(() => question.Deactivate(Now));
		Should.Throw<DomainRuleViolationException>(() => question.AssignRole(QuestionRole.None));
		Should.Throw<DomainRuleViolationException>(() =>
			question.Revise(QuestionType.ShortText, "Label", "Libellé", true, true, 0, Now));
		question.ForksWhenEdited(hasBeenAnswered: true).ShouldBeFalse();
	}

	[Fact]
	public void GivenMediaConsentQuestion_WhenItsRoleIsReassignedToItself_ThenAccepted()
	{
		// Given
		var question = MediaConsent();

		// When
		question.AssignRole(QuestionRole.ConsentMedia);

		// Then
		question.Role.ShouldBe(QuestionRole.ConsentMedia);
	}

	private static Question MediaConsent()
	{
		return Question.CreateConsentMedia("May we show your photos?", "Pouvons-nous montrer vos photos ?", Now);
	}
}
