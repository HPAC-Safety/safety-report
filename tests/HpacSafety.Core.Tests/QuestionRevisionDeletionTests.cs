using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Deleting one revision out of a question's history — distinct from
///     <see cref="Question.Delete" />, which retires the whole question. See
///     REQ-DOM-008.
/// </summary>
public class QuestionRevisionDeletionTests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenASupersededUnreferencedRevision_WhenDeleted_ThenStampedWithOneTimestamp()
	{
		// Given
		var question = Question.Create(
			"heading", QuestionType.SingleSelect, "Heading", "Cap", Now,
			options: [new QuestionOptionInput("north", "North", "Nord")]);
		var oldRevision = question.CurrentRevision;
		question.Revise(
			QuestionType.SingleSelect, "Heading (revised)", "Cap (révisé)", true, true, 0, Now.AddHours(1),
			options: [new QuestionOptionInput("north", "North", "Nord")]);

		// When
		var at = Now.AddHours(2);
		question.DeleteRevision(oldRevision.Id, hasBeenAnswered: false, at);

		// Then
		oldRevision.Deleted.ShouldBe(at);
		oldRevision.Options.ShouldAllBe(option => option.Deleted == at);
		question.CurrentRevision.Deleted.ShouldBeNull();
	}

	[Fact]
	public void GivenTheCurrentRevision_WhenDeleted_ThenRefused()
	{
		// Given — a question has exactly one revision at birth, and it is current
		var question = Question.Create("heading", QuestionType.ShortText, "Heading", "Cap", Now);

		// When
		var deleting = () => question.DeleteRevision(question.CurrentRevision.Id, hasBeenAnswered: false, Now.AddHours(1));

		// Then
		deleting.ShouldThrow<DomainRuleViolationException>();
		question.CurrentRevision.Deleted.ShouldBeNull();
	}

	[Fact]
	public void GivenARevisionAnswerReferences_WhenDeleted_ThenRefused()
	{
		// Given
		var question = Question.Create("heading", QuestionType.ShortText, "Heading", "Cap", Now);
		var oldRevision = question.CurrentRevision;
		question.Revise(QuestionType.ShortText, "Heading (revised)", "Cap (révisé)", true, true, 0, Now.AddHours(1));

		// When
		var deleting = () => question.DeleteRevision(oldRevision.Id, hasBeenAnswered: true, Now.AddHours(2));

		// Then
		deleting.ShouldThrow<DomainRuleViolationException>();
		oldRevision.Deleted.ShouldBeNull();
	}

	[Fact]
	public void GivenARevisionAlreadyDeleted_WhenDeletedAgain_ThenTimestampDoesNotMove()
	{
		// Given — with an option, so the cascade's own idempotency is proven too
		var question = Question.Create(
			"heading", QuestionType.SingleSelect, "Heading", "Cap", Now,
			options: [new QuestionOptionInput("north", "North", "Nord")]);
		var oldRevision = question.CurrentRevision;
		var oldOption = oldRevision.Options.Single();
		question.Revise(
			QuestionType.SingleSelect, "Heading (revised)", "Cap (révisé)", true, true, 0, Now.AddHours(1),
			options: [new QuestionOptionInput("north", "North", "Nord")]);
		var firstDeletion = Now.AddHours(2);
		question.DeleteRevision(oldRevision.Id, hasBeenAnswered: false, firstDeletion);

		// When
		question.DeleteRevision(oldRevision.Id, hasBeenAnswered: false, Now.AddHours(3));

		// Then
		oldRevision.Deleted.ShouldBe(firstDeletion);
		oldOption.Deleted.ShouldBe(firstDeletion);
	}

	[Fact]
	public void GivenARevisionIdThatDoesNotBelongToThisQuestion_WhenDeleted_ThenRefused()
	{
		// Given
		var questionA = Question.Create("heading", QuestionType.ShortText, "Heading", "Cap", Now);
		var questionB = Question.Create("weather", QuestionType.ShortText, "Weather", "Météo", Now);

		// When
		var deleting = () =>
			questionA.DeleteRevision(questionB.CurrentRevision.Id, hasBeenAnswered: false, Now.AddHours(1));

		// Then
		deleting.ShouldThrow<DomainRuleViolationException>();
	}
}
