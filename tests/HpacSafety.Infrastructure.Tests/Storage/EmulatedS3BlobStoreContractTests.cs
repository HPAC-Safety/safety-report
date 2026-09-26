using System.Net.Http.Headers;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Containers;
using HpacSafety.Core;
using HpacSafety.Infrastructure.Storage;
using HpacSafety.Testing;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
///     The same contract suite, against a real S3-compatible server in a container
///     (<see cref="S3Emulator" />).
///     <para>
///         Carries the Integration trait, so a machine with no Docker daemon can skip it
///         with <c>--filter "Category!=Integration"</c>. CI runs it.
///     </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class EmulatedS3BlobStoreContractTests : BlobStoreContractTests, IDisposable
{
	private const string BucketName = "hpac-safety-uploads";

	private readonly HttpClient _http = new();
	private readonly IContainer _server = S3Emulator.Build();
	private AmazonS3Client _s3 = null!;

	public void Dispose()
	{
		_http.Dispose();
		_s3?.Dispose();
	}

	public override async Task InitializeAsync()
	{
		await _server.StartAsync();
		_s3 = await S3Emulator.CreateBucket(_server, BucketName);
		await base.InitializeAsync();
	}

	public override async Task DisposeAsync()
	{
		await base.DisposeAsync();
		await _server.DisposeAsync();
	}

	protected override Task<IBlobStore> CreateStore()
	{
		return Task.FromResult<IBlobStore>(new S3BlobStore(_s3, new S3BlobStoreOptions { BucketName = BucketName }, TimeProvider.System));
	}

	protected override async Task<bool> TryRead(Uri readUrl)
	{
		using var response = await _http.GetAsync(readUrl, CancellationToken.None);
		return response.IsSuccessStatusCode;
	}

	protected override Uri RetargetToKey(Uri url,
										 BlobKey key)
	{
		return new UriBuilder(url) { Path = $"/{BucketName}/{key.Value}" }.Uri;
	}

	[Fact]
	public async Task GivenUploadWrittenTwice_WhenDeleted_ThenNoVersionOfItRemains()
	{
		// Given
		var key = BlobKey.ForUpload(UploadId.New());
		foreach (var bytes in new byte[][] { [1], [2] })
		{
			using var source = new MemoryStream(bytes);
			await Store.Write(key, source, "image/jpeg", CancellationToken.None);
		}

		// When
		await Store.Delete(key, CancellationToken.None);

		// Then
		// No delete marker hiding the bytes: every version is gone (ADR-0096).
		var versions = await _s3.ListVersionsAsync(
			new ListVersionsRequest { BucketName = BucketName, Prefix = key.Value },
			CancellationToken.None);
		(versions.Versions ?? []).ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenAccentedReporterFileName_WhenReadUrlIsUsed_ThenDownloadIsForcedUnderThatName()
	{
		// Given
		var key = BlobKey.For("dQw4w9WgXcQ", MediaCompartment.Original, "Xk3jR9pQ2mZ");
		using (var source = new MemoryStream([1, 2, 3]))
		{
			await Store.Write(key, source, "application/pdf", CancellationToken.None);
		}

		// When
		var url = await Store.CreateReadUrl(key, "Rapport d'accident é.pdf", TimeSpan.FromMinutes(5), CancellationToken.None);
		using var response = await _http.GetAsync(url, CancellationToken.None);

		// Then
		response.IsSuccessStatusCode.ShouldBeTrue();
		var disposition = response.Content.Headers.ContentDisposition!;
		disposition.DispositionType.ShouldBe("attachment");
		disposition.FileNameStar.ShouldBe("Rapport d'accident é.pdf");
	}

	[Fact]
	public async Task GivenUploadUrl_WhenBrowserPutsExactlyWhatWasSigned_ThenStoredUnderThatKeyWithThatType()
	{
		// Given
		var key = BlobKey.ForUpload(UploadId.New());
		byte[] content = [1, 2, 3, 4, 5];
		var url = await Store.CreateUploadUrl(key, "application/pdf", content.Length, TimeSpan.FromMinutes(5), CancellationToken.None);

		// When
		using var response = await Put(url, content, "application/pdf");

		// Then
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		(await Store.Describe(key, CancellationToken.None)).ShouldBe(new StoredBlob("application/pdf", content.Length));
	}

	[Theory]
	[InlineData("longer")]
	[InlineData("shorter")]
	[InlineData("type")]
	[InlineData("key")]
	public async Task GivenUploadUrl_WhenPutDiffersFromWhatWasSigned_ThenStorageRefusesAndStoresNothing(string difference)
	{
		// Given
		var key = BlobKey.ForUpload(UploadId.New());
		var elsewhere = BlobKey.ForUpload(UploadId.New());
		var url = await Store.CreateUploadUrl(key, "application/pdf", 5, TimeSpan.FromMinutes(5), CancellationToken.None);

		// When
		using var response = difference switch
		{
			"longer" => await Put(url, [1, 2, 3, 4, 5, 6], "application/pdf"),
			"shorter" => await Put(url, [1, 2, 3, 4], "application/pdf"),
			"type" => await Put(url, [1, 2, 3, 4, 5], "image/png"),
			_ => await Put(RetargetToKey(url, elsewhere), [1, 2, 3, 4, 5], "application/pdf"),
		};

		// Then
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await Store.Describe(key, CancellationToken.None)).ShouldBeNull();
		(await Store.Describe(elsewhere, CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task GivenExpiredUploadUrl_WhenPut_ThenStorageRefusesAndStoresNothing()
	{
		// Given
		var key = BlobKey.ForUpload(UploadId.New());
		var url = await Store.CreateUploadUrl(key, "application/pdf", 3, TimeSpan.FromSeconds(1), CancellationToken.None);
		await Task.Delay(TimeSpan.FromSeconds(2.5));

		// When
		using var response = await Put(url, [1, 2, 3], "application/pdf");

		// Then
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await Store.Describe(key, CancellationToken.None)).ShouldBeNull();
	}

	private async Task<HttpResponseMessage> Put(Uri url,
												byte[] content,
												string contentType)
	{
		var body = new ByteArrayContent(content);
		body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
		using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = body };
		return await _http.SendAsync(request, CancellationToken.None);
	}
}
