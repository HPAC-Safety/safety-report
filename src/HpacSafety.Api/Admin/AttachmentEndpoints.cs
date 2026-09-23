using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.Admin;

/// <summary>
///     A reviewer's only two ways to see an uploaded file: a short-lived, forced
///     download of an image or video's stripped derivative, or of a document's
///     validated private original. Neither endpoint proxies bytes through the API
///     or returns a public URL — both mint a pre-signed GET through
///     <see cref="ReviewerMediaLink" />, the one place allowed to call
///     <see cref="IBlobStore.CreateReadUrl" />. See REQ-MED-010, REQ-MED-011,
///     REQ-MED-013, and ADR-0090 for the atomic audit write.
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

		return group;
	}

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
			// A failed or still-processing file is inaccessible to any
			// reviewer — REQ-MED-013. Refusing here, before minting a URL,
			// means no audit row records a view that never actually happened.
			return Results.NotFound();
		}

		Uri url;

		try
		{
			url = await links.CreateViewUrl(
				file.ViewableKey, DownloadFileName(file, derivative: true), BlobUrlLifetime.Maximum, cancellationToken).ConfigureAwait(false);
		}
		catch (DomainRuleViolationException cause)
		{
			return Problem(cause.Message);
		}

		return await IssueAsync(file, url, DownloadFileName(file, derivative: true), context, database, authOptions, clock, cancellationToken).ConfigureAwait(false);
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
	///     Writes the audit row and only then discloses the URL. If the audit write
	///     fails, the caller sees a failure and never receives a URL for a view that
	///     has no record of having happened — the atomicity ADR-0090 requires, with
	///     "the action" being disclosure of the link itself.
	/// </summary>
	private static async Task<IResult> IssueAsync(
		ReportFile file,
		Uri url,
		string fileName,
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
			identity.Subject, AuditAction.ViewedAttachment, "ReportFile", file.Id, clock.GetUtcNow()));

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

		var served = derivative && original.StrippedForm is { } stripped ? stripped : original;
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
