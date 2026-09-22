using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
///     One question of the seeded form, in both locales, exactly as
///     <c>docs/form-spec.md</c> describes it.
/// </summary>
/// <param name="Key">The stable key. Never changes, never re-used.</param>
/// <param name="Type">The question type.</param>
/// <param name="Role">
///     The optional role that projects this answer onto a typed property of the
///     report. See ADR-0016.
/// </param>
/// <param name="IsPrivate">Whether the answer is private redaction context. Immutable after creation.</param>
/// <param name="IsRequired">Whether the form refuses to submit without an answer.</param>
/// <param name="IsSystem">Whether the question may be deleted, deactivated, or retyped.</param>
/// <param name="LabelEn">The English label, the source wording.</param>
/// <param name="LabelFr">The French label, machine-translated and unreviewed.</param>
/// <param name="HelpEn">The English help text, the source wording.</param>
/// <param name="HelpFr">The French help text, machine-translated and unreviewed.</param>
/// <param name="PlaceholderEn">The English placeholder, if any.</param>
/// <param name="PlaceholderFr">The French placeholder, if any.</param>
/// <param name="DependsOnKey">
///     The stable key of the question this one is conditional on, if any. Named by
///     key rather than an id, since a seeded row's identifier is itself derived
///     from the key (see <see cref="SeedIds" />).
/// </param>
/// <param name="DependsOnOptionCode">The required option code, when the parent is a single-select question.</param>
/// <param name="GroupedUnderKey">The stable key of the group question this one displays under, if any.</param>
/// <param name="AllowsReporterAdditions">Whether a reporter's value not on the list is recorded as a new choice.</param>
/// <param name="Options">The option set, in display order. Empty unless the type takes options.</param>
public sealed record SeededQuestion(
	string Key,
	QuestionType Type,
	QuestionRole Role,
	bool IsPrivate,
	bool IsRequired,
	bool IsSystem,
	string LabelEn,
	string LabelFr,
	string? HelpEn,
	string? HelpFr,
	string? PlaceholderEn,
	string? PlaceholderFr,
	string? DependsOnKey,
	string? DependsOnOptionCode,
	string? GroupedUnderKey,
	bool AllowsReporterAdditions,
	IReadOnlyList<SeededOption> Options);
