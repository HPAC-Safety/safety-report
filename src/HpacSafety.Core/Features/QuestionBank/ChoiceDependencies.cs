namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     The rules for a question whose choices depend on another question's answer
///     that no single question can check on its own, because they are about two
///     questions at once: the <i>parent</i>, whose answer decides what is offered,
///     and the <i>child</i>, each of whose choices names one parent choice
///     (ADR-0146).
/// </summary>
/// <remarks>
///     <para>
///         A dependency is not a condition. A condition
///         (<see cref="QuestionDependencies" />) decides whether a question is shown;
///         a dependency decides which of its choices are offered. A question may be
///         both.
///     </para>
///     <para>
///         The dependency and every link live outside revisions, on the question and
///         its choices, so nothing here ever revises or forks either question. When
///         the parent choice a link names is replaced, merged, or copied by a fork,
///         <see cref="Follow" /> re-points the link at the choice that stands for it
///         today, at the moment it happens. Readers then compare identifiers and
///         never resolve anything.
///     </para>
///     <para>
///         Like <see cref="QuestionDependencies" />, this is a static rule-checker over
///         a collection the caller loads, with no I/O. The database holds the
///         references and refuses a self-reference; which types may take part, that
///         the dependency is one level deep, and that a link names a choice of the
///         parent read the parent's current revision and choices, so they are checked
///         here rather than by a trigger (ADR-0060, ADR-0074, ADR-0146).
///     </para>
/// </remarks>
public static class ChoiceDependencies
{
	/// <summary>Whether a question of this type may take part in a dependency, as parent or child: a single-select or a type-ahead.</summary>
	public static bool TakesPart(QuestionType type)
	{
		return type is QuestionType.SingleSelect or QuestionType.Autocomplete;
	}

	/// <summary>
	///     Checks that a question may have its choices depend on the one it names: the
	///     parent is another live question, both are single-selects or type-aheads,
	///     the parent depends on nothing and the child is nobody's parent — one level
	///     only — and the parent comes before the child on the form.
	/// </summary>
	/// <param name="questions">Every live question, including the child if it already exists.</param>
	/// <param name="childId">The question whose choices are to depend on the parent, or null when it is being created.</param>
	/// <param name="childLabel">The child's English wording, to name it in a refusal.</param>
	/// <param name="childType">The type the child is being saved as.</param>
	/// <param name="childDisplayOrder">Where the child sits on the form.</param>
	/// <param name="parentId">The question it is to depend on.</param>
	/// <param name="childGroupedUnderId">The group the child renders under, if any: it is asked where its group is.</param>
	/// <exception cref="DomainRuleViolationException">When the dependency is not allowed.</exception>
	public static void EnsureDependencyAllowed(
		IReadOnlyCollection<Question> questions,
		TinyId? childId,
		string childLabel,
		QuestionType childType,
		int childDisplayOrder,
		TinyId parentId,
		TinyId? childGroupedUnderId = null)
	{
		ArgumentNullException.ThrowIfNull(questions);

		if (childId == parentId)
		{
			throw new DomainRuleViolationException("A question's choices cannot depend on the question itself.");
		}

		var parent = questions.FirstOrDefault(question => question.Id == parentId && question.Deleted is null)
					 ?? throw new DomainRuleViolationException(
						 "That question no longer exists, so no question's choices can depend on it.");

		if (!TakesPart(childType))
		{
			throw new DomainRuleViolationException(
				$"A {EnumCode.Of(childType)} question's choices cannot depend on another question. Only a single-select or type-ahead's can.");
		}

		if (!TakesPart(parent.Type))
		{
			throw new DomainRuleViolationException(
				$"'{parent.CurrentRevision.LabelEn}' is a {EnumCode.Of(parent.Type)} question. Only a single-select or type-ahead's answer can decide another question's choices.");
		}

		if (parent.ChoicesDependOnQuestionId is not null)
		{
			throw new DomainRuleViolationException(
				$"'{parent.CurrentRevision.LabelEn}' already has choices that depend on another question. A dependency is one level deep.");
		}

		if (childId is { } child
			&& questions.FirstOrDefault(question => question.Deleted is null && question.ChoicesDependOnQuestionId == child) is { } dependent)
		{
			throw new DomainRuleViolationException(
				$"The choices of '{dependent.CurrentRevision.LabelEn}' already depend on this question. A dependency is one level deep.");
		}

		if (FormPosition(questions, parent.GroupedUnderQuestionId, parent.DisplayOrder)
			.CompareTo(FormPosition(questions, childGroupedUnderId, childDisplayOrder)) >= 0)
		{
			throw new DomainRuleViolationException(
				$"'{parent.CurrentRevision.LabelEn}' must come before '{childLabel}' on the form, so it is answered first.");
		}
	}

	/// <summary>
	///     Checks that a parent saved with <paramref name="groupedUnderId" /> still comes
	///     before every question whose choices depend on it, wherever grouping places
	///     them on the form (ADR-0146).
	/// </summary>
	public static void EnsureDependentsFollow(IReadOnlyCollection<Question> questions,
											  Question parent,
											  TinyId? groupedUnderId)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);

		var at = FormPosition(questions, groupedUnderId, parent.DisplayOrder);

		if (DependentsOf(questions, parent)
				.FirstOrDefault(child => at.CompareTo(FormPosition(questions, child.GroupedUnderQuestionId, child.DisplayOrder)) >= 0) is { } before)
		{
			throw new DomainRuleViolationException(
				$"'{parent.CurrentRevision.LabelEn}' must come before '{before.CurrentRevision.LabelEn}' on the form, because the choices of '{before.CurrentRevision.LabelEn}' depend on it.");
		}
	}

	/// <summary>
	///     Where a question is asked: the position of its group's page when it renders
	///     under a group, then its own order within that page; a question on a page of
	///     its own comes before anything grouped under the same position.
	/// </summary>
	private static (int Page, int Within) FormPosition(IReadOnlyCollection<Question> questions,
													   TinyId? groupedUnderId,
													   int displayOrder)
	{
		return groupedUnderId is { } groupId
			   && questions.FirstOrDefault(question => question.Id == groupId && question.Deleted is null) is { } group
			? (group.DisplayOrder, displayOrder)
			: (displayOrder, int.MinValue);
	}

	/// <summary>
	///     Checks that a question other questions' choices depend on may be saved as
	///     <paramref name="type" />: a parent stays a single-select or type-ahead
	///     while any live question depends on it.
	/// </summary>
	public static void EnsureParentKeepsType(IReadOnlyCollection<Question> questions,
											 Question parent,
											 QuestionType type)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);

		if (!TakesPart(type)
			&& DependentsOf(questions, parent).FirstOrDefault() is { } dependent)
		{
			throw new DomainRuleViolationException(
				$"The choices of '{dependent.CurrentRevision.LabelEn}' depend on this question, so it must stay a single-select or type-ahead.");
		}
	}

	/// <summary>
	///     Checks that every live choice of a dependent <paramref name="child" /> names
	///     a live choice of its parent — never another question's — naming the first
	///     that does not. A question whose choices depend on nothing is not checked.
	/// </summary>
	public static void EnsureLinksAllowed(IReadOnlyCollection<Question> questions,
										  Question child)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(child);

		if (child.ChoicesDependOnQuestionId is not { } parentId)
		{
			return;
		}

		var parent = questions.FirstOrDefault(question => question.Id == parentId && question.Deleted is null)
					 ?? throw new DomainRuleViolationException(
						 "That question no longer exists, so no question's choices can depend on it.");

		if (child.Choices.FirstOrDefault(choice => choice.ParentChoiceId is not { } linked || parent.OfferedChoice(linked) is null) is { } stray)
		{
			throw new DomainRuleViolationException(
				$"'{stray.LabelEn ?? stray.LabelFr}' must be offered under a choice '{parent.CurrentRevision.LabelEn}' offers.");
		}
	}

	/// <summary>
	///     Checks one value a reviewer relinked: it names a live choice of its
	///     question's parent. Other values are not judged by it (ADR-0146).
	/// </summary>
	public static void EnsureLinkAllowed(IReadOnlyCollection<Question> questions,
										 Question child,
										 TinyId choiceId)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(child);

		var parent = questions.FirstOrDefault(question => question.Id == child.ChoicesDependOnQuestionId && question.Deleted is null)
					 ?? throw new DomainRuleViolationException(
						 "That question no longer exists, so no question's choices can depend on it.");
		var value = child.AllChoices.First(choice => choice.Id == choiceId);

		if (value.ParentChoiceId is not { } linked || parent.OfferedChoice(linked) is null)
		{
			throw new DomainRuleViolationException(
				$"'{value.LabelEn ?? value.LabelFr}' must be offered under a choice '{parent.CurrentRevision.LabelEn}' offers.");
		}
	}

	/// <summary>
	///     Checks that saving <paramref name="parent" />'s choices as
	///     <paramref name="remainingCodes" /> removes none a live choice of a dependent
	///     question is offered under. Removing one would leave that choice offered
	///     under nothing, so the save is refused naming the dependent; a replaced
	///     option keeps its code in the list and passes its links on instead
	///     (ADR-0146).
	/// </summary>
	public static void EnsureParentChoicesRemovable(IReadOnlyCollection<Question> questions,
													Question parent,
													IReadOnlyCollection<string> remainingCodes)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);
		ArgumentNullException.ThrowIfNull(remainingCodes);

		var kept = remainingCodes.Select(QuestionKey.Normalize).ToHashSet(StringComparer.Ordinal);
		var removed = parent.Choices.Where(choice => !kept.Contains(choice.Code)).Select(choice => choice.Id).ToHashSet();

		EnsureNoneLinked(questions, parent, removed);
	}

	/// <summary>
	///     Checks that a reviewer may remove a type-ahead parent's value: no live
	///     choice of a dependent question is offered under it. Merging it instead
	///     passes its links to the value it is merged into (ADR-0146).
	/// </summary>
	public static void EnsureValueRemovable(IReadOnlyCollection<Question> questions,
											Question parent,
											TinyId choiceId)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);

		EnsureNoneLinked(questions, parent, [choiceId]);
	}

	/// <summary>
	///     Checks that merging parent value <paramref name="sourceId" /> into
	///     <paramref name="targetId" /> leaves no dependent question offering one
	///     wording twice under the target, which a reporter could not tell apart. The
	///     reviewer merges or removes one of the pair first (ADR-0146).
	/// </summary>
	public static void EnsureMergeKeepsChildrenApart(IReadOnlyCollection<Question> questions,
													 Question parent,
													 TinyId sourceId,
													 TinyId targetId)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);

		static IEnumerable<string> Wording(QuestionChoice choice)
		{
			return new[] { choice.LabelEn, choice.LabelFr }.OfType<string>().Select(label => label.Trim());
		}

		foreach (var child in DependentsOf(questions, parent))
		{
			var underTarget = child.Choices.Where(choice => choice.ParentChoiceId == targetId).SelectMany(Wording)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			if (child.Choices.FirstOrDefault(choice => choice.ParentChoiceId == sourceId && Wording(choice).Any(underTarget.Contains)) is { } clash)
			{
				throw new DomainRuleViolationException(
					$"'{child.CurrentRevision.LabelEn}' offers '{clash.LabelEn ?? clash.LabelFr}' under both values. Merge or remove one of those first.");
			}
		}
	}

	/// <summary>
	///     Checks an arrangement of the form: every parent comes before each question
	///     whose choices depend on it, so the reporter answers it first.
	/// </summary>
	/// <param name="ordered">Every live question, in its new order.</param>
	public static void EnsureOrder(IReadOnlyList<Question> ordered)
	{
		ArgumentNullException.ThrowIfNull(ordered);

		var index = ordered.Select((question, at) => (question.Id, at)).ToDictionary(pair => pair.Id, pair => pair.at);

		// A grouped question is asked on its group's page, wherever the group is.
		(int Page, int Within) Position(Question question)
		{
			return question.GroupedUnderQuestionId is { } groupId && index.TryGetValue(groupId, out var groupAt)
				? (groupAt, index[question.Id])
				: (index[question.Id], int.MinValue);
		}

		foreach (var child in ordered)
		{
			if (child.ChoicesDependOnQuestionId is { } parentId
				&& index.TryGetValue(parentId, out var parentAt)
				&& Position(ordered[parentAt]).CompareTo(Position(child)) >= 0)
			{
				var parent = ordered[parentAt];
				throw new DomainRuleViolationException(
					$"'{parent.CurrentRevision.LabelEn}' must come before '{child.CurrentRevision.LabelEn}' on the form, because the choices of '{child.CurrentRevision.LabelEn}' depend on it.");
			}
		}
	}

	/// <summary>
	///     The live questions whose choices depend on <paramref name="parent" />.
	/// </summary>
	public static IEnumerable<Question> DependentsOf(IReadOnlyCollection<Question> questions,
													 Question parent)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);

		return questions.Where(question => question.Deleted is null && question.ChoicesDependOnQuestionId == parent.Id);
	}

	/// <summary>
	///     Re-points every dependency on <paramref name="parent" /> — or on the
	///     question it replaced when it forked — at it, and every link at the parent
	///     choice standing today for the one it named: that choice, or its copy on the
	///     replacement after a fork, followed through any replacement (ADR-0128) and
	///     merge (ADR-0129). Neither question is revised: the dependency and the links
	///     live outside revisions (ADR-0146). Call it after anything that retires a
	///     parent choice or the parent itself.
	/// </summary>
	/// <param name="questions">
	///     Every question the change touched and every live one: the retired parent a
	///     fork left behind included, so its choices can be matched to their copies.
	/// </param>
	/// <param name="parent">The parent as it stands now.</param>
	public static void Follow(IReadOnlyCollection<Question> questions,
							  Question parent)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);

		var byId = questions.GroupBy(question => question.Id).ToDictionary(group => group.Key, group => group.First());

		foreach (var child in questions.Where(question => question.Deleted is null && question.ChoicesDependOnQuestionId is not null))
		{
			if (!byId.TryGetValue(child.ChoicesDependOnQuestionId!.Value, out var named)
				|| (named.Id != parent.Id && !(named.Deleted is not null && named.Key == parent.Key)))
			{
				continue;
			}

			child.FollowParent(parent.Id);

			foreach (var choice in child.AllChoices.Where(choice => choice.ParentChoiceId is not null))
			{
				if (ChoiceToday(named, parent, choice.ParentChoiceId!.Value) is { } today
					&& today.Id != choice.ParentChoiceId)
				{
					choice.LinkTo(today.Id);
				}
			}
		}
	}

	/// <summary>
	///     The choice of <paramref name="parent" /> that stands today for
	///     <paramref name="choiceId" />, a choice of <paramref name="named" />: its copy
	///     when <paramref name="named" /> forked into <paramref name="parent" />, then
	///     whatever replaced it, then whatever it was merged into.
	/// </summary>
	private static QuestionChoice? ChoiceToday(Question named,
											   Question parent,
											   TinyId choiceId)
	{
		var id = choiceId;

		if (named.Id != parent.Id)
		{
			var code = named.AllChoices.FirstOrDefault(choice => choice.Id == choiceId)?.Code;
			if (parent.AllChoices.FirstOrDefault(choice => choice.Code == code) is not { } copy)
			{
				return null;
			}

			id = copy.Id;
		}

		var current = parent.CurrentChoice(id);

		return current?.MergedIntoChoiceId is { } target
			? parent.AllChoices.FirstOrDefault(choice => choice.Id == target)
			: current;
	}

	private static void EnsureNoneLinked(IReadOnlyCollection<Question> questions,
										 Question parent,
										 HashSet<TinyId> removed)
	{
		if (removed.Count == 0)
		{
			return;
		}

		foreach (var child in DependentsOf(questions, parent))
		{
			if (child.Choices.FirstOrDefault(choice => choice.ParentChoiceId is { } linked && removed.Contains(linked)) is { } linkedChoice)
			{
				var wording = parent.AllChoices.First(choice => choice.Id == linkedChoice.ParentChoiceId).Label(Locale.EnCa);
				throw new DomainRuleViolationException(
					$"The choices of '{child.CurrentRevision.LabelEn}' are offered under '{wording}'. Link them to another choice first, or replace or merge '{wording}' instead of removing it.");
			}
		}
	}
}
