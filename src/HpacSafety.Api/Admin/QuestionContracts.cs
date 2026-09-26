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
	bool IsTranslatable,
	bool AllowFutureDates,
	bool IsActive,
	int DisplayOrder,
	string? DependsOnQuestionId,
	string? DependsOnChoiceId,
	string? GroupedUnderQuestionId,
	string LabelEn,
	string LabelFr,
	string? HelpTextEn,
	string? HelpTextFr,
	string? PlaceholderEn,
	string? PlaceholderFr,
	IReadOnlyList<OptionView> Options,
	int ReporterChoicesAwaitingReview,
	bool HasBeenAnswered)
{
	/// <summary>Flattens a question, its current revision, and its live choices for the screen.</summary>
	/// <param name="question">The question to show, with its choices loaded.</param>
	/// <param name="hasBeenAnswered">
	///     Whether any answer references this question. The screen warns before a
	///     save, because a wording edit to an answered question retires it and
	///     creates a new one in its place (ADR-0071) — though a choices-only edit
	///     never does (ADR-0095).
	/// </param>
	/// <param name="bank">
	///     The live questions, so a condition naming a replaced option shows the
	///     option that replaced it — the one the screen offers (ADR-0128).
	/// </param>
	public static QuestionView Of(Question question,
								  bool hasBeenAnswered = false,
								  IReadOnlyCollection<Question>? bank = null)
	{
		ArgumentNullException.ThrowIfNull(question);

		var revision = question.CurrentRevision;

		return new QuestionView(
			question.Id.Value,
			question.Key,
			revision.Id.Value,
			revision.RevisionNumber,
			EnumCode.Of(revision.Type),
			question.IsSystem,
			revision.IsRequired,
			revision.IsPrivate,
			revision.IsTranslatable,
			revision.AllowFutureDates,
			revision.IsActive,
			revision.DisplayOrder,
			(revision.DependsOnQuestionId is { } parentId && bank is not null
				? QuestionDependencies.ParentToday(bank, parentId)?.Id ?? parentId
				: revision.DependsOnQuestionId)?.Value,
			((bank is null ? null : QuestionDependencies.RequiredChoiceToday(bank, revision)?.Id) ?? revision.DependsOnChoiceId)?.Value,
			revision.GroupedUnderQuestionId?.Value,
			revision.LabelEn,
			revision.LabelFr,
			revision.HelpTextEn,
			revision.HelpTextFr,
			revision.PlaceholderEn,
			revision.PlaceholderFr,
			[.. question.Choices.Select(OptionView.Of)],
			question.ReporterChoicesAwaitingReview,
			hasBeenAnswered);
	}
}

/// <summary>One of a question's choices, as the authoring screen edits it.</summary>
/// <param name="Id">The choice's identifier, which answers and conditions name (ADR-0128).</param>
/// <param name="Code">The invariant code the choice is recorded under.</param>
/// <param name="LabelEn">The English wording. Null only on a reporter-added choice typed in French.</param>
/// <param name="LabelFr">The French wording. Null only on a reporter-added choice typed in English.</param>
/// <param name="AddedByReporter">
///     True when a reporter typed this into a type-ahead rather than an
///     administrator writing it — the entries most worth curating. See ADR-0063.
/// </param>
/// <param name="NeedsTranslation">True while one language is missing, waiting for an administrator.</param>
/// <param name="ReporterLocale">The language a reporter typed it in, or null.</param>
/// <param name="Pin">
///     <c>first</c>, <c>last</c>, or <c>none</c>: whether this choice is listed
///     before or after the alphabetical rest, or among them (ADR-0136).
/// </param>
public sealed record OptionView(
	string Id,
	string Code,
	string? LabelEn,
	string? LabelFr,
	bool AddedByReporter,
	bool NeedsTranslation,
	string? ReporterLocale,
	string Pin)
{
	/// <summary>Flattens one choice.</summary>
	public static OptionView Of(QuestionChoice choice)
	{
		ArgumentNullException.ThrowIfNull(choice);

		return new OptionView(
			choice.Id.Value,
			choice.Code, choice.LabelEn, choice.LabelFr, choice.AddedByReporter, choice.NeedsTranslation,
			choice.ReporterLocale?.Code,
			EnumCode.Of(choice.Pin));
	}
}

/// <summary>
///     What an administrator submits to create a question or to save an edit. A
///     change to the question's wording, type, or flags produces a new revision;
///     <see cref="Options" />, the complete list of its choices, is applied in
///     place and never does (ADR-0095). <see cref="IsTranslatable" /> may be left
///     out: a new question then takes its type's default, and an edit that keeps
///     the type keeps the current setting (ADR-0112). So may
///     <see cref="AllowFutureDates" />: a new question then does not allow future
///     dates, and an edit to a date question keeps its setting (ADR-0138).
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
	string? DependsOnChoiceId,
	string? GroupedUnderQuestionId,
	IReadOnlyList<OptionInput>? Options,
	bool? IsTranslatable = null,
	bool? AllowFutureDates = null);

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
/// <param name="LabelEn">
///     The English wording. A new choice needs both languages; a reporter-added
///     one may keep a missing language until an administrator supplies it.
/// </param>
/// <param name="LabelFr">The French wording, under the same rule.</param>
/// <param name="Replace">
///     For a picker option that already exists: true retires it and adds a new
///     option with this wording in its place, so earlier answers keep the old one;
///     false, the default, fixes its wording in place for every answer (ADR-0128).
/// </param>
/// <param name="Pin">
///     <c>first</c> or <c>last</c> to list the choice before or after the
///     alphabetical rest; <c>none</c>, or nothing, to list it among them (ADR-0136).
/// </param>
public sealed record OptionInput(string? Code, string? LabelEn, string? LabelFr, bool Replace = false, string? Pin = null)
{
	/// <summary>The pin this choice is saved with. An unknown code is refused, never guessed.</summary>
	public ChoicePin ResolvedPin =>
		string.IsNullOrWhiteSpace(Pin)
			? ChoicePin.None
			: EnumCode.TryParse(Pin, out ChoicePin pin)
				? pin
				: throw new DomainRuleViolationException($"'{Pin}' is not a choice position. Use first, last, or none.");

	/// <summary>The normalized code this choice is recorded under.</summary>
	public string ResolvedCode => QuestionKey.Normalize(
		!string.IsNullOrWhiteSpace(Code) ? Code : !string.IsNullOrWhiteSpace(LabelEn) ? LabelEn : LabelFr ?? string.Empty);

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
			var wording = string.Join(" and ", clash.Select(pair => $"'{pair.Option.LabelEn ?? pair.Option.LabelFr}'"));
			throw new DomainRuleViolationException(
				$"The choices {wording} are too alike to tell apart. Word one of them differently.");
		}

		return resolved;
	}
}

/// <summary>Every question in the order the administrator arranged them.</summary>
public sealed record ReorderQuestionsRequest(IReadOnlyList<string> QuestionIdsInOrder);
