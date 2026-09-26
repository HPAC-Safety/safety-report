namespace HpacSafety.Core.Features.PrivateNotes;

/// <summary>
///     A safety officer's or administrator's note on a report (ADR-0133). Only
///     those two roles ever read it: it is never summarized, translated, or
///     published. Its text lives in an ordered list of immutable
///     <see cref="Revisions" />, and the newest is the note. Nothing is ever
///     erased: removing a note soft-deletes it.
/// </summary>
public class PrivateNote
{
	/// <summary>The longest note text staff may write.</summary>
	public const int MaxLength = 4000;

	/// <summary>The longest token subject stored, the same width as every other subject column.</summary>
	public const int SubjectMaxLength = 256;

	private readonly List<PrivateNoteRevision> _revisions = [];

	// For EF Core only; see ADR-0019.
	private PrivateNote()
	{
	}

	private PrivateNote(TinyId reportId,
						DateTimeOffset at)
	{
		Id = TinyId.New();
		ReportId = reportId;
		CreatedAt = at;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The report it is on.</summary>
	public TinyId ReportId { get; private init; }

	/// <summary>When it was first written.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>When it was removed, or its report deleted, if either happened.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>Every version of its text, oldest first.</summary>
	public IReadOnlyList<PrivateNoteRevision> Revisions => _revisions;

	/// <summary>The note as it reads now: the newest revision.</summary>
	public PrivateNoteRevision Current => _revisions.MaxBy(revision => revision.Number)
										  ?? throw new InvalidOperationException("A private note always has a revision.");

	/// <summary>Writes a new note on a report, with its first revision.</summary>
	/// <exception cref="DomainRuleViolationException">The text is blank or too long.</exception>
	public static PrivateNote Write(TinyId reportId,
									string writerSubject,
									string? text,
									DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(writerSubject);

		var note = new PrivateNote(reportId, at);
		note._revisions.Add(PrivateNoteRevision.Write(note.Id, 1, Validated(text), writerSubject, at));
		return note;
	}

	/// <summary>
	///     Replaces the text with a new revision. Every earlier revision is kept.
	///     Any reviewer may edit any note; the revision records who did.
	/// </summary>
	/// <param name="writerSubject">The editing reviewer's token subject.</param>
	/// <param name="text">The new text.</param>
	/// <param name="basedOn">The revision number the reviewer was looking at.</param>
	/// <param name="at">When.</param>
	/// <exception cref="StalePrivateNoteException">Someone else edited it since.</exception>
	/// <exception cref="DomainRuleViolationException">The note was removed, or the text is blank or too long.</exception>
	public PrivateNoteRevision Edit(string writerSubject,
									string? text,
									int basedOn,
									DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(writerSubject);

		if (Deleted is not null)
		{
			throw new DomainRuleViolationException("A removed private note cannot be edited.");
		}

		if (basedOn != Current.Number)
		{
			throw new StalePrivateNoteException();
		}

		var revision = PrivateNoteRevision.Write(Id, Current.Number + 1, Validated(text), writerSubject, at);
		_revisions.Add(revision);
		return revision;
	}

	/// <summary>
	///     Soft-deletes the note and every revision with one time. Removing it
	///     twice keeps the first time. There is no undo.
	/// </summary>
	public void Remove(DateTimeOffset at)
	{
		if (Deleted is not null)
		{
			return;
		}

		Deleted = at;

		foreach (var revision in _revisions)
		{
			revision.Delete(at);
		}
	}

	private static string Validated(string? text)
	{
		var trimmed = text?.Trim() ?? string.Empty;

		if (trimmed.Length == 0)
		{
			throw new DomainRuleViolationException("A private note needs some text.");
		}

		if (trimmed.Length > MaxLength)
		{
			throw new DomainRuleViolationException($"A private note is at most {MaxLength} characters.");
		}

		return trimmed;
	}
}
