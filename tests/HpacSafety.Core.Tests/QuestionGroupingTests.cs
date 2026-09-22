using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     Statement and group question types, and grouping a question under a
///     group heading (ADR-0076).
/// </summary>
public class QuestionGroupingTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

	private static Question Ordinary(string key, QuestionType type, TinyId? groupedUnder = null)
	{
		return Question.Create(
			key, type, $"Question {key}", $"Question {key} (fr)", At, isActive: true, isPrivate: false,
			groupedUnderQuestionId: groupedUnder);
	}

	private static Question Group(string key)
	{
		return Ordinary(key, QuestionType.Group);
	}

	[Theory]
	[InlineData(QuestionType.Statement)]
	[InlineData(QuestionType.Group)]
	public void GivenNoAnswerType_WhenQuestionIsCreated_ThenCollectsNoAnswer(QuestionType type)
	{
		// Given / When
		var question = Ordinary("heading", type);

		// Then
		question.CurrentRevision.CollectsNoAnswer.ShouldBeTrue();
	}

	[Theory]
	[InlineData(QuestionType.Statement)]
	[InlineData(QuestionType.Group)]
	public void GivenNoAnswerType_WhenAuthoredRequired_ThenRejected(QuestionType type)
	{
		// Given / When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			Question.Create("heading", type, "Heading", "Titre", At, isRequired: true));
	}

	[Theory]
	[InlineData(QuestionType.Statement)]
	[InlineData(QuestionType.Group)]
	public void GivenNoAnswerType_WhenAuthoredPrivate_ThenRejected(QuestionType type)
	{
		// Given / When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			Question.Create("heading", type, "Heading", "Titre", At, isPrivate: true));
	}

	[Theory]
	[InlineData(QuestionType.Statement)]
	[InlineData(QuestionType.Group)]
	public void GivenNoAnswerType_WhenMadeConditional_ThenRejected(QuestionType type)
	{
		// Given
		var parent = Ordinary("were_you_injured", QuestionType.YesNo);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			Question.Create("heading", type, "Heading", "Titre", At, dependsOnQuestionId: parent.Id));
	}

	[Fact]
	public void GivenNoAnswerType_WhenNamedAsTheConditionForAnother_ThenRejected()
	{
		// Given — a statement/group cannot even reach the switch in
		// QuestionDependencies; the default case rejects any non-yes/no,
		// non-single-select parent, this one included.
		var heading = Group("heading");

		// When / Then
		var cause = Should.Throw<DomainRuleViolationException>(() =>
			QuestionDependencies.EnsureDependencyAllowed([heading], null, heading.Id));

		cause.Message.ShouldContain("yes/no or single-select");
	}

	[Fact]
	public void GivenGroupQuestion_WhenChildNamesIt_ThenChildRecordsGrouping()
	{
		// Given
		var group = Group("aircraft");

		// When
		var child = Ordinary("manufacturer", QuestionType.ShortText, group.Id);

		// Then
		child.GroupedUnderQuestionId.ShouldBe(group.Id);
		child.CurrentRevision.GroupedUnderQuestionId.ShouldBe(group.Id);
	}

	[Fact]
	public void GivenQuestion_WhenGroupedUnderItself_ThenRejected()
	{
		// Given
		var question = Group("aircraft");

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => question.GroupUnder(question.Id, At.AddHours(1)));
	}

	[Fact]
	public void GivenGroupedQuestion_WhenGroupingIsCleared_ThenNewRevisionRecords()
	{
		// Given
		var group = Group("aircraft");
		var child = Ordinary("manufacturer", QuestionType.ShortText, group.Id);

		// When
		child.GroupUnder(null, At.AddHours(1));

		// Then
		child.GroupedUnderQuestionId.ShouldBeNull();
		child.CurrentRevision.RevisionNumber.ShouldBe(2);
	}

	[Fact]
	public void GivenNonGroupParent_WhenBankChecksGrouping_ThenRejected()
	{
		// Given
		var parent = Ordinary("manufacturer", QuestionType.ShortText);

		// When / Then
		var cause = Should.Throw<DomainRuleViolationException>(() =>
			QuestionGrouping.EnsureGroupingAllowed([parent], null, parent.Id));

		cause.Message.ShouldContain("group question");
	}

	[Fact]
	public void GivenGroupParentDoesNotExist_WhenBankChecksGrouping_ThenRejected()
	{
		// Given
		var group = Group("aircraft");

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionGrouping.EnsureGroupingAllowed([], null, group.Id));
	}

	[Fact]
	public void GivenDeletedGroupParent_WhenBankChecksGrouping_ThenRejected()
	{
		// Given
		var group = Group("aircraft");
		group.Delete(At.AddHours(1));

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionGrouping.EnsureGroupingAllowed([group], null, group.Id));
	}

	[Fact]
	public void GivenQuestionIsOwnGroupParent_WhenBankChecks_ThenRejected()
	{
		// Given
		var group = Group("aircraft");

		// When / Then
		Should.Throw<DomainRuleViolationException>(() =>
			QuestionGrouping.EnsureGroupingAllowed([group], group.Id, group.Id));
	}

	[Fact]
	public void GivenTwoGroups_WhenOneIsGroupedUnderTheOther_ThenRejected()
	{
		// Given — no nesting: a group cannot itself be grouped under another one
		var outer = Group("form");
		var inner = Group("aircraft");

		// When / Then
		var cause = Should.Throw<DomainRuleViolationException>(() =>
			QuestionGrouping.EnsureGroupingAllowed([outer, inner], inner.Id, outer.Id));

		cause.Message.ShouldContain("cannot itself be grouped");
	}

	[Fact]
	public void GivenOrdinaryQuestion_WhenGroupedUnderAGroup_ThenAllowed()
	{
		// Given
		var group = Group("aircraft");
		var child = Ordinary("manufacturer", QuestionType.ShortText);

		// When / Then
		Should.NotThrow(() => QuestionGrouping.EnsureGroupingAllowed([group, child], child.Id, group.Id));
	}
}
