using System.Security.Cryptography;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Storage;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
/// The guarantees that are specific to the local store: nothing it hands out is
/// a plain path, and a signed URL is bound to its operation and its clock as
/// well as its key.
/// </summary>
public sealed class FileSystemBlobStoreTests : IDisposable
{
    private static readonly BlobKey Photo = BlobKey.Parse("reports/9f1c8a/photo.jpg");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "hpac-blob-tests", Guid.NewGuid().ToString("n"));
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero));
    private readonly FileSystemBlobStore _store;

    public FileSystemBlobStoreTests() =>
        _store = new FileSystemBlobStore(
            new FileSystemBlobStoreOptions
            {
                RootPath = _root,
                SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            },
            _clock);

    [Fact]
    public async Task Given_an_upload_url_When_it_is_issued_Then_it_is_not_an_http_url_anything_could_serve()
    {
        // Given / When
        var url = await _store.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        url.Scheme.ShouldBe(FileSystemBlobStore.UrlScheme);
        url.Query.ShouldContain("expires=");
        url.Query.ShouldContain("sig=");
    }

    [Fact]
    public async Task Given_an_upload_url_When_it_is_presented_as_a_read_url_Then_it_is_refused()
    {
        // Given
        var url = await _store.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When / Then
        await Should.ThrowAsync<PresignedUrlRejectedException>(() => _store.ExecuteReadAsync(url, CancellationToken.None));
    }

    [Fact]
    public async Task Given_an_upload_url_When_its_expiry_is_pushed_out_by_hand_Then_it_is_refused()
    {
        // Given
        var url = await _store.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);
        var tampered = new Uri(url.ToString().Replace(
            $"expires={_clock.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds()}",
            $"expires={_clock.GetUtcNow().AddYears(1).ToUnixTimeSeconds()}",
            StringComparison.Ordinal));

        // When / Then
        using var content = new MemoryStream([1, 2, 3]);
        await Should.ThrowAsync<PresignedUrlRejectedException>(
            () => _store.ExecuteUploadAsync(tampered, content, CancellationToken.None));
    }

    [Fact]
    public async Task Given_an_upload_url_When_it_is_used_after_it_expires_Then_it_is_refused()
    {
        // Given
        var url = await _store.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When
        _clock.Advance(TimeSpan.FromMinutes(6));

        // Then
        using var content = new MemoryStream([1, 2, 3]);
        await Should.ThrowAsync<PresignedUrlRejectedException>(
            () => _store.ExecuteUploadAsync(url, content, CancellationToken.None));
    }

    [Fact]
    public async Task Given_an_upload_url_signed_by_another_store_When_it_is_presented_Then_it_is_refused()
    {
        // Given
        var other = new FileSystemBlobStore(
            new FileSystemBlobStoreOptions
            {
                RootPath = _root,
                SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            },
            _clock);
        var url = await other.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When / Then
        using var content = new MemoryStream([1, 2, 3]);
        await Should.ThrowAsync<PresignedUrlRejectedException>(
            () => _store.ExecuteUploadAsync(url, content, CancellationToken.None));
    }

    [Fact]
    public async Task Given_an_upload_through_a_signed_url_When_the_blob_is_read_Then_the_signed_content_type_was_recorded()
    {
        // Given
        var url = await _store.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When
        using var content = new MemoryStream([1, 2, 3]);
        await _store.ExecuteUploadAsync(url, content, CancellationToken.None);

        // Then
        var recorded = await _store.ReadContentTypeAsync(Photo, CancellationToken.None);
        recorded.ShouldBe(MediaType.Jpeg.ContentType);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

internal sealed class MutableClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
