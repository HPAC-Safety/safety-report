using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HpacSafety.Testing;

/// <summary>
///     Sends a synthetic attachment the way the reporter's browser does (ADR-0126):
///     mint an upload through the API, then PUT the bytes straight to storage through
///     the pre-signed URL it returned, with no member credential.
/// </summary>
/// <remarks>Compiled into each test project that uploads, like <see cref="S3Emulator" />.</remarks>
internal static class DirectUpload
{
	private static readonly Uri Uploads = new("/api/v1/uploads", UriKind.Relative);

	// Storage is another host from the API under test; one client serves every PUT.
	private static readonly HttpClient Storage = new();

	/// <summary>Mints an upload for a declaration and returns the raw response.</summary>
	public static Task<HttpResponseMessage> Mint(HttpClient api,
												 string contentType,
												 long byteSize)
	{
		ArgumentNullException.ThrowIfNull(api);

		return api.PostAsJsonAsync(Uploads, new { contentType, byteSize });
	}

	/// <summary>PUTs a body to a pre-signed URL, declaring <paramref name="contentType" />.</summary>
	public static async Task<HttpResponseMessage> Put(Uri uploadUrl,
													  HttpContent body,
													  string contentType,
													  CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(body);

		body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
		using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = body };
		return await Storage.SendAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Mints an upload and sends the bytes to it, failing loudly if either step fails.</summary>
	/// <returns>The upload's id.</returns>
	public static async Task<string> Send(HttpClient api,
										  byte[] bytes,
										  string contentType)
	{
		ArgumentNullException.ThrowIfNull(bytes);

		var (uploadId, uploadUrl) = await MintOrThrow(api, contentType, bytes.Length).ConfigureAwait(false);
		using var put = await Put(uploadUrl, new ByteArrayContent(bytes), contentType).ConfigureAwait(false);
		if (!put.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Storage refused the PUT: {(int)put.StatusCode}.");
		}

		return uploadId;
	}

	/// <summary>Mints an upload, failing loudly unless it is minted.</summary>
	public static async Task<(string UploadId, Uri UploadUrl)> MintOrThrow(HttpClient api,
																		   string contentType,
																		   long byteSize)
	{
		using var response = await Mint(api, contentType, byteSize).ConfigureAwait(false);
		if (response.StatusCode != HttpStatusCode.Created)
		{
			throw new InvalidOperationException(
				$"The upload was not minted: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync().ConfigureAwait(false)}");
		}

		var minted = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
		return (minted.GetProperty("uploadId").GetString()!, new Uri(minted.GetProperty("uploadUrl").GetString()!));
	}
}
