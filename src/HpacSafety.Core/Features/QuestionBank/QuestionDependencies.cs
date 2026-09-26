namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     The rules that no single question can check on its own, because they are
///     about its relationship to the others.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="QuestionRevision" /> checks everything visible from inside one
///         revision — that a dependency is not self-referential, that the system
///         question is not made conditional. It cannot check the <i>parent's</i>
///         current type, or, for a single-select parent, whether it currently offers
///         the required option — those facts live on the parent's current revision,
///         which is a different row. Nor can it see a cycle. All three are checked
///         here, against the live bank, and the database is not asked to enforce
///         them: doing so would take a trigger reading a second table, and a trigger
///         is a rule hidden from everyone reading the C#. See ADR-0060, ADR-0074.
///     </para>
///     <para>
///         This is a static rule-checker over a collection, not a repository. It does
///         no I/O — the caller loads the live questions and hands them in.
///     </para>
///     <para>
///         A condition names the parent question and choice it was saved with. When
///         that parent forks (ADR-0071), the dependent is neither revised nor forked:
///         every reader resolves the retired parent to the live question with its key,
///         and the retired choice to its copy there, through any replacement
///         (<see cref="ParentToday" />, <see cref="RequiredChoiceToday" />). A key is
///         shared only by one fork chain and never reused (REQ-QB-096), so the live
///         question with it is the one that replaced the parent. To follow a fork the
///         collection must also hold the retired parents live questions still name.
///         See ADR-0132.
///     </para>
/// </remarks>
public static class QuestionDependencies
{
	/// <summary>
	///     Checks that a question may depend on the one it names: the parent
	///     exists, is live, is a type that can enable another question, currently
	///     offers <paramref name="requiredChoiceId" /> — or the choice that replaced
	///     it — when it needs one, and does not lead back to the child.
	/// </summary>
	/// <param name="questions">Every live question, including the child if it already exists.</param>
	/// <param name="childId">The question being made conditional, or null when it is being created.</param>
	/// <param name="parentId">The question it is to depend on.</param>
	/// <param name="requiredChoiceId">
	///     The parent's choice it must be answered with, when the parent is
	///     single-select. Must be null for a yes/no parent, whose condition is a
	///     yes. See ADR-0074, ADR-0128.
	/// </param>
	/// <exception cref="DomainRuleViolationException">When the dependency is not allowed.</exception>
	public static void EnsureDependencyAllowed(
		IReadOnlyCollection<Question> questions,
		TinyId? childId,
		TinyId parentId,
		TinyId? requiredChoiceId = null)
	{
		ArgumentNullException.ThrowIfNull(questions);

		if (childId == parentId)
		{
			throw new DomainRuleViolationException("A question cannot be conditional on itself.");
		}

		var parent = questions.FirstOrDefault(question => question.Id == parentId && question.Deleted is null)
					 ?? throw new DomainRuleViolationException(
						 "That question no longer exists, so nothing can be made conditional on it.");

		switch (parent.Type)
		{
			case QuestionType.YesNo when requiredChoiceId is not null:
				throw new DomainRuleViolationException(
					$"'{parent.Key}' is a yes/no question. Its condition is always 'answered yes' and cannot also name a required option.");

			case QuestionType.YesNo:
				break;

			case QuestionType.SingleSelect when requiredChoiceId is null:
				throw new DomainRuleViolationException(
					$"'{parent.Key}' is a single-select question and needs a required option to enable another one.");

			case QuestionType.SingleSelect when parent.CurrentChoice(requiredChoiceId.Value) is not { Deleted: null }:
				throw new DomainRuleViolationException(
					$"'{parent.Key}' does not currently offer that option.");

			case QuestionType.SingleSelect:
				break;

			default:
				throw new DomainRuleViolationException(
					$"'{parent.Key}' is a {EnumCode.Of(parent.Type)} question. Only a yes/no or single-select question can enable another one.");
		}

		if (childId is { } child
			&& LeadsTo(questions, parent.Id, child))
		{
			throw new DomainRuleViolationException(
				$"'{parent.Key}' already depends on this question, directly or through another one. A cycle would leave both permanently disabled.");
		}
	}

	/// <summary>
	///     Checks that saving <paramref name="parent" />'s choices as
	///     <paramref name="remainingCodes" /> removes none a live question depends
	///     on. Removing one would silently leave that question never enabled, so
	///     the save is refused naming it (ADR-0074, ADR-0095).
	/// </summary>
	/// <param name="questions">Every live question.</param>
	/// <param name="parent">The question whose choices are being saved.</param>
	/// <param name="remainingCodes">Every code the saved list keeps.</param>
	/// <exception cref="DomainRuleViolationException">When a removed choice enables another question.</exception>
	public static void EnsureChoicesRemovable(
		IReadOnlyCollection<Question> questions,
		Question parent,
		IReadOnlyCollection<string> remainingCodes)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);
		ArgumentNullException.ThrowIfNull(remainingCodes);

		var kept = remainingCodes.Select(QuestionKey.Normalize).ToHashSet(StringComparer.Ordinal);

		// A dependency names a choice, and follows it through any replacement
		// (ADR-0128): what matters is the choice that stands for it today.
		var dependent = questions.FirstOrDefault(question => question.Deleted is null
															 && question.DependsOnQuestionId is { } named
															 && ParentToday(questions, named) == parent
															 && RequiredChoiceToday(questions, question.CurrentRevision) is { Deleted: null } current
															 && !kept.Contains(current.Code));

		if (dependent is not null)
		{
			var wording = RequiredChoiceToday(questions, dependent.CurrentRevision)!.Label(Locale.EnCa);
			throw new DomainRuleViolationException(
				$"'{dependent.CurrentRevision.LabelEn}' is shown only when '{wording}' is chosen. Change that question first, then remove the choice.");
		}
	}

	/// <summary>
	///     The question that stands today for the parent a condition names: that
	///     question while it is live, or — once it has forked — the live question that
	///     replaced it, which carries its key (ADR-0071, ADR-0132). Null when the named
	///     question is not among <paramref name="questions" />, or was deleted rather
	///     than forked.
	/// </summary>
	public static Question? ParentToday(IReadOnlyCollection<Question> questions,
										TinyId parentId)
	{
		ArgumentNullException.ThrowIfNull(questions);

		var named = questions.FirstOrDefault(question => question.Id == parentId);

		return named is null or { Deleted: null }
			? named
			: questions.FirstOrDefault(question => question.Deleted is null && question.Key == named.Key);
	}

	/// <summary>
	///     The choice that stands today for the one <paramref name="revision" />
	///     requires: on the parent today (<see cref="ParentToday" />), the choice itself
	///     or — when the parent forked — its copy, which keeps its code, followed
	///     through any replacement (ADR-0128, ADR-0132). Null when the revision
	///     requires no choice or its parent cannot be resolved.
	/// </summary>
	public static QuestionChoice? RequiredChoiceToday(IReadOnlyCollection<Question> questions,
													  QuestionRevision revision)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(revision);

		if (revision is not { DependsOnQuestionId: { } parentId, DependsOnChoiceId: { } choiceId }
			|| ParentToday(questions, parentId) is not { } today)
		{
			return null;
		}

		if (today.Id == parentId)
		{
			return today.CurrentChoice(choiceId);
		}

		var named = questions.First(question => question.Id == parentId);
		var code = named.AllChoices.FirstOrDefault(choice => choice.Id == choiceId)?.Code;

		return today.AllChoices.FirstOrDefault(choice => choice.Code == code) is { } copy
			? today.CurrentChoice(copy.Id)
			: null;
	}

	/// <summary>
	///     Whether following <see cref="Question.DependsOnQuestionId" /> from
	///     <paramref name="from" /> reaches <paramref name="target" />. The visited
	///     set is what stops a cycle already in the data from looping here.
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

			if (question?.DependsOnQuestionId is not { } named
				|| ParentToday(questions, named)?.Id is not { } next)
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
