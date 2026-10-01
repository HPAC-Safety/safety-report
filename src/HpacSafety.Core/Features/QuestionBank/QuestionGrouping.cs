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
	public static void EnsureGroupingAllowed(IReadOnlyCollection<Question> questions,
											 TinyId? childId,
											 TinyId groupId)
	{
		ArgumentNullException.ThrowIfNull(questions);

		if (childId == groupId)
		{
			throw new DomainRuleViolationException("A question cannot be grouped under itself.");
		}

		var group = questions.FirstOrDefault(question => question.Id == groupId && question.Deleted is null)
					?? throw new DomainRuleViolationException(
						"That question no longer exists, so nothing can be grouped under it.");

		if (group.Type != QuestionType.Group)
		{
			throw new DomainRuleViolationException(
				$"'{group.Key}' is a {EnumCode.Of(group.Type)} question. Only a group question can have other questions displayed under it.");
		}

		var child = childId is { } id ? questions.FirstOrDefault(question => question.Id == id) : null;

		if (child?.Type == QuestionType.Group)
		{
			throw new DomainRuleViolationException(
				$"'{child.Key}' is a group question and cannot itself be grouped under another one.");
		}

		if (childId is { } childValue
			&& LeadsTo(questions, groupId, childValue))
		{
			throw new DomainRuleViolationException(
				$"'{group.Key}' is already grouped under this question, directly or through another one. A cycle would leave both unable to render.");
		}
	}

	/// <summary>
	///     Ungroups every live question displayed under <paramref name="group" />
	///     because it was just deleted or retyped away from
	///     <see cref="QuestionType.Group" />, and places them in the form where it
	///     stood (REQ-QB-052).
	/// </summary>
	/// <remarks>
	///     <para>
	///         The children keep their order within the group and take the group's slot,
	///         every later question shifting down only as far as it must. A group that
	///         was deleted leaves the slot; one that was retyped keeps it, and its
	///         children follow it. An
	///         answered child forks and an unanswered one is revised, like any edit
	///         (ADR-0071). Whether a child has been answered is a fact about reports, so
	///         the caller reads it and passes it in.
	///     </para>
	///     <para>
	///         Call it after the group was deleted or retyped. Grouping is display
	///         metadata, so an ungrouped child's own condition and choice dependencies
	///         are untouched; the caller re-points the ones a fork leaves behind.
	///     </para>
	/// </remarks>
	/// <param name="questions">Every live question, and the group even if it was just retired.</param>
	/// <param name="group">The group that stopped being one.</param>
	/// <param name="answered">Ids of the questions any answer references.</param>
	/// <param name="at">When the change happened.</param>
	public static UngroupResult UngroupChildren(IReadOnlyCollection<Question> questions,
												Question group,
												IReadOnlySet<TinyId> answered,
												DateTimeOffset at)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(answered);

		var form = questions
			.Where(question => question.Deleted is null || question.Id == group.Id)
			.OrderBy(question => question.DisplayOrder)
			.ThenBy(question => question.Key, StringComparer.Ordinal)
			.ToList();

		var children = form.Where(question => question.Deleted is null && question.GroupedUnderQuestionId == group.Id).ToList();

		if (children.Count == 0)
		{
			return new UngroupResult([], [], 0);
		}

		var arrangement = new List<Question>(form.Count);

		foreach (var question in form.Where(question => !children.Contains(question)))
		{
			if (question.Deleted is null)
			{
				arrangement.Add(question);
			}

			if (question.Id == group.Id)
			{
				arrangement.AddRange(children);
			}
		}

		List<Question> ungrouped = [];
		List<Question> replacements = [];
		var moved = 0;
		var last = -1;
		var shifting = false;

		// Questions before the children's slot keep their order. After it, a question
		// is shifted only while it sits at or before the one ahead of it; the first
		// that clears the children ends the shifting, so nothing unrelated is revised.
		foreach (var question in arrangement)
		{
			if (children.Contains(question))
			{
				shifting = true;
				last++;
				var live = question.Ungroup(answered.Contains(question.Id), last, at);
				ungrouped.Add(live);

				if (!ReferenceEquals(live, question))
				{
					replacements.Add(live);
				}
			}
			else if (shifting && question.DisplayOrder <= last)
			{
				last++;
				question.Reorder(last, at);
				moved++;
			}
			else
			{
				shifting = false;
				last = Math.Max(last, question.DisplayOrder);
			}
		}

		return new UngroupResult(ungrouped, replacements, moved);
	}

	/// <summary>
	///     Gives every live question displayed under <paramref name="group" /> a new
	///     revision, still grouped under it, because the group was edited and stayed
	///     one (REQ-QB-248). An answered child forks and an unanswered one is revised,
	///     like any edit (ADR-0071); the form order is untouched.
	/// </summary>
	/// <param name="questions">Every live question.</param>
	/// <param name="group">The group that was edited.</param>
	/// <param name="answered">Ids of the questions any answer references.</param>
	/// <param name="at">When the change happened.</param>
	public static UngroupResult ReviseChildren(IReadOnlyCollection<Question> questions,
											   Question group,
											   IReadOnlySet<TinyId> answered,
											   DateTimeOffset at)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(answered);

		List<Question> revised = [];
		List<Question> replacements = [];

		foreach (var child in questions.Where(question => question.Deleted is null && question.GroupedUnderQuestionId == group.Id))
		{
			var live = child.ReviseWithGroup(answered.Contains(child.Id), at);
			revised.Add(live);

			if (!ReferenceEquals(live, child))
			{
				replacements.Add(live);
			}
		}

		return new UngroupResult(revised, replacements, 0);
	}

	/// <summary>
	///     Whether following <see cref="Question.GroupedUnderQuestionId" /> from
	///     <paramref name="from" /> reaches <paramref name="target" />. The
	///     visited set is what stops a cycle already in the data from looping
	///     here. In practice this only ever finds a cycle of length one, since a
	///     <see cref="QuestionType.Group" /> cannot itself be grouped under
	///     another group — but the walk costs nothing extra to make general.
	/// </summary>
	private static bool LeadsTo(IReadOnlyCollection<Question> questions,
								TinyId from,
								TinyId target)
	{
		var visited = new HashSet<TinyId>();
		var current = from;

		while (visited.Add(current))
		{
			var question = questions.FirstOrDefault(candidate => candidate.Id == current);

			if (question?.GroupedUnderQuestionId is not { } next)
			{
				return false;
			}

			if (next == target)
			{
				return true;
			}

			current = next;
		}

		return false;
	}
}
