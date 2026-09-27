namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     The rules for a question whose choices depend on another question's answer
///     that no single question can check on its own, because they are about two
///     questions at once: the <i>parent</i>, whose answer decides what is offered,
///     and the <i>child</i>, each of whose choices names one or more parent
///     choices (ADR-0146, ADR-0151).
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
///         today, at the moment it happens: the old link is stamped and the choice
///         offered under the new one, collapsing into a link it already has. Readers
///         then compare identifiers and never resolve anything.
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
	///     Checks that every live choice of a dependent <paramref name="child" /> is
	///     offered under at least one live choice of its parent, and that no live link
	///     names another question's choice, naming the first choice that fails. A link
	///     to a parent choice since removed may stay: it filters nothing (ADR-0151). A
	///     question whose choices depend on nothing is not checked.
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

		var parent = ParentOf(questions, parentId);

		if (child.Choices.FirstOrDefault(choice => !LinksAllowed(parent, choice)) is { } stray)
		{
			throw new DomainRuleViolationException(
				$"'{stray.LabelEn ?? stray.LabelFr}' must be offered under at least one choice '{parent.CurrentRevision.LabelEn}' offers, and under no other question's choice.");
		}
	}

	/// <summary>
	///     Checks the parent choices a save ticks: each is a live choice of the parent
	///     question <paramref name="parentId" />, never a removed one or another
	///     question's (ADR-0151).
	/// </summary>
	public static void EnsureOfferable(IReadOnlyCollection<Question> questions,
									   TinyId parentId,
									   IEnumerable<TinyId> parentChoiceIds)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parentChoiceIds);

		var parent = ParentOf(questions, parentId);

		if (parentChoiceIds.Any(id => parent.OfferedChoice(id) is null))
		{
			throw new DomainRuleViolationException(
				$"A choice can be offered only under a choice '{parent.CurrentRevision.LabelEn}' offers.");
		}
	}

	/// <summary>
	///     The parent choices a save offers <paramref name="existing" /> under: the ones
	///     it ticks, each a live choice of the parent unless the choice already names
	///     it, and every link it already has to a parent choice since removed. A
	///     control lists only the parent's live choices, so it can neither show nor
	///     untick such a link; it stays, and filters nothing (ADR-0151).
	/// </summary>
	/// <param name="questions">Every live question.</param>
	/// <param name="parentId">The question the choices depend on.</param>
	/// <param name="existing">The choice being saved, or null for a new one.</param>
	/// <param name="ticked">The parent choices the save ticks.</param>
	public static IReadOnlyList<TinyId> WithStandingLinks(IReadOnlyCollection<Question> questions,
														  TinyId parentId,
														  QuestionChoice? existing,
														  IReadOnlyCollection<TinyId> ticked)
	{
		ArgumentNullException.ThrowIfNull(ticked);

		var linked = existing?.ParentChoiceIds ?? [];
		EnsureOfferable(questions, parentId, ticked.Where(id => !linked.Contains(id)));

		var parent = ParentOf(questions, parentId);
		var inert = linked.Where(id => parent.OfferedChoice(id) is null && parent.AllChoices.Any(choice => choice.Id == id));

		return [.. ticked.Union(inert)];
	}

	/// <summary>
	///     The parent choices a reviewer's save offers one value under: those ticked,
	///     at least one, plus any link to a parent value since removed, which the page
	///     cannot show (ADR-0151).
	/// </summary>
	public static IReadOnlyList<TinyId> ValueParents(IReadOnlyCollection<Question> questions,
													 Question child,
													 TinyId choiceId,
													 IReadOnlyCollection<TinyId> ticked)
	{
		ArgumentNullException.ThrowIfNull(child);
		ArgumentNullException.ThrowIfNull(ticked);

		var parentId = child.ChoicesDependOnQuestionId
					   ?? throw new DomainRuleViolationException($"'{child.Key}' does not depend on another question, so its values have no parent choice.");

		if (ticked.Count == 0)
		{
			throw new DomainRuleViolationException("A value is offered under at least one choice of the parent question. Tick another before unticking the last.");
		}

		return WithStandingLinks(questions, parentId, child.AllChoices.FirstOrDefault(choice => choice.Id == choiceId), ticked);
	}

	/// <summary>
	///     Checks that saving <paramref name="parent" />'s choices as
	///     <paramref name="remainingCodes" /> leaves every live choice of a dependent
	///     question offered under at least one live parent choice. A removed parent
	///     choice's links then stay and filter nothing; the save is refused only when
	///     a child choice would be left under none, naming those choices. A replaced
	///     option keeps its code in the list and passes its links on instead
	///     (ADR-0151).
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

		EnsureNoneStranded(questions, parent, removed);
	}

	/// <summary>
	///     Checks that a reviewer may remove a type-ahead parent's value: every live
	///     choice of a dependent question offered under it is offered under another
	///     live value too. Merging it instead passes its links to the value it is
	///     merged into (ADR-0151).
	/// </summary>
	public static void EnsureValueRemovable(IReadOnlyCollection<Question> questions,
											Question parent,
											TinyId choiceId)
	{
		ArgumentNullException.ThrowIfNull(questions);
		ArgumentNullException.ThrowIfNull(parent);

		EnsureNoneStranded(questions, parent, [choiceId]);
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
	/// <param name="at">When the change happened: the stamp on each link it re-points.</param>
	public static void Follow(IReadOnlyCollection<Question> questions,
							  Question parent,
							  DateTimeOffset at)
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

			foreach (var choice in child.AllChoices)
			{
				foreach (var linked in choice.ParentChoiceIds)
				{
					if (ChoiceToday(named, parent, linked) is { } today
						&& today.Id != linked)
					{
						choice.Repoint(linked, today.Id, at);
					}
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

	private static Question ParentOf(IReadOnlyCollection<Question> questions,
									 TinyId parentId)
	{
		return questions.FirstOrDefault(question => question.Id == parentId && question.Deleted is null)
			   ?? throw new DomainRuleViolationException(
				   "That question no longer exists, so no question's choices can depend on it.");
	}

	/// <summary>
	///     A choice's links are allowed when at least one names a live choice of the
	///     parent, and none names another question's.
	/// </summary>
	private static bool LinksAllowed(Question parent,
									 QuestionChoice choice)
	{
		var linked = choice.ParentChoiceIds;

		return linked.Any(id => parent.OfferedChoice(id) is not null)
			   && linked.All(id => parent.AllChoices.Any(parentChoice => parentChoice.Id == id));
	}

	private static void EnsureNoneStranded(IReadOnlyCollection<Question> questions,
										   Question parent,
										   HashSet<TinyId> removed)
	{
		if (removed.Count == 0)
		{
			return;
		}

		var remaining = parent.Choices.Select(choice => choice.Id).Where(id => !removed.Contains(id)).ToHashSet();

		foreach (var child in DependentsOf(questions, parent))
		{
			var stranded = child.Choices
				.Where(choice => choice.ParentChoiceIds.Any(removed.Contains) && !choice.ParentChoiceIds.Any(remaining.Contains))
				.ToList();

			if (stranded.Count > 0)
			{
				var wording = string.Join(", ", stranded.Select(choice => $"'{choice.LabelEn ?? choice.LabelFr}'"));
				throw new DomainRuleViolationException(
					$"Removing that would leave {wording} of '{child.CurrentRevision.LabelEn}' offered under no choice. Offer them under another choice first, or replace or merge it instead of removing it.");
			}
		}
	}
}
