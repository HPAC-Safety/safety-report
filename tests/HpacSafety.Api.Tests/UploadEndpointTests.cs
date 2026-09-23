using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using HpacSafety.Core.Features.Moderation;
using ImageMagick;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     A reporter's attachment uploads the moment it is attached, is validated before
///     anything is stored, and waits in quarantine under an opaque id until a
///     submission claims it (ADR-0096). Against real PostgreSQL and MinIO containers;
///     every file here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class UploadEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Uploads = new("/api/v1/uploads", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenAllowlistedImage_WhenUploaded_ThenCreatedWithOpaqueIdAndStoredInQuarantine()
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);
		using var body = Png();

		// When
		using var response = await reporter.PostAsync(Uploads, body);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		var text = await response.Content.ReadAsStringAsync();
		var upload = JsonSerializer.Deserialize<JsonElement>(text);
		var uploadId = upload.GetProperty("uploadId").GetString()!;
		uploadId.Length.ShouldBe(22);
		upload.GetProperty("kind").GetString().ShouldBe("image");

		// Nothing but the id and the kind: no key, no URL (REQ-SUB-039).
		upload.EnumerateObject().Select(property => property.Name).ShouldBe(["uploadId", "kind"], ignoreOrder: true);
		text.ShouldNotContain("quarantine");

		var stored = await fixture.Storage.GetObjectMetadataAsync(ApiPostgresFixture.BucketName, $"quarantine/{uploadId}");
		stored.Headers.ContentType.ShouldBe("image/png");
	}

	[Fact]
	public async Task GivenEmptyBody_WhenUploaded_ThenRefusedAsEmptyAndNothingIsStored()
	{
		await AssertRefused([], "image/png", "empty");
	}

	[Fact]
	public async Task GivenBytesNoSnifferRecognises_WhenUploaded_ThenRefusedAsUnrecognised()
	{
		await AssertRefused([0x00, 0x13, 0x37, 0x42, 0xFF, 0xFE, 0x01, 0x02], "image/png", "unrecognised_content");
	}

	[Fact]
	public async Task GivenPngDeclaredAsJpeg_WhenUploaded_ThenRefusedAsMismatch()
	{
		using var image = new MagickImage(MagickColors.SkyBlue, 8, 8) { Format = MagickFormat.Png };
		await AssertRefused(image.ToByteArray(), "image/jpeg", "declared_type_mismatch");
	}

	[Fact]
	public async Task GivenFileOneByteOverLimit_WhenUploaded_ThenRefusedAsTooLarge()
	{
		// Given — a 1 KB limit, so "one byte past 50 MB" is proven without 50 MB
		await using var small = _factory.WithWebHostBuilder(builder =>
			builder.UseSetting("HpacSafety:Media:Policy:MaxByteSize", "1024"));
		using var reporter = await SignedInClient.As(small, MemberRole.User);
		var before = await QuarantineCount();

		// When
		using var exact = new ByteArrayContent(new byte[1025]);
		exact.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
		using var response = await reporter.PostAsync(Uploads, exact);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await Reason(response)).ShouldBe("too_large");
		(await QuarantineCount()).ShouldBe(before);
	}

	[Fact]
	public async Task GivenFileExactlyAtLimit_WhenUploaded_ThenAccepted()
	{
		// Given
		await using var small = _factory.WithWebHostBuilder(builder =>
			builder.UseSetting("HpacSafety:Media:Policy:MaxByteSize", "1024"));
		using var reporter = await SignedInClient.As(small, MemberRole.User);
		using var body = new ByteArrayContent(Encoding.ASCII.GetBytes(new string('a', 1024)));
		body.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

		// When
		using var response = await reporter.PostAsync(Uploads, body);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task GivenNoBearerToken_WhenUploaded_ThenRejectedBeforeAnythingIsStored()
	{
		// Given
		using var anonymous = _factory.CreateClient();
		using var body = Png();
		var before = await QuarantineCount();

		// When
		using var response = await anonymous.PostAsync(Uploads, body);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await QuarantineCount()).ShouldBe(before);
	}

	[Fact]
	public async Task GivenUnclaimedUpload_WhenDeleted_ThenEveryVersionIsGoneAndDeletingAgainSucceeds()
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);
		var uploadId = await Upload(reporter);

		// When
		using var first = await reporter.DeleteAsync(new Uri($"/api/v1/uploads/{uploadId}", UriKind.Relative));
		using var second = await reporter.DeleteAsync(new Uri($"/api/v1/uploads/{uploadId}", UriKind.Relative));

		// Then
		first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var versions = await fixture.Storage.ListVersionsAsync(new ListVersionsRequest
		{
			BucketName = ApiPostgresFixture.BucketName,
			Prefix = $"quarantine/{uploadId}",
		});
		(versions.Versions ?? []).ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenMalformedUploadId_WhenDeleted_ThenRejected()
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await reporter.DeleteAsync(new Uri("/api/v1/uploads/not-an-id", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenNoBearerToken_WhenDeleted_ThenRejected()
	{
		// Given
		using var anonymous = _factory.CreateClient();

		// When
		using var response = await anonymous.DeleteAsync(new Uri("/api/v1/uploads/kP3x9QmR2vT8wLb6nYc4Dg", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	private async Task AssertRefused(byte[] bytes,
									 string declaredType,
									 string reason)
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);
		using var body = new ByteArrayContent(bytes);
		body.Headers.ContentType = new MediaTypeHeaderValue(declaredType);
		var before = await QuarantineCount();

		// When
		using var response = await reporter.PostAsync(Uploads, body);

		// Then — refused with a safe reason, and nothing reached the bucket
		// (REQ-SUB-040).
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await Reason(response)).ShouldBe(reason);
		(await QuarantineCount()).ShouldBe(before);
	}

	private static async Task<string?> Reason(HttpResponseMessage response)
	{
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		return problem.GetProperty("reason").GetString();
	}

	private async Task<int> QuarantineCount()
	{
		var listing = await fixture.Storage.ListObjectsV2Async(new ListObjectsV2Request
		{
			BucketName = ApiPostgresFixture.BucketName,
			Prefix = "quarantine/",
		});
		return listing.S3Objects?.Count ?? 0;
	}

	private static async Task<string> Upload(HttpClient reporter)
	{
		using var body = Png();
		using var response = await reporter.PostAsync(Uploads, body);
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		var upload = await response.Content.ReadFromJsonAsync<JsonElement>();
		return upload.GetProperty("uploadId").GetString()!;
	}

	private static ByteArrayContent Png()
	{
		using var image = new MagickImage(MagickColors.SkyBlue, 8, 8) { Format = MagickFormat.Png };
		var body = new ByteArrayContent(image.ToByteArray());
		body.Headers.ContentType = new MediaTypeHeaderValue("image/png");
		return body;
	}
}
