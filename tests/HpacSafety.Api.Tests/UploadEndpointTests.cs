using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amazon.S3.Model;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Testing;
using ImageMagick;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     A reporter's attachment is minted the moment it is attached: the API judges the
///     declared type and size, and returns an opaque id and a pre-signed PUT the
///     browser sends the file to, straight into quarantine, until a submission claims
///     it (ADR-0096, ADR-0126). Against real PostgreSQL and S3-compatible containers;
///     every file here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class UploadEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Uploads = new("/api/v1/uploads", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenAllowlistedImage_WhenMinted_ThenCreatedWithOpaqueIdAndPutUrlAndNothingStored()
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await DirectUpload.Mint(reporter, "image/png", 1234);

		// Then
		var text = await response.Content.ReadAsStringAsync();
		response.StatusCode.ShouldBe(HttpStatusCode.Created, text);
		var upload = JsonSerializer.Deserialize<JsonElement>(text);
		var uploadId = upload.GetProperty("uploadId").GetString()!;
		uploadId.Length.ShouldBe(22);
		upload.GetProperty("kind").GetString().ShouldBe("image");
		upload.EnumerateObject().Select(property => property.Name)
			.ShouldBe(["uploadId", "kind", "uploadUrl", "expiresAt"], ignoreOrder: true);

		var url = new Uri(upload.GetProperty("uploadUrl").GetString()!);
		url.AbsolutePath.ShouldEndWith($"/quarantine/{uploadId}");
		url.Query.ShouldContain("X-Amz-Expires=900");
		url.Query.ShouldContain("content-length");
		url.Query.ShouldContain("content-type");
		upload.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(15).AddSeconds(5));

		(await QuarantineHolds(uploadId)).ShouldBeFalse();
	}

	[Fact]
	public async Task GivenMintedUpload_WhenBrowserPutsTheFile_ThenItWaitsInQuarantineUnderDeclaredType()
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);
		var png = PngBytes();

		// When
		var uploadId = await DirectUpload.Send(reporter, png, "image/png");

		// Then
		var stored = await fixture.Storage.GetObjectMetadataAsync(ApiPostgresFixture.BucketName, $"quarantine/{uploadId}");
		stored.Headers.ContentType.ShouldBe("image/png");
		stored.ContentLength.ShouldBe(png.Length);
		stored.Metadata.Keys.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("image/png", 0, "empty")]
	[InlineData("application/x-msdownload", 10, "unaccepted_media_type")]
	[InlineData("video/mp4", (250L * 1024 * 1024) + 1, "too_large")]
	[InlineData("image/jpeg", (25L * 1024 * 1024) + 1, "too_large")]
	[InlineData("application/pdf", (25L * 1024 * 1024) + 1, "too_large")]
	public async Task GivenDeclarationPolicyRefuses_WhenMinted_ThenRefusedWithReasonAndNoUrl(string contentType,
																							 long byteSize,
																							 string reason)
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await DirectUpload.Mint(reporter, contentType, byteSize);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("reason").GetString().ShouldBe(reason);
		problem.TryGetProperty("uploadUrl", out _).ShouldBeFalse();
	}

	[Theory]
	[InlineData("video/mp4", 250L * 1024 * 1024)]
	[InlineData("image/heic", 25L * 1024 * 1024)]
	[InlineData("text/markdown", 25L * 1024 * 1024)]
	public async Task GivenDeclarationExactlyAtItsKindsLimit_WhenMinted_ThenCreated(string contentType,
																				   long byteSize)
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await DirectUpload.Mint(reporter, contentType, byteSize);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task GivenConfiguredPerKindLimit_WhenDeclarationPassesIt_ThenRefusedAsTooLarge()
	{
		// Given — the limits are configuration, one per kind (ADR-0126)
		await using var small = _factory.WithWebHostBuilder(builder =>
			builder.UseSetting("HpacSafety:Media:Policy:MaxDocumentByteSize", "1024"));
		using var reporter = await SignedInClient.As(small, MemberRole.User);

		// When
		using var atLimit = await DirectUpload.Mint(reporter, "text/plain", 1024);
		using var past = await DirectUpload.Mint(reporter, "text/plain", 1025);
		using var image = await DirectUpload.Mint(reporter, "image/png", 1025);

		// Then
		atLimit.StatusCode.ShouldBe(HttpStatusCode.Created);
		past.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		image.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Fact]
	public async Task GivenRetiredSingleSizeSetting_WhenHostStarts_ThenItRefusesToStart()
	{
		// Given
		await using var retired = _factory.WithWebHostBuilder(builder =>
			builder.UseSetting("HpacSafety:Media:Policy:MaxByteSize", "1024"));

		// When / Then
		Should.Throw<InvalidOperationException>(() => retired.CreateClient())
			.Message.ShouldContain("MaxByteSize is retired");
	}

	[Theory]
	[InlineData("text/plain", "not json")]
	[InlineData("application/json", "{\"contentType\":\"image/png\"}")]
	[InlineData("application/json", "{\"contentType\":")]
	public async Task GivenMalformedDeclaration_WhenMinted_ThenRejected(string mediaType,
																		string body)
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);
		using var content = new StringContent(body, Encoding.UTF8, mediaType);

		// When
		using var response = await reporter.PostAsync(Uploads, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenNoBearerToken_WhenMinted_ThenRejected()
	{
		// Given
		using var anonymous = _factory.CreateClient();

		// When
		using var response = await DirectUpload.Mint(anonymous, "image/png", 10);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenUnclaimedUpload_WhenDeleted_ThenEveryVersionIsGoneAndDeletingAgainSucceeds()
	{
		// Given
		using var reporter = await SignedInClient.As(_factory, MemberRole.User);
		var uploadId = await DirectUpload.Send(reporter, PngBytes(), "image/png");

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

	private async Task<bool> QuarantineHolds(string uploadId)
	{
		var listing = await fixture.Storage.ListObjectsV2Async(new ListObjectsV2Request
		{
			BucketName = ApiPostgresFixture.BucketName,
			Prefix = $"quarantine/{uploadId}",
		});
		return (listing.S3Objects?.Count ?? 0) > 0;
	}

	private static byte[] PngBytes()
	{
		using var image = new MagickImage(MagickColors.SkyBlue, 8, 8) { Format = MagickFormat.Png };
		return image.ToByteArray();
	}
}
