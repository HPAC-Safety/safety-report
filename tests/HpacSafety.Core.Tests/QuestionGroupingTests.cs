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

	private static Question Ordinary(string key,
									 QuestionType type,
									 TinyId? groupedUnder = null)
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
		group.Delete(false, At.AddHours(1));

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

	[Fact]
	public void GivenChain_WhenWouldCloseIntoCycle_ThenRejected()
	{
		// Given — a shape only direct construction can produce: the no-nesting
		// rule stops the bank checker from ever creating this through the
		// ordinary API, but the checker still has to refuse it if bad data
		// ever reaches it, exactly as QuestionDependencies does for a
		// conditional cycle.
		var leaf = Ordinary("manufacturer", QuestionType.ShortText);
		var group = Group("aircraft");
		group.GroupUnder(leaf.Id, At.AddHours(1));

		// When / Then — grouping "leaf" under "group" would close the loop
		var cause = Should.Throw<DomainRuleViolationException>(() =>
			QuestionGrouping.EnsureGroupingAllowed([group, leaf], leaf.Id, group.Id));

		cause.Message.ShouldContain("cycle");
	}

	[Fact]
	public void GivenLongerChainDoesNotClose_WhenBankChecks_ThenAllowed()
	{
		// Given — group is (directly constructed) grouped under leaf, leaf
		// has nothing above it, and unrelated is a third, disconnected
		// question. Walking from group reaches leaf, never unrelated.
		var leaf = Ordinary("manufacturer", QuestionType.ShortText);
		var group = Group("aircraft");
		group.GroupUnder(leaf.Id, At.AddHours(1));
		var unrelated = Ordinary("model", QuestionType.ShortText);

		// When / Then
		Should.NotThrow(() =>
			QuestionGrouping.EnsureGroupingAllowed([group, leaf, unrelated], unrelated.Id, group.Id));
	}

	[Fact]
	public void GivenExistingCycleAmongOtherQuestions_WhenCheckedAgainstUnrelatedTarget_ThenAllowed()
	{
		// Given — two groups already (directly constructed) grouped under
		// each other, a shape only this construction can produce. Walking
		// from one must notice it has already visited both and stop, rather
		// than loop forever or wrongly report reaching an unrelated target.
		var groupA = Group("form");
		var groupB = Group("aircraft");
		groupA.GroupUnder(groupB.Id, At.AddHours(1));
		groupB.GroupUnder(groupA.Id, At.AddHours(1));
		var unrelated = Ordinary("manufacturer", QuestionType.ShortText);

		// When / Then
		Should.NotThrow(() =>
			QuestionGrouping.EnsureGroupingAllowed([groupA, groupB, unrelated], unrelated.Id, groupA.Id));
	}

	private static Question WithOrder(Question question,
								int order)
	{
		question.Reorder(order, At);
		return question;
	}

	[Fact]
	public void GivenDeletedGroup_WhenChildrenAreUngrouped_ThenTheyTakeItsSlotAndLaterQuestionsShiftDown()
	{
		// Given
		var before = WithOrder(Ordinary("before", QuestionType.ShortText), 0);
		var group = WithOrder(Group("aircraft"), 1);
		var first = WithOrder(Ordinary("first", QuestionType.ShortText, group.Id), 2);
		var second = WithOrder(Ordinary("second", QuestionType.ShortText, group.Id), 3);
		var after = WithOrder(Ordinary("after", QuestionType.ShortText), 4);
		group.Delete(false, At.AddHours(1));

		// When
		var result = QuestionGrouping.UngroupChildren([before, group, first, second, after], group, new HashSet<TinyId>(), At.AddHours(1));

		// Then
		result.Replacements.ShouldBeEmpty();
		result.Ungrouped.ShouldBe([first, second]);
		first.GroupedUnderQuestionId.ShouldBeNull();
		second.GroupedUnderQuestionId.ShouldBeNull();
		new[] { before, first, second, after }.Select(question => question.DisplayOrder).ShouldBe([0, 1, 2, 4]);
		result.Moved.ShouldBe(0);
	}

	[Fact]
	public void GivenChildrenOrderedFarFromTheirGroup_WhenUngrouped_ThenOnlyTheQuestionsTheyDisplaceShift()
	{
		// Given
		var group = WithOrder(Group("aircraft"), 0);
		var near = WithOrder(Ordinary("near", QuestionType.ShortText), 1);
		var first = WithOrder(Ordinary("first", QuestionType.ShortText, group.Id), 5);
		var second = WithOrder(Ordinary("second", QuestionType.ShortText, group.Id), 6);
		var far = WithOrder(Ordinary("far", QuestionType.ShortText), 20);
		group.Delete(false, At.AddHours(1));

		// When
		var result = QuestionGrouping.UngroupChildren([group, near, first, second, far], group, new HashSet<TinyId>(), At.AddHours(1));

		// Then
		new[] { first, second, near, far }.Select(question => question.DisplayOrder).ShouldBe([0, 1, 2, 20]);
		result.Moved.ShouldBe(1);
	}

	[Fact]
	public void GivenRetypedGroup_WhenChildrenAreUngrouped_ThenTheyFollowIt()
	{
		// Given
		var group = WithOrder(Group("aircraft"), 0);
		var second = WithOrder(Ordinary("second", QuestionType.ShortText, group.Id), 2);
		var first = WithOrder(Ordinary("first", QuestionType.ShortText, group.Id), 1);
		var after = WithOrder(Ordinary("after", QuestionType.ShortText), 3);
		group.Revise(QuestionType.ShortText, "Aircraft", "Aéronef", false, true, 0, At);

		// When
		var result = QuestionGrouping.UngroupChildren([group, second, first, after], group, new HashSet<TinyId>(), At.AddHours(1));

		// Then
		result.Ungrouped.ShouldBe([first, second]);
		new[] { group, first, second, after }.Select(question => question.DisplayOrder).ShouldBe([0, 1, 2, 3]);
	}

	[Fact]
	public void GivenAnsweredChild_WhenUngrouped_ThenItIsReplacedWithTheSameKey()
	{
		// Given
		var group = WithOrder(Group("aircraft"), 0);
		var answered = WithOrder(Ordinary("first", QuestionType.ShortText, group.Id), 1);
		var unanswered = WithOrder(Ordinary("second", QuestionType.ShortText, group.Id), 2);
		group.Delete(false, At.AddHours(1));

		// When
		var result = QuestionGrouping.UngroupChildren(
			[group, answered, unanswered], group, new HashSet<TinyId> { answered.Id }, At.AddHours(1));

		// Then
		var replacement = result.Replacements.ShouldHaveSingleItem();
		replacement.Key.ShouldBe("first");
		replacement.Id.ShouldNotBe(answered.Id);
		replacement.GroupedUnderQuestionId.ShouldBeNull();
		replacement.DisplayOrder.ShouldBe(0);
		answered.Deleted.ShouldNotBeNull();
		unanswered.Deleted.ShouldBeNull();
		unanswered.CurrentRevision.RevisionNumber.ShouldBe(3);
		result.Ungrouped.ShouldBe([replacement, unanswered]);
	}

	[Fact]
	public void GivenGroupWithNoChildren_WhenUngrouped_ThenNothingChanges()
	{
		// Given
		var group = WithOrder(Group("aircraft"), 0);
		var other = WithOrder(Ordinary("other", QuestionType.ShortText), 5);
		group.Delete(false, At.AddHours(1));

		// When
		var result = QuestionGrouping.UngroupChildren([group, other], group, new HashSet<TinyId>(), At.AddHours(1));

		// Then
		result.Ungrouped.ShouldBeEmpty();
		result.Moved.ShouldBe(0);
		other.DisplayOrder.ShouldBe(5);
	}
}
