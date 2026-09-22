namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     The rule that no single question can check on its own: whether the
///     question it names as a heading actually is one.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="QuestionRevision" /> checks everything visible from inside one
///         revision — that a grouping is not self-referential. It cannot check
///         the named question's <i>current type</i>, which lives on that
///         question's current revision, a different row. Nor can it see a
///         cycle. Both are checked here, against the live bank, the same split
///         <see cref="QuestionDependencies" /> uses for a conditional
///         dependency — a deliberately separate mechanism, since "display
///         together" is not "conditional on." See ADR-0076.
///     </para>
///     <para>
///         This is a static rule-checker over a collection, not a repository. It does
///         no I/O — the caller loads the live questions and hands them in.
///     </para>
/// </remarks>
public static class QuestionGrouping
{
	/// <summary>
	///     Checks that a question may be grouped under the one it names: the
	///     named question exists, is live, is currently a
	///     <see cref="QuestionType.Group" />, and does not lead back to the
	///     child through a chain of groupings.
	/// </summary>
	/// <param name="questions">Every live question, including the child if it already exists.</param>
	/// <param name="childId">The question being grouped, or null when it is being created.</param>
	/// <param name="groupId">The group question it is to be displayed under.</param>
	/// <exception cref="DomainRuleViolationException">When the grouping is not allowed.</exception>
	public static void EnsureGroupingAllowed(IReadOnlyCollection<Question> questions, TinyId? childId, TinyId groupId)
	{
		ArgumentNullException.ThrowIfNull(questions);

		if (childId == groupId) throw new DomainRuleViolationException("A question cannot be grouped under itself.");

		var group = questions.FirstOrDefault(question => question.Id == groupId && question.Deleted is null)
					?? throw new DomainRuleViolationException(
						"That question no longer exists, so nothing can be grouped under it.");

		if (group.Type != QuestionType.Group)
			throw new DomainRuleViolationException(
				$"'{group.Key}' is a {EnumCode.Of(group.Type)} question. Only a group question can have other questions displayed under it.");

		var child = childId is { } id ? questions.FirstOrDefault(question => question.Id == id) : null;

		if (child?.Type == QuestionType.Group)
			throw new DomainRuleViolationException(
				$"'{child.Key}' is a group question and cannot itself be grouped under another one.");

		if (childId is { } childValue && LeadsTo(questions, groupId, childValue))
			throw new DomainRuleViolationException(
				$"'{group.Key}' is already grouped under this question, directly or through another one. A cycle would leave both unable to render.");
	}

	/// <summary>
	///     Whether following <see cref="Question.GroupedUnderQuestionId" /> from
	///     <paramref name="from" /> reaches <paramref name="target" />. The
	///     visited set is what stops a cycle already in the data from looping
	///     here. In practice this only ever finds a cycle of length one, since a
	///     <see cref="QuestionType.Group" /> cannot itself be grouped under
	///     another group — but the walk costs nothing extra to make general.
	/// </summary>
	private static bool LeadsTo(IReadOnlyCollection<Question> questions, TinyId from, TinyId target)
	{
		var visited = new HashSet<TinyId>();
		var current = from;

		while (visited.Add(current))
		{
			var question = questions.FirstOrDefault(candidate => candidate.Id == current);

			if (question?.GroupedUnderQuestionId is not { } next) return false;

			if (next == target) return true;

			current = next;
		}

		return false;
	}
}
