using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Api.Admin;

/// <summary>
///     One question as the authoring screen needs it: its stable identity plus
///     every field of its current revision, flattened.
/// </summary>
/// <remarks>
///     Purpose-built for this one screen, per <c>skills/persist-hpac-data</c>. It
///     carries no answer, no report, and no reporter data of any kind — the
///     question bank is form definition, not report content.
/// </remarks>
public sealed record QuestionView(
	string Id,
	string Key,
	string RevisionId,
	int RevisionNumber,
	string Type,
	bool IsSystem,
	bool IsRequired,
	bool IsPrivate,
	bool IsActive,
	int DisplayOrder,
	string? DependsOnQuestionId,
	string? DependsOnOptionCode,
	string? OptionSetId,
	string? GroupedUnderQuestionId,
	bool AllowsReporterAdditions,
	string LabelEn,
	string LabelFr,
	string? HelpTextEn,
	string? HelpTextFr,
	string? PlaceholderEn,
	string? PlaceholderFr,
	IReadOnlyList<OptionView> Options,
	bool ChoicesComeFromLiveList,
	bool HasBeenAnswered)
{
	/// <summary>Flattens a question and its current revision for the screen.</summary>
	/// <param name="question">The question to show.</param>
	/// <param name="optionSet">
	///     The shared set the current revision names, when it names one. An
	///     autocomplete renders the live set rather than its snapshot, so the
	///     authoring screen shows an administrator the same list a reporter would
	///     see — including anything reporters have added. See ADR-0063.
	/// </param>
	/// <param name="hasBeenAnswered">
	///     Whether any answer references this question. The screen warns before a
	///     save, because an edit to an answered question retires it and creates a
	///     new one in its place (ADR-0071).
	/// </param>
	public static QuestionView Of(
		Question question, OptionSet? optionSet = null, bool hasBeenAnswered = false)
	{
		ArgumentNullException.ThrowIfNull(question);

		var revision = question.CurrentRevision;
		var choices = QuestionChoices.For(revision, optionSet);

		return new QuestionView(
			question.Id.Value,
			question.Key,
			revision.Id.Value,
			revision.RevisionNumber,
			EnumCode.Of(revision.Type),
			question.IsSystem,
			revision.IsRequired,
			revision.IsPrivate,
			revision.IsActive,
			revision.DisplayOrder,
			revision.DependsOnQuestionId?.Value,
			revision.DependsOnOptionCode,
			revision.OptionSetId?.Value,
			revision.GroupedUnderQuestionId?.Value,
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
			hasBeenAnswered);
	}
}

/// <summary>One choice on a question revision, in both official languages.</summary>
/// <summary>One choice, in both official languages.</summary>
/// <param name="Code">The invariant code stored against an answer.</param>
/// <param name="LabelEn">The English wording.</param>
/// <param name="LabelFr">The French wording.</param>
/// <param name="SourceItemId">The shared item this came from, if any.</param>
/// <param name="AddedByReporter">
///     True when a reporter typed this into a type-ahead rather than an
///     administrator authoring it — the entries most worth curating. See ADR-0063.
/// </param>
public sealed record OptionView(
	string Code,
	string LabelEn,
	string LabelFr,
	string? SourceItemId,
	bool AddedByReporter);

/// <summary>
///     What an administrator submits to create a question or to save an edit. An
///     edit produces a new revision; nothing here patches a row that exists.
/// </summary>
public sealed record SaveQuestionRequest(
	string? Key,
	string Type,
	string LabelEn,
	string LabelFr,
	string? HelpTextEn,
	string? HelpTextFr,
	string? PlaceholderEn,
	string? PlaceholderFr,
	bool IsRequired,
	bool IsPrivate,
	bool IsActive,
	string? DependsOnQuestionId,
	string? DependsOnOptionCode,
	string? OptionSetId,
	string? GroupedUnderQuestionId,
	bool AllowsReporterAdditions,
	IReadOnlyList<OptionInput>? Options);

/// <summary>
///     One option as authored. An administrator names a choice by its wording
///     only; the code stored against answers is never theirs to invent.
/// </summary>
/// <param name="Code">
///     The code a choice that already exists was recorded under, sent back
///     unchanged so a relabel is never a recode. Null for a new choice, whose
///     code is derived from <paramref name="LabelEn" /> exactly as a
///     reporter-added choice's is (ADR-0063).
/// </param>
/// <param name="LabelEn">The English wording.</param>
/// <param name="LabelFr">The French wording.</param>
public sealed record OptionInput(string? Code, string LabelEn, string LabelFr)
{
	/// <summary>The normalized code this choice is recorded under.</summary>
	public string ResolvedCode => QuestionKey.Normalize(string.IsNullOrWhiteSpace(Code) ? LabelEn : Code);

	/// <summary>
	///     Every option paired with its resolved code, refusing two choices that
	///     would be recorded the same way. The refusal names the wording, because
	///     the administrator never saw a code.
	/// </summary>
	public static IReadOnlyList<(string Code, OptionInput Option)> Resolve(IEnumerable<OptionInput> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var resolved = options.Select(option => (Code: option.ResolvedCode, Option: option)).ToList();

		if (resolved.GroupBy(pair => pair.Code, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1) is { } clash)
		{
			var wording = string.Join(" and ", clash.Select(pair => $"'{pair.Option.LabelEn}'"));
			throw new DomainRuleViolationException(
				$"The choices {wording} are too alike to tell apart. Word one of them differently.");
		}

		return resolved;
	}
}

/// <summary>Every question in the order the administrator arranged them.</summary>
public sealed record ReorderQuestionsRequest(IReadOnlyList<string> QuestionIdsInOrder);

/// <summary>A reusable choice list as the authoring screen needs it.</summary>
public sealed record OptionSetView(
	string Id,
	string Key,
	string NameEn,
	string NameFr,
	IReadOnlyList<OptionView> Items)
{
	/// <summary>Flattens a set and its live items.</summary>
	public static OptionSetView Of(OptionSet set)
	{
		ArgumentNullException.ThrowIfNull(set);

		return new OptionSetView(
			set.Id.Value,
			set.Key,
			set.NameEn,
			set.NameFr,
			[
				.. set.Items.Select(item => new OptionView(
					item.Code, item.LabelEn, item.LabelFr, item.Id.Value, item.AddedByReporter))
			]);
	}
}

/// <summary>What an administrator submits to create or replace a choice list.</summary>
public sealed record SaveOptionSetRequest(
	string? Key,
	string NameEn,
	string NameFr,
	IReadOnlyList<OptionInput> Items);
