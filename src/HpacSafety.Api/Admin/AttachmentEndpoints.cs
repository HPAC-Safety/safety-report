using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.Admin;

/// <summary>
///     A reviewer's ways to see an uploaded file: a short-lived, inline view of an
///     image or video's stripped derivative (for the lightbox), a forced download
///     of a document's validated private original, or — only while an image or
///     video has no derivative yet — a forced, audited download of its raw
///     original. Neither endpoint proxies bytes through the API or returns a
///     public URL — every one mints a pre-signed GET through
///     <see cref="ReviewerMediaLink" />, the one place allowed to call
///     <see cref="IBlobStore.CreateReadUrl" />/<see cref="IBlobStore.CreateInlineReadUrl" />.
///     See REQ-MED-010, REQ-MED-011, REQ-MED-013, and ADR-0090 for the atomic
///     audit write.
/// </summary>
public static class AttachmentEndpoints
{
	/// <summary>Maps the reviewer attachment endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminAttachments(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/admin/reports/{reportId}/attachments/{attachmentId}")
			.RequireAuthorization(HpacPolicies.Reviewer);

		group.MapGet("/view", ViewAsync);
		group.MapGet("/download", DownloadAsync);
		group.MapGet("/original", OriginalAsync);
		group.MapPost("/hide", Hide);
		group.MapPost("/show", Show);

		return group;
	}

	/// <summary>
	///     An inline, short-lived link to an image or video's stripped derivative,
	///     for the lightbox (issue #427 decision 10). Still audited, at most 15
	///     minutes. 404 for a document (use <see cref="DownloadAsync" />) and for a
	///     file with no derivative yet — still processing, or failed (use
	///     <see cref="OriginalAsync" />) — REQ-MED-013.
	/// </summary>
	private static async Task<IResult> ViewAsync(
		string reportId,
		string attachmentId,
		HttpContext context,
		HpacSafetyDbContext database,
		ReviewerMediaLink links,
		IOptions<HpacAuthenticationOptions> authOptions,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		var file = await LoadAccessibleFileAsync(reportId, attachmentId, database, cancellationToken).ConfigureAwait(false);

		if (file is null)
		{
			return Results.NotFound();
		}

		if (file.Kind is not (AttachmentKind.Image or AttachmentKind.Video))
		{
			return Problem("Use the download endpoint for a document.");
		}

		if (file.ProcessingErrorCode is not null
			|| file.AwaitsStripping)
		{
			// A failed or still-processing file has no derivative to view inline;
			// use /original instead (decision 15). Refusing here, before minting a
			// URL, means no audit row records a view that never actually happened.
			return Results.NotFound();
		}

		Uri url;

		try
		{
			var contentType = MediaType.TryParse(file.ContentType, out var original) && original.DerivativeForm is { } derived
				? derived.ContentType
				: file.ContentType;

			url = await links.CreateInlineViewUrl(
				file.ViewableKey, contentType, BlobUrlLifetime.Maximum, cancellationToken).ConfigureAwait(false);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		return await IssueAsync(file, url, DownloadFileName(file, derivative: true), context, database, authOptions, clock, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	///     A forced, audited download of the raw original of an image or video
	///     that has no stripped derivative yet — still processing, or failed
	///     (issue #427 decision 15, widening ADR-0094 from a failed video to any
	///     image or video with no derivative). 404 once a derivative exists (use
	///     <see cref="ViewAsync" /> instead) and for a document (use
	///     <see cref="DownloadAsync" />).
	/// </summary>
	private static async Task<IResult> OriginalAsync(
		string reportId,
		string attachmentId,
		HttpContext context,
		HpacSafetyDbContext database,
		ReviewerMediaLink links,
		IOptions<HpacAuthenticationOptions> authOptions,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		var file = await LoadAccessibleFileAsync(reportId, attachmentId, database, cancellationToken).ConfigureAwait(false);

		if (file is null)
		{
			return Results.NotFound();
		}

		if (file.Kind is not (AttachmentKind.Image or AttachmentKind.Video))
		{
			return Problem("Use the download endpoint for a document.");
		}

		if (file.ProcessingErrorCode is null
			&& !file.AwaitsStripping)
		{
			// A derivative exists — the caller should have used /view instead.
			return Results.NotFound();
		}

		Uri url;
		var hasDerivative = file.ProcessingErrorCode is null && !file.AwaitsStripping;

		try
		{
			url = await links.CreateOriginalMediaDownloadUrl(
				BlobKey.Parse(file.BlobKey), file.Kind, hasDerivative, DownloadFileName(file, derivative: false), BlobUrlLifetime.Maximum, cancellationToken).ConfigureAwait(false);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		return await IssueAsync(file, url, DownloadFileName(file, derivative: false), AuditAction.DownloadedOriginalMedia, context, database, authOptions, clock, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<IResult> DownloadAsync(
		string reportId,
		string attachmentId,
		HttpContext context,
		HpacSafetyDbContext database,
		ReviewerMediaLink links,
		IOptions<HpacAuthenticationOptions> authOptions,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		var file = await LoadAccessibleFileAsync(reportId, attachmentId, database, cancellationToken).ConfigureAwait(false);

		if (file is null)
		{
			return Results.NotFound();
		}

		if (file.Kind is not AttachmentKind.Document)
		{
			return Problem("Use the view endpoint for an image or video.");
		}

		if (file.ProcessingErrorCode is not null)
		{
			return Results.NotFound();
		}

		Uri url;

		try
		{
			url = await links.CreateDocumentDownloadUrl(
				BlobKey.Parse(file.BlobKey), file.Kind, DownloadFileName(file, derivative: false), BlobUrlLifetime.Maximum, cancellationToken).ConfigureAwait(false);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		return await IssueAsync(file, url, DownloadFileName(file, derivative: false), context, database, authOptions, clock, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	///     A reviewer hides a file from the published report (REQ-MED-030,
	///     REQ-MED-040). Audited; the bytes are untouched. Hiding a file already
	///     hidden changes nothing and records nothing.
	/// </summary>
	private static Task<IResult> Hide(string reportId,
									  string attachmentId,
									  HttpContext context,
									  HpacSafetyDbContext database,
									  TimeProvider clock,
									  CancellationToken cancellationToken)
	{
		return ChangeVisibility(reportId, attachmentId, context, database, clock, AuditAction.HidMedia,
			(file, subject, at) => file.HideBy(subject, at), cancellationToken);
	}

	/// <summary>A reviewer shows a hidden file again (REQ-MED-030, REQ-MED-040). Audited.</summary>
	private static Task<IResult> Show(string reportId,
									  string attachmentId,
									  HttpContext context,
									  HpacSafetyDbContext database,
									  TimeProvider clock,
									  CancellationToken cancellationToken)
	{
		return ChangeVisibility(reportId, attachmentId, context, database, clock, AuditAction.ShowedMedia,
			(file, _, _) => file.Show(), cancellationToken);
	}

	private static async Task<IResult> ChangeVisibility(string reportId,
														string attachmentId,
														HttpContext context,
														HpacSafetyDbContext database,
														TimeProvider clock,
														AuditAction action,
														Func<ReportFile, string, DateTimeOffset, bool> change,
														CancellationToken cancellationToken)
	{
		var file = await LoadAccessibleFileAsync(reportId, attachmentId, database, cancellationToken).ConfigureAwait(false);

		if (file is null)
		{
			return Results.NotFound();
		}

		var subject = MemberRoles.SubjectOf(context.User);

		if (subject is null)
		{
			return Results.Forbid();
		}

		// Every kind of file can be hidden (ADR-0119), and a deleted one was
		// never loaded, so the change cannot be refused here.
		var at = clock.GetUtcNow();
		var changed = change(file, subject, at);

		if (changed)
		{
			database.AuditLog.Add(new AuditLogEntry(subject, action, "ReportFile", file.Id, at));
			await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}

		return Results.NoContent();
	}

	/// <summary>
	///     Writes the audit row and only then discloses the URL. If the audit write
	///     fails, the caller sees a failure and never receives a URL for a view that
	///     has no record of having happened — the atomicity ADR-0090 requires, with
	///     "the action" being disclosure of the link itself.
	/// </summary>
	private static Task<IResult> IssueAsync(
		ReportFile file,
		Uri url,
		string fileName,
		HttpContext context,
		HpacSafetyDbContext database,
		IOptions<HpacAuthenticationOptions> authOptions,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		return IssueAsync(file, url, fileName, AuditAction.ViewedAttachment, context, database, authOptions, clock, cancellationToken);
	}

	private static async Task<IResult> IssueAsync(
		ReportFile file,
		Uri url,
		string fileName,
		AuditAction action,
		HttpContext context,
		HpacSafetyDbContext database,
		IOptions<HpacAuthenticationOptions> authOptions,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		var identity = MemberRoles.IdentityOf(context.User, authOptions.Value.RoleClaimType);

		if (identity is null)
		{
			return Results.Forbid();
		}

		database.AuditLog.Add(new AuditLogEntry(
			identity.Subject, action, "ReportFile", file.Id, clock.GetUtcNow()));

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

		return Results.Ok(new AttachmentLinkResponse(
			url.ToString(), clock.GetUtcNow().Add(BlobUrlLifetime.Maximum), fileName));
	}

	private static Task<ReportFile?> LoadAccessibleFileAsync(
		string reportId,
		string attachmentId,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(reportId, out var parsedReportId)
			|| !TinyId.TryParse(attachmentId, out var parsedAttachmentId))
		{
			return Task.FromResult<ReportFile?>(null);
		}

		return database.ReportFiles
			.FirstOrDefaultAsync(f => f.Id == parsedAttachmentId && f.ReportId == parsedReportId, cancellationToken);
	}

	/// <summary>
	///     A stable, server-minted name — never the client-supplied filename
	///     (REQ-MED-003), and never the private original's blob key.
	/// </summary>
	/// <summary>
	///     The reporter's sanitized name with the extension of the bytes served —
	///     the stripped derivative's type for a view, the original's for a download —
	///     or the file id when the reporter's name is unknown (ADR-0097).
	/// </summary>
	private static string DownloadFileName(ReportFile file,
										   bool derivative)
	{
		if (!MediaType.TryParse(file.ContentType, out var original))
		{
			return $"{file.Id}.bin";
		}

		var served = derivative && original.DerivativeForm is { } derived ? derived : original;
		return AttachmentFileName.ForDownload(file.OriginalFileName, file.Id, served);
	}

	private static IResult Problem(string detail)
	{
		return Results.Problem(
			title: "That attachment cannot be issued a link.",
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: "https://hpac.ca/problems/attachment-link");
	}
}

/// <summary>A short-lived link to a reviewer-facing attachment.</summary>
/// <param name="Url">The pre-signed, forced-download URL.</param>
/// <param name="ExpiresAt">When the URL stops working.</param>
/// <param name="FileName">The name the download will save as — the reporter's, sanitized, or a server-minted one.</param>
public sealed record AttachmentLinkResponse(string Url, DateTimeOffset ExpiresAt, string FileName);
