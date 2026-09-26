namespace HpacSafety.Core.Features.QuestionBank.Typeform;

/// <summary>
///     What parsing an English/French Typeform pair produced: drafts ready for
///     an Administrator to review and save, fields with no equivalent type, and
///     fields whose branching logic needs manual wiring. Nothing here is
///     persisted — see ADR-0077.
/// </summary>
public sealed record TypeformImportResult(
	IReadOnlyList<ImportedQuestionDraft> Drafts,
	IReadOnlyList<RejectedTypeformField> Rejected,
	IReadOnlyList<PendingTypeformLogic> PendingLogic);

/// <summary>
///     One question as parsed from a Typeform pair, shaped for the ordinary
///     authoring screen — the same review-and-save path a hand-typed question
///     goes through. <see cref="Key" /> is the field's Typeform <c>ref</c>,
///     normalized, so re-importing the same form resolves to the same question
///     (ADR-0071, ADR-0077).
/// </summary>
public sealed record ImportedQuestionDraft(
	string Key,
	QuestionType Type,
	string LabelEn,
	string LabelFr,
	bool FrenchDefaultedToEnglish,
	string? HelpTextEn,
	string? HelpTextFr,
	string? GroupedUnderKey,
	IReadOnlyList<ImportedOption> Options,
	bool IsPrivate = true,
	bool IsRequired = false,
	string? DependsOnKey = null,
	string? DependsOnOptionCode = null,
	bool AllowFutureDates = false);

/// <summary>One choice on an imported draft.</summary>
public sealed record ImportedOption(string Code, string LabelEn, string LabelFr, bool FrenchDefaultedToEnglish);

/// <summary>A Typeform field this system has no question type for. Never silently dropped.</summary>
public sealed record RejectedTypeformField(string Ref, string Title, string TypeformType);

/// <summary>
///     A field whose Typeform branching logic is more than linear flow, and so
///     was not translated into a dependency. An Administrator wires the
///     equivalent condition by hand on the saved question and clears this note.
/// </summary>
public sealed record PendingTypeformLogic(string FieldRef, string FieldTitle, string RawLogicJson);
