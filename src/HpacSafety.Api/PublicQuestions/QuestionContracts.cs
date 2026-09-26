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
			revision.TakesReporterAdditions,
			revision.LabelEn,
			revision.LabelFr,
			revision.HelpTextEn,
			revision.HelpTextFr,
			revision.PlaceholderEn,
			revision.PlaceholderFr,
			[.. question.Choices.Select(PublicOptionView.Of)],
			children);
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
public sealed record PublicOptionView(string Id, string Code, string LabelEn, string LabelFr, string? OnlyIn, string Pin)
{
	/// <summary>Flattens one choice for the public form.</summary>
	public static PublicOptionView Of(QuestionChoice choice)
	{
		ArgumentNullException.ThrowIfNull(choice);

		return new PublicOptionView(
			choice.Id.Value,
			choice.Code,
			choice.Label(Locale.EnCa),
			choice.Label(Locale.FrCa),
			choice.NeedsTranslation ? (choice.LabelEn is null ? Locale.FrCa : Locale.EnCa).Code : null,
			EnumCode.Of(choice.Pin));
	}
}
