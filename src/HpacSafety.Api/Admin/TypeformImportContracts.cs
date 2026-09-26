using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank.Typeform;

namespace HpacSafety.Api.Admin;

/// <summary>What importing a Typeform pair produced, shaped for the authoring screen.</summary>
public sealed record TypeformImportPreviewResponse(
	IReadOnlyList<ImportedQuestionDraftView> Drafts,
	IReadOnlyList<RejectedTypeformFieldView> Rejected,
	IReadOnlyList<string> PendingLogicNoteIds);

/// <summary>One imported question, ready to prefill the ordinary authoring screen.</summary>
public sealed record ImportedQuestionDraftView(
	string Key,
	string Type,
	string LabelEn,
	string LabelFr,
	bool FrenchDefaultedToEnglish,
	string? HelpTextEn,
	string? HelpTextFr,
	string? GroupedUnderKey,
	IReadOnlyList<ImportedOptionView> Options,
	bool IsPrivate,
	bool IsRequired,
	string? DependsOnKey,
	string? DependsOnOptionCode,
	bool AllowFutureDates)
{
	/// <summary>Flattens a draft for the wire, converting its type to the invariant code every other view uses.</summary>
	public static ImportedQuestionDraftView Of(ImportedQuestionDraft draft)
	{
		ArgumentNullException.ThrowIfNull(draft);

		return new ImportedQuestionDraftView(
			draft.Key, EnumCode.Of(draft.Type), draft.LabelEn, draft.LabelFr, draft.FrenchDefaultedToEnglish,
			draft.HelpTextEn, draft.HelpTextFr, draft.GroupedUnderKey,
			[.. draft.Options.Select(ImportedOptionView.Of)], draft.IsPrivate, draft.IsRequired, draft.DependsOnKey,
			draft.DependsOnOptionCode, draft.AllowFutureDates);
	}
}

/// <summary>One choice on an imported draft.</summary>
public sealed record ImportedOptionView(string Code, string LabelEn, string LabelFr, bool FrenchDefaultedToEnglish)
{
	/// <summary>Flattens an option for the wire.</summary>
	public static ImportedOptionView Of(ImportedOption option)
	{
		ArgumentNullException.ThrowIfNull(option);

		return new ImportedOptionView(option.Code, option.LabelEn, option.LabelFr, option.FrenchDefaultedToEnglish);
	}
}

/// <summary>A Typeform field with no equivalent question type — never silently dropped.</summary>
public sealed record RejectedTypeformFieldView(string Ref, string Title, string TypeformType)
{
	/// <summary>Flattens a rejection for the wire.</summary>
	public static RejectedTypeformFieldView Of(RejectedTypeformField field)
	{
		ArgumentNullException.ThrowIfNull(field);

		return new RejectedTypeformFieldView(field.Ref, field.Title, field.TypeformType);
	}
}

/// <summary>One field whose Typeform branching logic still needs an Administrator to wire it by hand.</summary>
public sealed record PendingImportLogicView(string Id, string FieldRef, string FieldTitle, string RawLogicJson, DateTimeOffset CreatedAt)
{
	/// <summary>Flattens a pending-logic note for the wire.</summary>
	public static PendingImportLogicView Of(PendingImportLogic note)
	{
		ArgumentNullException.ThrowIfNull(note);

		return new PendingImportLogicView(note.Id.Value, note.FieldRef, note.FieldTitle, note.RawLogicJson, note.CreatedAt);
	}
}
