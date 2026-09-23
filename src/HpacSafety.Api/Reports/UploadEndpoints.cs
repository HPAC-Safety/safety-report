using HpacSafety.Api.Authentication;
using HpacSafety.Api.RateLimiting;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using Microsoft.AspNetCore.Http.Features;

namespace HpacSafety.Api.Reports;

/// <summary>
///     A reporter's attachment, uploaded the moment it is attached and held in private
///     quarantine until the final submission claims it (ADR-0096).
/// </summary>
/// <remarks>
///     <para>
///         An upload is validated before anything is stored: the body is read into a
///         temporary file, stopping one byte past the size limit, then sniffed and
///         judged. Only an accepted file reaches the bucket, and a cancelled request
///         reaches nothing, because the write happens last.
///     </para>
///     <para>
///         Nothing here records who uploaded. The member token is checked and then
///         forgotten, exactly as submission does (ADR-0067); the upload id is the only
///         handle anybody has on the file.
///     </para>
/// </remarks>
public static class UploadEndpoints
{
	private const int ReadBufferSize = 81920;

	/// <summary>Maps the upload and delete endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAttachmentUploads(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/v1/uploads")
			.RequireAuthorization(HpacPolicies.Member)
			.RequireRateLimiting(RateLimitPolicies.AttachmentUpload);

		group.MapPost("/", Upload);
		group.MapDelete("/{uploadId}", Delete);

		return group;
	}

	private static async Task<IResult> Upload(
		HttpContext context,
		MediaIngestor ingestor,
		MediaPolicy policy,
		IBlobStore blobStore,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(ingestor);
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentNullException.ThrowIfNull(blobStore);

		var request = context.Request;

		if (request.ContentLength > policy.MaxByteSize)
		{
			return Refused(MediaRejectionReason.TooLarge);
		}

		// Kestrel's default body limit is below the attachment limit. Raised to
		// exactly one byte past it, which is all the bounded copy below ever reads.
		if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
		{
			bodySize.MaxRequestBodySize = policy.MaxByteSize + 1;
		}

		var temporaryPath = Path.Combine(Path.GetTempPath(), $"hpac-upload-{UploadId.New().Value}");

		// DeleteOnClose: the temporary copy disappears when this request ends,
		// whether it was accepted, refused, or aborted by the browser.
		await using var copy = new FileStream(
			temporaryPath,
			FileMode.CreateNew,
			FileAccess.ReadWrite,
			FileShare.None,
			ReadBufferSize,
			FileOptions.DeleteOnClose | FileOptions.Asynchronous);

		if (await CopyBounded(request.Body, copy, policy.MaxByteSize, cancellationToken).ConfigureAwait(false))
		{
			return Refused(MediaRejectionReason.TooLarge);
		}

		var verdict = await ingestor.Inspect(copy, request.ContentType, cancellationToken).ConfigureAwait(false);
		if (!verdict.IsAccepted)
		{
			return Refused(verdict.RejectionReason);
		}

		var uploadId = UploadId.New();
		copy.Position = 0;
		await blobStore
			.Write(BlobKey.ForUpload(uploadId), copy, verdict.Type.ContentType, cancellationToken)
			.ConfigureAwait(false);

		return Results.Created(
			(string?)null,
			new UploadResponse(uploadId.Value, EnumCode.Of(verdict.Type.Kind)));
	}

	private static async Task<IResult> Delete(
		string uploadId,
		IBlobStore blobStore,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(blobStore);

		if (!UploadId.TryParse(uploadId, out var parsed))
		{
			return Results.Problem(
				title: "That upload was not recognised.",
				detail: "An upload id is 22 URL-safe characters.",
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/attachment-upload");
		}

		await blobStore.Delete(BlobKey.ForUpload(parsed), cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}

	/// <summary>
	///     Copies at most <paramref name="maxByteSize" /> + 1 bytes, so that a file of
	///     exactly the limit succeeds and one byte more is enough to know it failed.
	/// </summary>
	/// <returns><see langword="true" /> when the body exceeded the limit.</returns>
	private static async Task<bool> CopyBounded(
		Stream source,
		Stream destination,
		long maxByteSize,
		CancellationToken cancellationToken)
	{
		var buffer = new byte[ReadBufferSize];
		long total = 0;

		while (true)
		{
			var toRead = (int)Math.Min(buffer.Length, maxByteSize + 1 - total);

			if (toRead <= 0)
			{
				return true;
			}

			var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);

			if (read == 0)
			{
				await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
				return false;
			}

			total += read;
			await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	///     A safe 400 naming only which rule refused the file. Never the file's name,
	///     bytes, or declared type.
	/// </summary>
	private static IResult Refused(MediaRejectionReason reason)
	{
		return Results.Problem(
			title: "That attachment was not accepted.",
			statusCode: StatusCodes.Status400BadRequest,
			type: "https://hpac.ca/problems/attachment-upload",
			extensions: new Dictionary<string, object?> { ["reason"] = EnumCode.Of(reason) });
	}
}

/// <summary>What a successful upload returns. Nothing else — no name, key, or URL.</summary>
/// <param name="UploadId">The opaque id the final submission names this file by.</param>
/// <param name="Kind"><c>image</c>, <c>video</c>, or <c>document</c>.</param>
public sealed record UploadResponse(string UploadId, string Kind);
