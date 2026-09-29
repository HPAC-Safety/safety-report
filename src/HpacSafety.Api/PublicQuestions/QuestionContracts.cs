using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Api.PublicQuestions;

/// <summary>
///     One question as the reporter-facing form needs it: its current
///     revision, bilingual, with a group's children nested inside it. Carries
///     no authoring-only field (no <c>HasBeenAnswered</c>, no system flag) —
///     only the role, because the form asks media consent by a rule that reads
///     the publication-consent answer and the attachments (ADR-0117) — see <c>skills/persist-hpac-data</c>: this is purpose-built for
///     the public form, not a reuse of the admin screen's shape.
/// </summary>
/// <remarks>
///     A <see cref="QuestionType.Group" /> question's <see cref="Children" />
///     are the live questions grouped under it, in form order, so the client
///     never repeats them at the top level or filters the list itself to
///     render a group and its children together. Every other question has an
///     empty <see cref="Children" /> list — domain rules forbid nesting a
///     group under another group (ADR-0076), so no question is ever both a
///     top-level entry and somebody's child.
/// </remarks>
public sealed record PublicQuestionView(
	string Id,
	string Key,
	string Role,
	string RevisionId,
	string Type,
	bool IsRequired,
	bool IsPrivate,
	bool AllowFutureDates,
	int DisplayOrder,
	string? DependsOnQuestionId,
	string? DependsOnChoiceId,
	string? ChoicesDependOnQuestionId,
	bool AllowsReporterAdditions,
	string LabelEn,
	string LabelFr,
	string? HelpTextEn,
	string? HelpTextFr,
	string? PlaceholderEn,
	string? PlaceholderFr,
	IReadOnlyList<PublicOptionView> Options,
	IReadOnlyList<PublicQuestionView> Children)
{
	/// <summary>Flattens a question and its current revision for the public form.</summary>
	/// <param name="question">The question to show, with its choices loaded.</param>
	/// <param name="children">
	///     This question's grouped children, already resolved and ordered.
	///     Empty for anything but a live <see cref="QuestionType.Group" />
	///     question.
	/// </param>
	/// <param name="bank">
	///     Every live question, so a condition naming a replaced option names the
	///     option that replaced it — the one the form offers (ADR-0128).
	/// </param>
	public static PublicQuestionView Of(
		Question question,
		IReadOnlyList<PublicQuestionView> children,
		IReadOnlyCollection<Question> bank)
	{
		ArgumentNullException.ThrowIfNull(question);
		ArgumentNullException.ThrowIfNull(children);

		var revision = question.CurrentRevision;

		return new PublicQuestionView(
			question.Id.Value,
			question.Key,
			EnumCode.Of(question.Role),
			revision.Id.Value,
			EnumCode.Of(revision.Type),
			revision.IsRequired,
			revision.IsPrivate,
			revision.AllowFutureDates,
			revision.DisplayOrder,
			(revision.DependsOnQuestionId is { } parentId
				? QuestionDependencies.ParentToday(bank, parentId)?.Id ?? parentId
				: revision.DependsOnQuestionId)?.Value,
			(QuestionDependencies.RequiredChoiceToday(bank, revision)?.Id ?? revision.DependsOnChoiceId)?.Value,
			ChoiceParentOnForm(question, bank)?.Value,
			revision.TakesReporterAdditions,
			revision.LabelEn,
			revision.LabelFr,
			revision.HelpTextEn,
			revision.HelpTextFr,
			revision.PlaceholderEn,
			revision.PlaceholderFr,
			[.. question.Choices.Select(choice => PublicOptionView.Of(choice, question.AliasesOf(choice.Id)))],
			children);
	}

	/// <summary>
	///     The question whose answer filters this one's choices, when it is on the
	///     form. A parent the form does not ask — deactivated, or deleted — filters
	///     nothing, as a condition whose parent is missing hides nothing (ADR-0146).
	/// </summary>
	private static TinyId? ChoiceParentOnForm(Question question,
											  IReadOnlyCollection<Question> bank)
	{
		return question.ChoicesDependOnQuestionId is { } parentId
			   && bank.Any(candidate => candidate.Id == parentId && candidate.IsActive)
			? parentId
			: null;
	}
}

/// <summary>
///     One choice as the public form offers it. A reporter-added choice may have
///     only one language yet; both labels then carry that wording, and
///     <see cref="OnlyIn" /> names its language so the form can mark it
///     (ADR-0095).
/// </summary>
/// <param name="Id">The choice's identifier, which a submitted answer names (ADR-0128).</param>
/// <param name="Code">The invariant code a conditional question names (ADR-0074).</param>
/// <param name="LabelEn">The English wording, or the French when there is no English yet.</param>
/// <param name="LabelFr">The French wording, or the English when there is no French yet.</param>
/// <param name="OnlyIn">The one locale this choice is worded in, or null when it has both.</param>
/// <param name="Pin">
///     <c>first</c>, <c>last</c>, or <c>none</c>: whether the form lists this choice
///     before or after the alphabetical rest, or among them (ADR-0136).
/// </param>
/// <param name="ParentChoiceIds">
///     The parent question's choices the form offers this one under, when its
///     question's choices depend on another's: it is offered whenever the parent
///     is answered with any of them (ADR-0151).
/// </param>
/// <param name="Aliases">
///     Every wording ever merged into this choice, so the form can offer this
///     choice — never the merged-away value itself — with a hint naming the
///     wording a reporter typed, while they type it (ADR-0129 amendment). A
///     chained merge is already flattened here; no client-side chain-following
///     is needed.
/// </param>
public sealed record PublicOptionView(
	string Id,
	string Code,
	string LabelEn,
	string LabelFr,
	string? OnlyIn,
	string Pin,
	IReadOnlyList<string> ParentChoiceIds,
	IReadOnlyList<PublicAliasView> Aliases)
{
	/// <summary>Flattens one choice for the public form.</summary>
	/// <param name="choice">The live choice to show.</param>
	/// <param name="aliases">Every value merged into it (ADR-0129 amendment).</param>
	public static PublicOptionView Of(QuestionChoice choice,
									  IReadOnlyList<QuestionChoice>? aliases = null)
	{
		ArgumentNullException.ThrowIfNull(choice);

		return new PublicOptionView(
			choice.Id.Value,
			choice.Code,
			choice.Label(Locale.EnCa),
			choice.Label(Locale.FrCa),
			choice.NeedsTranslation ? (choice.LabelEn is null ? Locale.FrCa : Locale.EnCa).Code : null,
			EnumCode.Of(choice.Pin),
			[.. choice.ParentChoiceIds.Select(id => id.Value)],
			[.. (aliases ?? []).Select(alias => new PublicAliasView(alias.LabelEn, alias.LabelFr))]);
	}
}

/// <summary>
///     One wording merged away into a live type-ahead value (ADR-0129 amendment).
///     Carries whichever language(s) it had — a reporter-added value may have had
///     only one — so a reporter typing it in either language is matched.
/// </summary>
/// <param name="LabelEn">The merged value's English wording, or null while it had none.</param>
/// <param name="LabelFr">The merged value's French wording, or null while it had none.</param>
public sealed record PublicAliasView(string? LabelEn, string? LabelFr);
