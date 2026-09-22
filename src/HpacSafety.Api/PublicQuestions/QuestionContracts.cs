using HpacSafety.Api.Admin;
using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Api.PublicQuestions;

/// <summary>
///     One question as the reporter-facing form needs it: its current
///     revision, bilingual, with a group's children nested inside it. Carries
///     no authoring-only field (no <c>HasBeenAnswered</c>, no system/role
///     detail) — see <c>skills/persist-hpac-data</c>: this is purpose-built for
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
	string RevisionId,
	string Type,
	bool IsRequired,
	bool IsPrivate,
	int DisplayOrder,
	string? DependsOnQuestionId,
	string? DependsOnOptionCode,
	bool AllowsReporterAdditions,
	string LabelEn,
	string LabelFr,
	string? HelpTextEn,
	string? HelpTextFr,
	string? PlaceholderEn,
	string? PlaceholderFr,
	IReadOnlyList<OptionView> Options,
	bool ChoicesComeFromLiveList,
	IReadOnlyList<PublicQuestionView> Children)
{
	/// <summary>Flattens a question and its current revision for the public form.</summary>
	/// <param name="question">The question to show.</param>
	/// <param name="optionSet">
	///     The shared set the current revision names, when it names one. Same
	///     live-vs-snapshot rule as the admin screen — see ADR-0063.
	/// </param>
	/// <param name="children">
	///     This question's grouped children, already resolved and ordered.
	///     Empty for anything but a live <see cref="QuestionType.Group" />
	///     question.
	/// </param>
	public static PublicQuestionView Of(
		Question question, OptionSet? optionSet, IReadOnlyList<PublicQuestionView> children)
	{
		ArgumentNullException.ThrowIfNull(question);
		ArgumentNullException.ThrowIfNull(children);

		var revision = question.CurrentRevision;
		var choices = QuestionChoices.For(revision, optionSet);

		return new PublicQuestionView(
			question.Id.Value,
			question.Key,
			revision.Id.Value,
			EnumCode.Of(revision.Type),
			revision.IsRequired,
			revision.IsPrivate,
			revision.DisplayOrder,
			revision.DependsOnQuestionId?.Value,
			revision.DependsOnOptionCode,
			revision.AllowsReporterAdditions,
			revision.LabelEn,
			revision.LabelFr,
			revision.HelpTextEn,
			revision.HelpTextFr,
			revision.PlaceholderEn,
			revision.PlaceholderFr,
			[
				.. choices.Select(option => new OptionView(
					option.Code, option.LabelEn, option.LabelFr, option.SourceItemId?.Value, false))
			],
			QuestionChoices.RendersLiveSet(revision, optionSet),
			children);
	}
}
