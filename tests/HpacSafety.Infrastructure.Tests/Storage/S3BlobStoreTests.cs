using Amazon.Runtime;
using Amazon.S3;
using HpacSafety.Core;
using HpacSafety.Infrastructure.Storage;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
///     Presigning is a local SDK computation — no network call reaches S3 to
///     build the URL — so the forced-download header override is verified
///     directly here rather than only through the MinIO contract suite.
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
	public async Task GivenAReadUrl_WhenCreated_ThenItForcesDownloadUnderTheServerMintedName()
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
}
