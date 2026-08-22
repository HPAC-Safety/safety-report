using System.Net.Http.Headers;
using Amazon.S3;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Storage;
using Testcontainers.Minio;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
/// The same contract suite, against a real S3-compatible server in a container.
/// MinIO stands in for S3 here because the API is the part under test — the
/// pre-signed signature, the private bucket, the 403 on a retargeted URL — and
/// none of that needs an AWS account or a network.
/// <para>
/// Carries the Integration trait, so a machine with no Docker daemon can skip it
/// with <c>--filter "Category!=Integration"</c>. CI runs it.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class MinioBlobStoreContractTests : BlobStoreContractTests, IDisposable
{
    private const string BucketName = "hpac-safety-uploads";

    // Pinned rather than floating on `latest`, for the same reason the Postgres
    // container is: a server that moves underneath the suite is a failure nobody
    // can reproduce.
    private readonly MinioContainer _minio = new MinioBuilder("minio/minio:RELEASE.2025-04-22T22-12-26Z").Build();

    private readonly HttpClient _http = new();
    private AmazonS3Client _s3 = null!;

    public override async Task InitializeAsync()
    {
        await _minio.StartAsync();

        _s3 = new AmazonS3Client(
            new Amazon.Runtime.BasicAWSCredentials(_minio.GetAccessKey(), _minio.GetSecretKey()),
            new AmazonS3Config
            {
                ServiceURL = _minio.GetConnectionString(),
                ForcePathStyle = true,
                AuthenticationRegion = "ca-central-1",
            });

        // A private bucket, created with no public read policy. Nothing in this
        // system ever adds one. See docs/data-handling.md.
        await _s3.PutBucketAsync(BucketName, CancellationToken.None);

        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _minio.DisposeAsync();
    }

    protected override Task<IBlobStore> CreateStoreAsync() =>
        Task.FromResult<IBlobStore>(new S3BlobStore(_s3, new S3BlobStoreOptions { BucketName = BucketName }, TimeProvider.System));

    protected override async Task<bool> TryUploadAsync(Uri uploadUrl, byte[] content, string contentType)
    {
        using var body = new ByteArrayContent(content);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        using var response = await _http.PutAsync(uploadUrl, body, CancellationToken.None);
        return response.IsSuccessStatusCode;
    }

    protected override async Task<bool> TryReadAsync(Uri readUrl)
    {
        using var response = await _http.GetAsync(readUrl, CancellationToken.None);
        return response.IsSuccessStatusCode;
    }

    protected override Uri RetargetToKey(Uri url, BlobKey key) =>
        new UriBuilder(url) { Path = $"/{BucketName}/{key.Value}" }.Uri;

    public void Dispose()
    {
        _http.Dispose();
        _s3?.Dispose();
    }
}
