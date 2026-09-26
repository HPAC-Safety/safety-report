namespace HpacSafety.Core.Features.PrivateNotes;

/// <summary>
///     One version of a private note's text, with who wrote it and when
///     (ADR-0133). Immutable: an edit adds a revision and never changes one.
/// </summary>
public class PrivateNoteRevision
{
	// For EF Core only; see ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private PrivateNoteRevision()
	{
	}
#pragma warning restore CS8618

	private PrivateNoteRevision(TinyId noteId,
								int number,
								string text,
								string authorSubject,
								DateTimeOffset at)
	{
		Id = TinyId.New();
		NoteId = noteId;
		Number = number;
		Text = text;
		AuthorSubject = authorSubject;
		CreatedAt = at;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The note this is a version of.</summary>
	public TinyId NoteId { get; private init; }

	/// <summary>1 for the first text, one more for each edit.</summary>
	public int Number { get; private init; }

	/// <summary>The text exactly as the reviewer typed it, trimmed.</summary>
	public string Text { get; private init; }

	/// <summary>
	///     Who wrote this revision, as their token's subject. Opaque, and not a
	///     key: there is no user table (ADR-0065).
	/// </summary>
	public string AuthorSubject { get; private init; }

	/// <summary>When it was written.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>When its note was removed, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	internal static PrivateNoteRevision Write(TinyId noteId,
											  int number,
											  string text,
											  string authorSubject,
											  DateTimeOffset at)
	{
		return new PrivateNoteRevision(noteId, number, text, authorSubject, at);
	}

	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;
	}
}
