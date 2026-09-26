using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Api.RateLimiting;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Api.Reports;

/// <summary>
///     A reporter's attachment, minted the moment it is attached and sent by the
///     browser straight to private quarantine, where it waits until the final
///     submission claims it (ADR-0096, ADR-0126).
/// </summary>
/// <remarks>
///     <para>
///         The API never holds an attachment's bytes. It judges what the browser
///         declares — type and exact size — and mints an opaque upload id and a
///         short-lived pre-signed PUT signed for exactly that. Storage refuses any
///         other type or length. The bytes are sniffed and validated when a
///         submission claims them.
///     </para>
///     <para>
///         Nothing here records who uploaded. The member token is checked and then
///         forgotten, exactly as submission does (ADR-0067); the upload id is the only
///         handle anybody has on the file.
///     </para>
/// </remarks>
public static class UploadEndpoints
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>Maps the upload and delete endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAttachmentUploads(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/v1/uploads")
			.RequireAuthorization(HpacPolicies.Member)
			.RequireRateLimiting(RateLimitPolicies.AttachmentUpload);

		group.MapPost("/", Mint);
		group.MapDelete("/{uploadId}", Delete);

		return group;
	}

	private static async Task<IResult> Mint(
		HttpRequest request,
		UploadLink uploadLink,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(uploadLink);

		if (!request.HasJsonContentType())
		{
			return Malformed();
		}

		var declaration = await ReadDeclaration(request, cancellationToken).ConfigureAwait(false);
		if (declaration is not { ByteSize: { } byteSize })
		{
			return Malformed();
		}

		var minted = await uploadLink.Mint(declaration.ContentType, byteSize, cancellationToken).ConfigureAwait(false);
		if (!minted.IsMinted)
		{
			return Refused(minted.RejectionReason);
		}

		return Results.Created(
			(string?)null,
			new UploadResponse(minted.UploadId.Value, EnumCode.Of(minted.Kind), minted.Url!.ToString(), minted.ExpiresAt));
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

	private static async Task<UploadRequest?> ReadDeclaration(HttpRequest request,
															  CancellationToken cancellationToken)
	{
		try
		{
			return await JsonSerializer
				.DeserializeAsync<UploadRequest>(request.Body, JsonOptions, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static IResult Malformed()
	{
		return Results.Problem(
			title: "That attachment was not accepted.",
			detail: "An upload is declared as JSON with its content type and exact byte size.",
			statusCode: StatusCodes.Status400BadRequest,
			type: "https://hpac.ca/problems/attachment-upload");
	}

	/// <summary>
	///     A safe 400 naming only which rule refused the file. Never the file's name,
	///     size, or declared type.
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

/// <summary>
///     What the browser declares about a file it is about to send. Never its name:
///     that travels only with the final submission (ADR-0097).
/// </summary>
/// <param name="ContentType">The type the browser read from the file.</param>
/// <param name="ByteSize">The file's exact size in bytes.</param>
public sealed record UploadRequest(string? ContentType, long? ByteSize);

/// <summary>What a minted upload returns. No name, key, or report id.</summary>
/// <param name="UploadId">The opaque id the final submission names this file by.</param>
/// <param name="Kind"><c>image</c>, <c>video</c>, or <c>document</c>, as declared.</param>
/// <param name="UploadUrl">
///     A pre-signed PUT to this upload's quarantine key, signed for the declared
///     <c>Content-Type</c> and exact <c>Content-Length</c>.
/// </param>
/// <param name="ExpiresAt">When <paramref name="UploadUrl" /> stops working.</param>
public sealed record UploadResponse(string UploadId, string Kind, string UploadUrl, DateTimeOffset ExpiresAt);
