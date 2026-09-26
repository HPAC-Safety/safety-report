using Amazon.Runtime;
using Amazon.S3;
using HpacSafety.Core;
using HpacSafety.Infrastructure.Storage;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
///     Presigning is a local SDK computation — no network call reaches S3 to
///     build the URL — so the forced-download header override is verified
///     directly here rather than only through the emulated-S3 contract suite.
/// </summary>
public sealed class S3BlobStoreTests : IDisposable
{
	private static readonly BlobKey Key = BlobKey.For("dQw4w9WgXcQ", MediaCompartment.Original, "report.pdf");

	private readonly AmazonS3Client _s3 = new(
		new BasicAWSCredentials("test", "test"),
		new AmazonS3Config { ServiceURL = "http://localhost:9000", ForcePathStyle = true, AuthenticationRegion = "ca-central-1" });

	public void Dispose()
	{
		_s3.Dispose();
	}

	[Fact]
	public async Task GivenAReadUrl_WhenCreated_ThenItForcesDownloadUnderTheGivenName()
	{
		// Given
		var store = new S3BlobStore(_s3, new S3BlobStoreOptions { BucketName = "hpac-media" }, TimeProvider.System);

		// When
		var url = await store.CreateReadUrl(Key, "annual-report.pdf", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Then
		// A browser that navigates straight to this URL must download the
		// file, never render it inline — REQ-MED-010/011.
		url.Query.ShouldContain("response-content-disposition=attachment%3B%20filename%3D%22annual-report.pdf%22");
	}

	[Fact]
	public async Task GivenAnInlineReadUrl_WhenCreated_ThenItRendersInlineUnderTheGivenType()
	{
		// Given
		var store = new S3BlobStore(_s3, new S3BlobStoreOptions { BucketName = "hpac-media" }, TimeProvider.System);
		var derivative = BlobKey.For("dQw4w9WgXcQ", MediaCompartment.Stripped, "photo");

		// When
		var url = await store.CreateInlineReadUrl(derivative, "image/jpeg", TimeSpan.FromMinutes(15), CancellationToken.None);

		// Then
		// A published report's page embeds it (ADR-0117), under the type its
		// derivative was verified as, both pinned by the signature.
		url.Query.ShouldContain("response-content-disposition=inline");
		url.Query.ShouldContain("response-content-type=image%2Fjpeg");
		// Fifteen minutes from the start of the current second, signed in whole
		// seconds: never past the cap, and short of it by less than one.
		ExpirySeconds(url).ShouldBeInRange(899, 900);
	}

	[Fact]
	public async Task GivenAnInlineReadUrl_WhenLifetimeIsOverTheCap_ThenRefused()
	{
		// Given
		var store = new S3BlobStore(_s3, new S3BlobStoreOptions { BucketName = "hpac-media" }, TimeProvider.System);

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			store.CreateInlineReadUrl(Key, "image/jpeg", TimeSpan.FromHours(1), CancellationToken.None));
	}

	[Fact]
	public async Task GivenPublicSigner_WhenReadUrlIsCreated_ThenItPointsAtTheHostTheBrowserCanReach()
	{
		// Given
		// In docker-compose the API reaches the S3 server as s3:9000; a browser only
		// as localhost:9000. SigV4 signs the host, so the URL must be signed for
		// the second one.
		using var internalClient = new AmazonS3Client(
			new BasicAWSCredentials("test", "test"),
			new AmazonS3Config { ServiceURL = "http://s3:9000", ForcePathStyle = true, AuthenticationRegion = "ca-central-1" });
		var store = new S3BlobStore(
			internalClient, new S3BlobStoreOptions { BucketName = "hpac-media" }, TimeProvider.System, signer: _s3);

		// When
		var url = await store.CreateReadUrl(Key, "annual-report.pdf", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Then
		url.Host.ShouldBe("localhost");
		url.Port.ShouldBe(9000);
		url.Scheme.ShouldBe("http");
	}

	[Fact]
	public async Task GivenReportMedia_WhenDeleteIsAsked_ThenRefusedBeforeAnyRequest()
	{
		// Given
		var store = new S3BlobStore(_s3, new S3BlobStoreOptions { BucketName = "hpac-media" }, TimeProvider.System);

		// When / Then
		// Nothing listens on localhost:9000 here, so reaching the network would
		// fail differently: the refusal comes first.
		await Should.ThrowAsync<DomainRuleViolationException>(() => store.Delete(Key, CancellationToken.None));
	}

	private static int ExpirySeconds(Uri url)
	{
		var value = url.Query.TrimStart('?').Split('&')
			.Select(pair => pair.Split('=', 2))
			.Single(pair => pair[0] == "X-Amz-Expires")[1];
		return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
	}
}
