using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
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
		return Results.Ok(notes.ConvertAll(note => View(note, reader)));
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

		try
		{
			note = PrivateNote.Write(report, writer, request.Text, clock.GetUtcNow());
		}
		catch (DomainRuleViolationException refusal)
		{
			return Invalid(refusal.Message);
		}

		database.PrivateNotes.Add(note);
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.Created($"/api/admin/reports/{reportId}/private-notes/{note.Id.Value}", View(note, writer));
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

		try
		{
			note.Edit(writer, request.Text, basedOn, clock.GetUtcNow());
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

		return Results.Ok(View(note, writer));
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
		return Results.Ok(note.Revisions
			.OrderBy(revision => revision.Number)
			.Select(revision => new PrivateNoteRevisionView(
				revision.Number,
				revision.Text,
				revision.AuthorSubject,
				revision.CreatedAt,
				Mine(revision, reader)))
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
										string reader)
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
			Mine(current, reader));
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
public sealed record WritePrivateNoteRequest(string? Text);

/// <summary>A note's new text and the revision it replaces.</summary>
/// <param name="Text">Plain text, 1 to 4000 characters once trimmed.</param>
/// <param name="Revision">The revision number the reviewer was looking at.</param>
public sealed record EditPrivateNoteRequest(string? Text, int? Revision);

/// <summary>A note as it reads now, for the report view.</summary>
/// <param name="Id">The note.</param>
/// <param name="Text">Its current text.</param>
/// <param name="Revision">Its current revision number, sent back with an edit.</param>
/// <param name="WrittenBy">The current text's writer, as an opaque token subject.</param>
/// <param name="WrittenAt">When the current text was written.</param>
/// <param name="CreatedAt">When the note was first written.</param>
/// <param name="Edited">Whether it has more than one revision.</param>
/// <param name="IsMine">Whether the reader wrote the current text.</param>
public sealed record PrivateNoteView(
	string Id,
	string Text,
	int Revision,
	string WrittenBy,
	DateTimeOffset WrittenAt,
	DateTimeOffset CreatedAt,
	bool Edited,
	bool IsMine);

/// <summary>One revision in a note's history.</summary>
/// <param name="Number">1 for the first text, one more for each edit.</param>
/// <param name="Text">The text as written then.</param>
/// <param name="WrittenBy">Its writer, as an opaque token subject.</param>
/// <param name="WrittenAt">When it was written.</param>
/// <param name="IsMine">Whether the reader wrote it.</param>
public sealed record PrivateNoteRevisionView(
	int Number,
	string Text,
	string WrittenBy,
	DateTimeOffset WrittenAt,
	bool IsMine);
