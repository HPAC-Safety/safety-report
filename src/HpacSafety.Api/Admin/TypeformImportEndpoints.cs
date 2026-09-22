using System.IO.Compression;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank.Typeform;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
///     Importing the question bank from a Typeform English/French export pair,
///     and exporting it back to the same two-file shape. See ADR-0077, amended
///     by ADR-0078.
/// </summary>
/// <remarks>
///     Import never saves a <see cref="Core.Features.QuestionBank.Question" />
///     by itself — it returns drafts, and an Administrator still reviews and
///     saves each one through the ordinary
///     <see cref="QuestionEndpoints.MapAdminQuestions" /> endpoints. It does
///     persist <see cref="PendingImportLogic" /> rows immediately, so a
///     branching-logic note survives the review session even though the
///     question it is about may not be saved yet.
/// </remarks>
public static class TypeformImportEndpoints
{
	/// <summary>Maps the admin Typeform import endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminTypeformImport(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/admin/typeform").RequireAuthorization(HpacPolicies.Administrator);

		// No antiforgery: this API is bearer-token authenticated, never
		// cookie/form-session authenticated, so there is no CSRF surface for
		// antiforgery to protect — consistent with AGENTS.md's "no CSRF
		// machinery." ASP.NET Core otherwise requires it by default on any
		// endpoint with an IFormFile parameter.
		group.MapPost("/import", Import).DisableAntiforgery();
		group.MapGet("/pending-logic", ListPendingLogic);
		group.MapDelete("/pending-logic/{id}", DeletePendingLogic);
		group.MapGet("/export", Export);

		return group;
	}

	/// <summary>
	///     The live question bank as a zip of an English and a French
	///     Typeform-shaped file, each field carrying an <c>hpac</c> extension
	///     object for everything Typeform has no field for. See
	///     <see cref="TypeformExportBuilder" />.
	/// </summary>
	private static async Task<IResult> Export(HpacSafetyDbContext database, CancellationToken cancellationToken)
	{
		var questions = await QuestionEndpoints.LiveQuestions(database)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);
		var ordered = questions
			.OrderBy(question => question.DisplayOrder)
			.ThenBy(question => question.Key, StringComparer.Ordinal)
			.ToList();
		var sets = await QuestionEndpoints.LiveSets(database, cancellationToken).ConfigureAwait(false);

		var (english, french) = TypeformExportBuilder.Build(ordered, sets);

		using var zipStream = new MemoryStream();

		using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
		{
			await WriteEntry(archive, "form-en.json", english.ToJson(), cancellationToken).ConfigureAwait(false);
			await WriteEntry(archive, "form-fr.json", french.ToJson(), cancellationToken).ConfigureAwait(false);
		}

		return Results.File(zipStream.ToArray(), "application/zip", "question-bank.zip");
	}

	private static async Task WriteEntry(
		ZipArchive archive, string entryName, string contents, CancellationToken cancellationToken)
	{
		var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
		await using var entryStream = entry.Open();
		await using var writer = new StreamWriter(entryStream);
		await writer.WriteAsync(contents.AsMemory(), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	///     Parses the pair and previews the result. Both files are required —
	///     a request missing one is rejected by model binding before this runs.
	/// </summary>
	private static async Task<IResult> Import(
		IFormFile english,
		IFormFile french,
		HpacSafetyDbContext database,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		TypeformImportResult result;

		try
		{
			var englishDocument = await Parse(english, cancellationToken).ConfigureAwait(false);
			var frenchDocument = await Parse(french, cancellationToken).ConfigureAwait(false);
			result = TypeformQuestionMapper.Map(englishDocument, frenchDocument);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem("invalid-typeform-file", "That file is not a Typeform export.", cause.Message);
		}

		var at = clock.GetUtcNow();
		var batchId = TinyId.New();
		var notes = result.PendingLogic
			.Select(entry => PendingImportLogic.Create(batchId, entry.FieldRef, entry.FieldTitle, entry.RawLogicJson, at))
			.ToList();

		database.PendingImportLogic.AddRange(notes);
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.Ok(
			new TypeformImportPreviewResponse(
				[.. result.Drafts.Select(ImportedQuestionDraftView.Of)],
				[.. result.Rejected.Select(RejectedTypeformFieldView.Of)],
				[.. notes.Select(note => note.Id.Value)]));
	}

	private static async Task<TypeformDocument> Parse(IFormFile file, CancellationToken cancellationToken)
	{
		await using var stream = file.OpenReadStream();
		using var reader = new StreamReader(stream);
		var json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

		return TypeformDocument.Parse(json);
	}

	/// <summary>Every unresolved pending-logic note, oldest first.</summary>
	private static async Task<IResult> ListPendingLogic(
		HpacSafetyDbContext database, CancellationToken cancellationToken)
	{
		var notes = await database.PendingImportLogic
			.OrderBy(note => note.CreatedAt)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return Results.Ok(notes.Select(PendingImportLogicView.Of).ToList());
	}

	/// <summary>
	///     Clears a note after an Administrator has wired the equivalent
	///     condition by hand. A real, hard delete — see the class remarks on
	///     <see cref="PendingImportLogic" />.
	/// </summary>
	private static async Task<IResult> DeletePendingLogic(
		string id, HpacSafetyDbContext database, CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(id, out var noteId))
		{
			return Results.NotFound();
		}

		var note = await database.PendingImportLogic
			.FirstOrDefaultAsync(candidate => candidate.Id == noteId, cancellationToken)
			.ConfigureAwait(false);

		if (note is null)
		{
			return Results.NotFound();
		}

		database.PendingImportLogic.Remove(note);
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}

	private static IResult Problem(string code, string title, string detail)
	{
		return Results.Problem(
			title: title,
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: $"https://hpac.ca/problems/{code}");
	}
}
