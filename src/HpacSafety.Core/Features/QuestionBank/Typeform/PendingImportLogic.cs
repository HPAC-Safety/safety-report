namespace HpacSafety.Core.Features.QuestionBank.Typeform;

/// <summary>
///     A note that one imported field's Typeform branching logic needs manual
///     wiring — <see cref="TypeformQuestionMapper" /> never auto-maps a real
///     condition (ADR-0077, amended by ADR-0078). An Administrator reviews the
///     list, wires the equivalent condition by hand on the saved question
///     using the ordinary conditional-question authoring UI, and deletes the
///     note.
/// </summary>
/// <remarks>
///     <b>Deliberately has no <c>Deleted</c> column.</b> Every other
///     application record supports soft deletion (product invariant #8); this
///     is a narrow, argued exception — the same shape as the <c>admin_users</c>
///     drop, on its own facts, not a precedent for anything else. This table
///     holds transient import scratch notes, never report or answer data, and
///     its entire purpose is to disappear once an Administrator has acted on
///     it. Deleting one is an ordinary hard delete.
/// </remarks>
public class PendingImportLogic
{
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property directly. It exists for the ORM and for
	// nothing else. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private PendingImportLogic()
	{
	}
#pragma warning restore CS8618

	private PendingImportLogic(
		TinyId importBatchId,
		string fieldRef,
		string fieldTitle,
		string rawLogicJson,
		DateTimeOffset at)
	{
		Id = TinyId.New();
		ImportBatchId = importBatchId;
		FieldRef = fieldRef;
		FieldTitle = fieldTitle;
		RawLogicJson = rawLogicJson;
		CreatedAt = at;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>
	///     Groups every note produced by the same import call, so the ones
	///     from an old, already-resolved import can be told apart from a
	///     fresh one.
	/// </summary>
	public TinyId ImportBatchId { get; private init; }

	/// <summary>
	///     The Typeform field's <c>ref</c> — stable across a re-import, unlike
	///     the question this note is about, which may not exist in the bank
	///     yet when the note is created.
	/// </summary>
	public string FieldRef { get; private init; }

	/// <summary>The field's title at import time, so the note is readable before the question is saved.</summary>
	public string FieldTitle { get; private init; }

	/// <summary>The Typeform field's raw <c>logic</c> actions, exactly as exported, for a human to read.</summary>
	public string RawLogicJson { get; private init; }

	/// <summary>When this note was recorded.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>Records one field's unmapped branching logic from an import batch.</summary>
	public static PendingImportLogic Create(
		TinyId importBatchId,
		string fieldRef,
		string fieldTitle,
		string rawLogicJson,
		DateTimeOffset at)
	{
		return new PendingImportLogic(importBatchId, fieldRef, fieldTitle, rawLogicJson, at);
	}
}
