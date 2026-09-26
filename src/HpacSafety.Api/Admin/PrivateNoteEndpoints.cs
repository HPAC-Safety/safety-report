using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.PrivateAttachments;
using HpacSafety.Core.Features.PrivateNotes;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HpacSafety.Api.Admin;

/// <summary>
///     Staff-only notes on a report (ADR-0133, REQ-MOD-098..103). A Safety Officer
///     or an Administrator lists, adds, edits, removes, and reads the history of
///     any note on any report that is not deleted. These routes are the only
///     reader of the note tables: no view, public endpoint, or Worker reads them,
///     and writing a note queues no outbox work.
/// </summary>
public static class PrivateNoteEndpoints
{
	/// <summary>Maps the private-note endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminPrivateNotes(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var notes = app.MapGroup("/api/admin/reports/{reportId}/private-notes").RequireAuthorization(HpacPolicies.Reviewer);

		notes.MapGet("/", List);
		notes.MapPost("/", Add);
		notes.MapPut("/{noteId}", Edit);
		notes.MapDelete("/{noteId}", Remove);
		notes.MapGet("/{noteId}/revisions", History);

		return notes;
	}

	/// <summary>A report's live notes, newest first (REQ-MOD-099).</summary>
	private static async Task<IResult> List(string reportId,
											HpacSafetyDbContext database,
											HttpContext context,
											CancellationToken cancellationToken)
	{
		if (await ReportOf(database, reportId, cancellationToken).ConfigureAwait(false) is not { } report)
		{
			return Results.NotFound();
		}

		var notes = await database.PrivateNotes
			.AsNoTracking()
			.Include(note => note.Revisions)
			.Where(note => note.ReportId == report)
			.OrderByDescending(note => note.CreatedAt)
			.ThenByDescending(note => note.Id)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var reader = SubjectOf(context);
		var referred = await Referred(database, notes.Select(note => note.Current.AttachmentId), cancellationToken).ConfigureAwait(false);
		return Results.Ok(notes.ConvertAll(note => View(note, reader, referred)));
	}

	/// <summary>Writes a new note with its first revision (REQ-MOD-099, REQ-MOD-102).</summary>
	private static async Task<IResult> Add(string reportId,
										   WritePrivateNoteRequest request,
										   HpacSafetyDbContext database,
										   TimeProvider clock,
										   HttpContext context,
										   CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (await ReportOf(database, reportId, cancellationToken).ConfigureAwait(false) is not { } report)
		{
			return Results.NotFound();
		}

		var writer = SubjectOf(context);
		PrivateNote note;

		if (await ReferredTo(database, request.AttachmentId, cancellationToken).ConfigureAwait(false) is not { } refersTo)
		{
			return Invalid("That private attachment does not exist.");
		}

		try
		{
			note = PrivateNote.Write(report, writer, request.Text, clock.GetUtcNow(), refersTo.Attachment);
		}
		catch (DomainRuleViolationException refusal)
		{
			return Invalid(refusal.Message);
		}

		database.PrivateNotes.Add(note);
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.Created($"/api/admin/reports/{reportId}/private-notes/{note.Id.Value}", View(note, writer, refersTo.AsLookup()));
	}

	/// <summary>
	///     Adds a revision. One based on a revision that is no longer the latest is
	///     refused with <c>409</c> and saves nothing (REQ-MOD-100).
	/// </summary>
	private static async Task<IResult> Edit(string reportId,
											string noteId,
											EditPrivateNoteRequest request,
											HpacSafetyDbContext database,
											TimeProvider clock,
											HttpContext context,
											CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		var note = await Load(database, reportId, noteId, cancellationToken).ConfigureAwait(false);

		if (note is null)
		{
			return Results.NotFound();
		}

		if (request.Revision is not { } basedOn)
		{
			return Invalid("Say which revision of the note this edit is based on.");
		}

		var writer = SubjectOf(context);

		if (await ReferredTo(database, request.AttachmentId, cancellationToken).ConfigureAwait(false) is not { } refersTo)
		{
			return Invalid("That private attachment does not exist.");
		}

		try
		{
			note.Edit(writer, request.Text, basedOn, clock.GetUtcNow(), refersTo.Attachment);
		}
		catch (StalePrivateNoteException)
		{
			return Stale();
		}
		catch (DomainRuleViolationException refusal)
		{
			return Invalid(refusal.Message);
		}

		try
		{
			await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (DbUpdateException cause) when (cause.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
		{
			// Another reviewer's edit of the same revision committed first.
			return Stale();
		}

		return Results.Ok(View(note, writer, refersTo.AsLookup()));
	}

	/// <summary>
	///     Soft-deletes the note and its revisions, with one content-free audit
	///     entry in the same save (REQ-MOD-101).
	/// </summary>
	private static async Task<IResult> Remove(string reportId,
											  string noteId,
											  HpacSafetyDbContext database,
											  TimeProvider clock,
											  HttpContext context,
											  CancellationToken cancellationToken)
	{
		var note = await Load(database, reportId, noteId, cancellationToken).ConfigureAwait(false);

		if (note is null)
		{
			return Results.NotFound();
		}

		var at = clock.GetUtcNow();
		note.Remove(at);
		database.AuditLog.Add(new AuditLogEntry(SubjectOf(context), AuditAction.RemovedPrivateNote, "PrivateNote", note.Id, at));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}

	/// <summary>Every revision of a live note, oldest first (REQ-MOD-100).</summary>
	private static async Task<IResult> History(string reportId,
											   string noteId,
											   HpacSafetyDbContext database,
											   HttpContext context,
											   CancellationToken cancellationToken)
	{
		var note = await Load(database, reportId, noteId, cancellationToken).ConfigureAwait(false);

		if (note is null)
		{
			return Results.NotFound();
		}

		var reader = SubjectOf(context);
		var referred = await Referred(database, note.Revisions.Select(revision => revision.AttachmentId), cancellationToken).ConfigureAwait(false);
		return Results.Ok(note.Revisions
			.OrderBy(revision => revision.Number)
			.Select(revision => new PrivateNoteRevisionView(
				revision.Number,
				revision.Text,
				revision.AuthorSubject,
				revision.CreatedAt,
				Mine(revision, reader),
				Reference(revision, referred)))
			.ToList());
	}

	/// <summary>The report's key when it exists and is not deleted; the default filter hides deleted reports.</summary>
	private static async Task<TinyId?> ReportOf(HpacSafetyDbContext database,
												string reportId,
												CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(reportId, out var id))
		{
			return null;
		}

		return await database.Reports.AnyAsync(report => report.Id == id, cancellationToken).ConfigureAwait(false)
			? id
			: null;
	}

	/// <summary>A live note on a live report, with its revisions.</summary>
	private static async Task<PrivateNote?> Load(HpacSafetyDbContext database,
												 string reportId,
												 string noteId,
												 CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(noteId, out var id)
			|| await ReportOf(database, reportId, cancellationToken).ConfigureAwait(false) is not { } report)
		{
			return null;
		}

		return await database.PrivateNotes
			.Include(note => note.Revisions)
			.SingleOrDefaultAsync(note => note.Id == id && note.ReportId == report, cancellationToken)
			.ConfigureAwait(false);
	}

	private static PrivateNoteView View(PrivateNote note,
										string reader,
										IReadOnlyDictionary<TinyId, PrivateAttachment> referred)
	{
		var current = note.Current;
		return new PrivateNoteView(
			note.Id.Value,
			current.Text,
			current.Number,
			current.AuthorSubject,
			current.CreatedAt,
			note.CreatedAt,
			current.Number > 1,
			Mine(current, reader),
			Reference(current, referred));
	}

	/// <summary>
	///     The attachment a request names, loaded whatever its state or report so the
	///     domain can refuse one that is removed or elsewhere (ADR-0135). A request
	///     naming none is a reference to nothing; one naming an attachment that does
	///     not exist at all is <see langword="null" />.
	/// </summary>
	private static async Task<Referral?> ReferredTo(HpacSafetyDbContext database,
													string? attachmentId,
													CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(attachmentId))
		{
			return new Referral(null);
		}

		if (!TinyId.TryParse(attachmentId, out var id))
		{
			return null;
		}

		var attachment = await database.PrivateAttachments
			.IgnoreQueryFilters()
			.AsNoTracking()
			.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
			.ConfigureAwait(false);

		return attachment is null ? null : new Referral(attachment);
	}

	/// <summary>
	///     The attachments some revisions refer to, removed ones included, so a
	///     revision whose attachment was removed later can still say what it was.
	/// </summary>
	private static async Task<Dictionary<TinyId, PrivateAttachment>> Referred(HpacSafetyDbContext database,
																			  IEnumerable<TinyId?> attachmentIds,
																			  CancellationToken cancellationToken)
	{
		var ids = attachmentIds.OfType<TinyId>().Distinct().ToList();

		if (ids.Count == 0)
		{
			return [];
		}

		return await database.PrivateAttachments
			.IgnoreQueryFilters()
			.AsNoTracking()
			.Where(attachment => ids.Contains(attachment.Id))
			.ToDictionaryAsync(attachment => attachment.Id, cancellationToken)
			.ConfigureAwait(false);
	}

	private static PrivateNoteAttachmentView? Reference(PrivateNoteRevision revision,
														IReadOnlyDictionary<TinyId, PrivateAttachment> referred)
	{
		return revision.AttachmentId is { } id && referred.TryGetValue(id, out var attachment)
			? new PrivateNoteAttachmentView(attachment.Id.Value, attachment.OriginalFileName, attachment.Deleted is not null)
			: null;
	}

	/// <summary>The attachment a write names, or none.</summary>
	private sealed record Referral(PrivateAttachment? Attachment)
	{
		public Dictionary<TinyId, PrivateAttachment> AsLookup()
		{
			return Attachment is null ? [] : new Dictionary<TinyId, PrivateAttachment> { [Attachment.Id] = Attachment };
		}
	}

	private static bool Mine(PrivateNoteRevision revision,
							 string reader)
	{
		return string.Equals(revision.AuthorSubject, reader, StringComparison.Ordinal);
	}

	private static string SubjectOf(HttpContext context)
	{
		// A validated token should always carry a subject; the review endpoints
		// record a missing one as unknown rather than failing, and so does this.
		return MemberRoles.SubjectOf(context.User) ?? "(unknown)";
	}

	private static IResult Invalid(string detail)
	{
		return Results.Problem(
			title: "That note cannot be saved.",
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: "https://hpac.ca/problems/invalid-private-note");
	}

	private static IResult Stale()
	{
		return Results.Problem(
			title: "This note changed since you opened it.",
			detail: "Another reviewer edited this note. Reload it to see the latest text, then try again.",
			statusCode: StatusCodes.Status409Conflict,
			type: "https://hpac.ca/problems/stale-private-note");
	}
}

/// <summary>A new note's text.</summary>
/// <param name="Text">Plain text, 1 to 4000 characters once trimmed.</param>
/// <param name="AttachmentId">A live private attachment on the same report the note refers to, if any (ADR-0135).</param>
public sealed record WritePrivateNoteRequest(string? Text, string? AttachmentId = null);

/// <summary>A note's new text and the revision it replaces.</summary>
/// <param name="Text">Plain text, 1 to 4000 characters once trimmed.</param>
/// <param name="Revision">The revision number the reviewer was looking at.</param>
/// <param name="AttachmentId">The private attachment the new revision refers to, or none (ADR-0135).</param>
public sealed record EditPrivateNoteRequest(string? Text, int? Revision, string? AttachmentId = null);

/// <summary>The private attachment a note's revision refers to.</summary>
/// <param name="Id">The attachment.</param>
/// <param name="FileName">Its sanitized file name.</param>
/// <param name="Removed">Whether it was removed after the revision referred to it.</param>
public sealed record PrivateNoteAttachmentView(string Id, string FileName, bool Removed);

/// <summary>A note as it reads now, for the report view.</summary>
/// <param name="Id">The note.</param>
/// <param name="Text">Its current text.</param>
/// <param name="Revision">Its current revision number, sent back with an edit.</param>
/// <param name="WrittenBy">The current text's writer, as an opaque token subject.</param>
/// <param name="WrittenAt">When the current text was written.</param>
/// <param name="CreatedAt">When the note was first written.</param>
/// <param name="Edited">Whether it has more than one revision.</param>
/// <param name="IsMine">Whether the reader wrote the current text.</param>
/// <param name="Attachment">The private attachment the current text refers to, if any.</param>
public sealed record PrivateNoteView(
	string Id,
	string Text,
	int Revision,
	string WrittenBy,
	DateTimeOffset WrittenAt,
	DateTimeOffset CreatedAt,
	bool Edited,
	bool IsMine,
	PrivateNoteAttachmentView? Attachment);

/// <summary>One revision in a note's history.</summary>
/// <param name="Number">1 for the first text, one more for each edit.</param>
/// <param name="Text">The text as written then.</param>
/// <param name="WrittenBy">Its writer, as an opaque token subject.</param>
/// <param name="WrittenAt">When it was written.</param>
/// <param name="IsMine">Whether the reader wrote it.</param>
/// <param name="Attachment">The private attachment it referred to, if any.</param>
public sealed record PrivateNoteRevisionView(
	int Number,
	string Text,
	string WrittenBy,
	DateTimeOffset WrittenAt,
	bool IsMine,
	PrivateNoteAttachmentView? Attachment);
