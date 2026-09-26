using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.PrivateAttachments;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
///     Staff-only files on a report (ADR-0135, REQ-MOD-107..111, REQ-MED-046..051).
///     A Safety Officer or an Administrator mints an upload, adds (claims) it, lists,
///     downloads, and removes the private attachments of any report that is not
///     deleted. These routes are the only reader of <c>report_private_attachments</c>:
///     no view, public endpoint, report DTO, count, or Worker reads it, and nothing
///     here queues outbox work.
/// </summary>
/// <remarks>
///     The bytes never pass through the API. The browser PUTs them straight to
///     quarantine through a URL <see cref="UploadLink" /> minted, and the claim copies
///     them, inside storage and unchanged, to the report's private compartment. A
///     download is a short-lived pre-signed GET from <see cref="PrivateAttachmentLink" />.
///     Neither a file's name nor its key is ever logged or put in a problem response.
/// </remarks>
public static partial class PrivateAttachmentEndpoints
{
	private const string ProblemType = "https://hpac.ca/problems/private-attachment";

	/// <summary>Maps the private-attachment endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminPrivateAttachments(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var attachments = app.MapGroup("/api/admin/reports/{reportId}/private-attachments")
			.RequireAuthorization(HpacPolicies.Reviewer);

		attachments.MapPost("/uploads", MintUpload);
		attachments.MapPost("/", Add);
		attachments.MapGet("/", List);
		attachments.MapGet("/{attachmentId}/download", Download);
		attachments.MapDelete("/{attachmentId}", Remove);

		return attachments;
	}

	/// <summary>
	///     Mints a pre-signed PUT to quarantine for a declared file of any type up to
	///     the private cap (REQ-MED-046, REQ-MED-047). Writes nothing.
	/// </summary>
	private static async Task<IResult> MintUpload(string reportId,
												  MintPrivateUploadRequest request,
												  HpacSafetyDbContext database,
												  UploadLink uploadLink,
												  PrivateAttachmentPolicy policy,
												  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (await ReportOf(database, reportId, cancellationToken).ConfigureAwait(false) is null)
		{
			return Results.NotFound();
		}

		if (request.ByteSize is not { } byteSize)
		{
			return Invalid("A private upload is declared with its exact byte size.");
		}

		var minted = await uploadLink.MintPrivate(policy, request.ContentType, byteSize, cancellationToken).ConfigureAwait(false);

		if (!minted.IsMinted)
		{
			return Refused(minted.RejectionReason);
		}

		return Results.Created(
			(string?)null,
			new PrivateUploadResponse(minted.UploadId.Value, minted.ContentType!, minted.Url!.ToString(), minted.ExpiresAt));
	}

	/// <summary>
	///     Claims an upload onto the report: copies it, unchanged, into the report's
	///     private compartment, records it, and then erases the quarantine copy
	///     (REQ-MED-048, REQ-MOD-108, REQ-MOD-110).
	/// </summary>
	private static async Task<IResult> Add(string reportId,
										   AddPrivateAttachmentRequest request,
										   HpacSafetyDbContext database,
										   IBlobStore blobStore,
										   PrivateAttachmentPolicy policy,
										   TimeProvider clock,
										   HttpContext context,
										   ILoggerFactory loggerFactory,
										   CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(blobStore);
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentNullException.ThrowIfNull(loggerFactory);

		if (await ReportOf(database, reportId, cancellationToken).ConfigureAwait(false) is not { } report)
		{
			return Results.NotFound();
		}

		if (!UploadId.TryParse(request.UploadId, out var uploadId))
		{
			return Invalid("An upload id is 22 URL-safe characters.");
		}

		var upload = BlobKey.ForUpload(uploadId);

		// Expired, erased, or never sent: indistinguishable, and all the same answer.
		if (await blobStore.Describe(upload, cancellationToken).ConfigureAwait(false) is not { } stored)
		{
			return Invalid("That upload is not in storage. Send the file again.");
		}

		// The PUT was signed for the size judged at mint, so this only fails if the
		// cap was lowered since; the claim still holds the file to today's cap.
		var refusal = policy.JudgeSize(stored.ByteSize);
		if (refusal is not MediaRejectionReason.None)
		{
			return Refused(refusal);
		}

		var id = TinyId.New();
		var key = BlobKey.For(report.Value, MediaCompartment.Private, id.Value);
		PrivateAttachment attachment;

		try
		{
			// Validated before anything is copied, so a refused name or
			// description leaves nothing behind.
			attachment = PrivateAttachment.Add(
				id,
				report,
				key,
				request.FileName,
				PrivateAttachmentPolicy.ContentTypeFor(stored.ContentType),
				stored.ByteSize,
				request.Description,
				SubjectOf(context),
				clock.GetUtcNow());
		}
		catch (DomainRuleViolationException refused)
		{
			return Invalid(refused.Message);
		}

		await blobStore.Copy(upload, key, cancellationToken).ConfigureAwait(false);

		database.PrivateAttachments.Add(attachment);
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		await Release(upload, blobStore, loggerFactory.CreateLogger(typeof(PrivateAttachmentEndpoints).FullName!)).ConfigureAwait(false);

		return Results.Created(
			$"/api/admin/reports/{reportId}/private-attachments/{attachment.Id.Value}",
			View(attachment, SubjectOf(context)));
	}

	/// <summary>A report's live private attachments, newest first (REQ-MOD-108).</summary>
	private static async Task<IResult> List(string reportId,
											HpacSafetyDbContext database,
											HttpContext context,
											CancellationToken cancellationToken)
	{
		if (await ReportOf(database, reportId, cancellationToken).ConfigureAwait(false) is not { } report)
		{
			return Results.NotFound();
		}

		var attachments = await database.PrivateAttachments
			.AsNoTracking()
			.Where(attachment => attachment.ReportId == report)
			.OrderByDescending(attachment => attachment.AddedAt)
			.ThenByDescending(attachment => attachment.Id)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var reader = SubjectOf(context);
		return Results.Ok(attachments.ConvertAll(attachment => View(attachment, reader)));
	}

	/// <summary>
	///     A forced-download pre-signed GET of at most fifteen minutes, under the
	///     sanitized file name. The audit entry is written before the URL is
	///     disclosed, so no download goes unrecorded (REQ-MED-049).
	/// </summary>
	private static async Task<IResult> Download(string reportId,
												string attachmentId,
												HpacSafetyDbContext database,
												PrivateAttachmentLink links,
												TimeProvider clock,
												HttpContext context,
												CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(links);

		var attachment = await Load(database, reportId, attachmentId, cancellationToken).ConfigureAwait(false);

		if (attachment is null)
		{
			return Results.NotFound();
		}

		var url = await links
			.CreateDownloadUrl(BlobKey.Parse(attachment.BlobKey), attachment.OriginalFileName, BlobUrlLifetime.Maximum, cancellationToken)
			.ConfigureAwait(false);

		var at = clock.GetUtcNow();
		database.AuditLog.Add(new AuditLogEntry(SubjectOf(context), AuditAction.DownloadedPrivateAttachment, "PrivateAttachment", attachment.Id, at));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

		return Results.Ok(new AttachmentLinkResponse(url.ToString(), at.Add(BlobUrlLifetime.Maximum), attachment.OriginalFileName));
	}

	/// <summary>
	///     Soft-deletes the row, recording who removed it, with one content-free
	///     audit entry in the same save. The bytes stay in storage (REQ-MOD-109).
	/// </summary>
	private static async Task<IResult> Remove(string reportId,
											  string attachmentId,
											  HpacSafetyDbContext database,
											  TimeProvider clock,
											  HttpContext context,
											  CancellationToken cancellationToken)
	{
		var attachment = await Load(database, reportId, attachmentId, cancellationToken).ConfigureAwait(false);

		if (attachment is null)
		{
			return Results.NotFound();
		}

		var at = clock.GetUtcNow();
		var subject = SubjectOf(context);
		attachment.Remove(subject, at);
		database.AuditLog.Add(new AuditLogEntry(subject, AuditAction.RemovedPrivateAttachment, "PrivateAttachment", attachment.Id, at));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}

	/// <summary>The report's key when it exists and is not deleted; the default filter hides deleted reports.</summary>
	internal static async Task<TinyId?> ReportOf(HpacSafetyDbContext database,
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

	/// <summary>A live private attachment on a live report.</summary>
	private static async Task<PrivateAttachment?> Load(HpacSafetyDbContext database,
													   string reportId,
													   string attachmentId,
													   CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(attachmentId, out var id)
			|| await ReportOf(database, reportId, cancellationToken).ConfigureAwait(false) is not { } report)
		{
			return null;
		}

		return await database.PrivateAttachments
			.SingleOrDefaultAsync(attachment => attachment.Id == id && attachment.ReportId == report, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	///     Erases the claimed upload from quarantine once the row has committed. Best
	///     effort, and not cancellable by the request: the report already holds its
	///     own copy, and an upload this fails to erase is expired by the lifecycle rule.
	/// </summary>
	private static async Task Release(BlobKey upload,
									  IBlobStore blobStore,
									  ILogger logger)
	{
		try
		{
			await blobStore.Delete(upload, CancellationToken.None).ConfigureAwait(false);
		}
#pragma warning disable CA1031 // The lifecycle rule is the backstop; a failed tidy-up must not fail a committed claim.
		catch (Exception cause)
#pragma warning restore CA1031
		{
			// No upload id or key in the message: an id is a capability, and a key is private.
			LogUploadNotReleased(logger, cause.GetType().Name);
		}
	}

	internal static PrivateAttachmentView View(PrivateAttachment attachment,
											   string reader)
	{
		return new PrivateAttachmentView(
			attachment.Id.Value,
			attachment.OriginalFileName,
			attachment.ContentType,
			attachment.ByteSize,
			attachment.Description,
			attachment.AddedBySubject,
			attachment.AddedAt,
			string.Equals(attachment.AddedBySubject, reader, StringComparison.Ordinal));
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
			title: "That private attachment was not accepted.",
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: ProblemType);
	}

	/// <summary>A safe 400 naming only which rule refused the file, never its name, size, or type.</summary>
	private static IResult Refused(MediaRejectionReason reason)
	{
		return Results.Problem(
			title: "That private attachment was not accepted.",
			statusCode: StatusCodes.Status400BadRequest,
			type: ProblemType,
			extensions: new Dictionary<string, object?> { ["reason"] = EnumCode.Of(reason) });
	}

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "A claimed private upload could not be removed from quarantine ({ExceptionType}); the lifecycle rule will expire it.")]
	private static partial void LogUploadNotReleased(ILogger logger,
													 string exceptionType);
}

/// <summary>What the browser declares about a private file it is about to send. Never its name.</summary>
/// <param name="ContentType">The type the browser read from the file, if any.</param>
/// <param name="ByteSize">The file's exact size in bytes.</param>
public sealed record MintPrivateUploadRequest(string? ContentType, long? ByteSize);

/// <summary>A minted private upload.</summary>
/// <param name="UploadId">The opaque id the claim names this file by.</param>
/// <param name="ContentType">The type the PUT is signed for; the browser sends exactly this.</param>
/// <param name="UploadUrl">A pre-signed PUT to the upload's quarantine key, signed for that type and exact size.</param>
/// <param name="ExpiresAt">When <paramref name="UploadUrl" /> stops working.</param>
public sealed record PrivateUploadResponse(string UploadId, string ContentType, string UploadUrl, DateTimeOffset ExpiresAt);

/// <summary>Claims a sent upload onto the report.</summary>
/// <param name="UploadId">The minted upload id.</param>
/// <param name="FileName">The file's name, sanitized before it is stored; required.</param>
/// <param name="Description">Optional plain text, at most 500 characters once trimmed.</param>
public sealed record AddPrivateAttachmentRequest(string? UploadId, string? FileName, string? Description);

/// <summary>A private attachment as the report view lists it.</summary>
/// <param name="Id">The attachment.</param>
/// <param name="FileName">Its sanitized file name, which its download saves as.</param>
/// <param name="ContentType">The type it was uploaded as. Never sniffed.</param>
/// <param name="ByteSize">Its size in bytes.</param>
/// <param name="Description">What the adder wrote about it, if anything.</param>
/// <param name="AddedBy">The adder, as an opaque token subject.</param>
/// <param name="AddedAt">When it was added.</param>
/// <param name="IsMine">Whether the reader added it.</param>
public sealed record PrivateAttachmentView(
	string Id,
	string FileName,
	string ContentType,
	long ByteSize,
	string? Description,
	string AddedBy,
	DateTimeOffset AddedAt,
	bool IsMine);
