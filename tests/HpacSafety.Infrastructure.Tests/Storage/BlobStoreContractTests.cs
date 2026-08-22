using System.Text;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Tests.Media;
using ImageMagick;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
/// The contract every <see cref="IBlobStore" /> keeps, run unchanged against
/// MinIO and against the filesystem store.
/// <para>
/// One suite rather than two is the point: a development stand-in that is not
/// held to the production adapter's guarantees is how a guarantee quietly stops
/// being true in the environment people actually run. See ADR-0026 and
/// AGENTS.md.
/// </para>
/// </summary>
public abstract class BlobStoreContractTests : IAsyncLifetime
{
    private static readonly BlobKey Photo = BlobKey.Parse("reports/9f1c8a/photo.jpg");
    private static readonly BlobKey OtherPhoto = BlobKey.Parse("reports/0000ff/other.jpg");

    /// <summary>The store under test.</summary>
    protected IBlobStore Store { get; private set; } = null!;

    /// <summary>Builds the store. Called once the environment it needs is up.</summary>
    protected abstract Task<IBlobStore> CreateStoreAsync();

    /// <summary>
    /// Attempts the upload a pre-signed URL authorises, returning whether the
    /// store accepted it. S3 answers with a status code and the filesystem store
    /// throws; both collapse to the same answer here so the test can be shared.
    /// </summary>
    protected abstract Task<bool> TryUploadAsync(Uri uploadUrl, byte[] content, string contentType);

    /// <summary>Attempts the read a pre-signed URL authorises.</summary>
    protected abstract Task<bool> TryReadAsync(Uri readUrl);

    /// <summary>Points a pre-signed URL at a different key, leaving the signature alone.</summary>
    protected abstract Uri RetargetToKey(Uri url, BlobKey key);

    /// <inheritdoc />
    public virtual async Task InitializeAsync() => Store = await CreateStoreAsync();

    /// <inheritdoc />
    public virtual Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Given_bytes_written_to_a_key_When_they_are_read_back_Then_they_are_unchanged()
    {
        // Given
        var content = Encoding.UTF8.GetBytes("the original bytes, kept exactly as uploaded");
        using var source = new MemoryStream(content);

        // When
        await Store.WriteAsync(Photo, source, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        await using var stored = await Store.OpenReadAsync(Photo, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stored.CopyToAsync(buffer, CancellationToken.None);
        buffer.ToArray().ShouldBe(content);
    }

    [Fact]
    public async Task Given_a_presigned_upload_url_When_it_is_used_for_the_key_it_was_signed_for_Then_the_upload_succeeds()
    {
        // Given
        var url = await Store.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When
        var accepted = await TryUploadAsync(url, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg.ContentType);

        // Then
        accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task Given_a_presigned_upload_url_When_it_is_reused_for_a_different_key_Then_the_upload_is_refused()
    {
        // Given
        var url = await Store.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When
        var retargeted = RetargetToKey(url, OtherPhoto);
        var accepted = await TryUploadAsync(retargeted, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg.ContentType);

        // Then
        // A pre-signed URL is a capability for one object, not a key to the bucket.
        accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task Given_a_presigned_read_url_When_it_is_reused_for_a_different_key_Then_the_read_is_refused()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.JpegWithGpsExif());
        await Store.WriteAsync(OtherPhoto, source, MediaType.Jpeg.ContentType, CancellationToken.None);
        var url = await Store.CreateReadUrlAsync(Photo, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When
        var accepted = await TryReadAsync(RetargetToKey(url, OtherPhoto));

        // Then
        accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task Given_a_lifetime_beyond_the_cap_When_a_read_url_is_requested_Then_it_is_refused()
    {
        // Given
        var lifetime = BlobUrlLifetime.Maximum + TimeSpan.FromMinutes(1);

        // When / Then
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => Store.CreateReadUrlAsync(Photo, lifetime, CancellationToken.None));
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => Store.CreateUploadUrlAsync(Photo, MediaType.Jpeg.ContentType, lifetime, CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_ingested_Then_the_derivative_has_no_location_data()
    {
        // Given
        var original = ExifFixtures.JpegWithGpsExif();
        using var source = new MemoryStream(original);
        await Store.WriteAsync(Photo, source, MediaType.Jpeg.ContentType, CancellationToken.None);

        // The fixture really does carry a location. Without this the assertions
        // below would pass just as happily on a photo that never had one, which
        // is the failure mode that makes a redaction test worthless.
        using (var beforeIngest = new MagickImage(original))
        {
            beforeIngest.GetExifProfile()!.GetValue(ExifTag.GPSLatitude).ShouldNotBeNull();
            beforeIngest.GetExifProfile()!.GetValue(ExifTag.GPSLongitude).ShouldNotBeNull();
        }

        // When
        var outcome = await Ingestor().IngestAsync(Photo, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        outcome.IsAccepted.ShouldBeTrue();

        var derivative = await ReadAllAsync(outcome.DerivativeKey);
        using var stripped = new MagickImage(derivative);

        // No profile at all, so no GPS IFD to read a coordinate out of.
        stripped.GetExifProfile().ShouldBeNull();

        // And at the byte level: no APP1 EXIF segment, and none of the ASCII
        // EXIF actually carried. Asserted on the bytes because a profile parser
        // that silently stopped finding profiles would satisfy the check above.
        derivative.AsSpan().IndexOf("Exif\u0000\u0000"u8).ShouldBe(-1);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CameraMake);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CapturedAt);
    }

    [Fact]
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_ingested_Then_the_original_bytes_are_retained_untouched()
    {
        // Given
        var original = ExifFixtures.JpegWithGpsExif();
        using var source = new MemoryStream(original);
        await Store.WriteAsync(Photo, source, MediaType.Jpeg.ContentType, CancellationToken.None);

        // When
        await Ingestor().IngestAsync(Photo, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        // The Restricted record keeps everything, GPS included; it is the
        // derivative that is safe to look at. See docs/data-handling.md.
        var retained = await ReadAllAsync(Photo);
        retained.ShouldBe(original);
        using var retainedImage = new MagickImage(retained);
        retainedImage.GetExifProfile().ShouldNotBeNull();
    }

    [Fact]
    public async Task Given_a_file_claiming_image_jpeg_but_containing_something_else_When_it_is_ingested_Then_it_is_rejected()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.NotAnImage());
        await Store.WriteAsync(Photo, source, MediaType.Jpeg.ContentType, CancellationToken.None);

        // When
        var outcome = await Ingestor().IngestAsync(Photo, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        outcome.IsAccepted.ShouldBeFalse();
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
    }

    [Fact]
    public async Task Given_a_png_uploaded_as_a_jpeg_When_it_is_ingested_Then_it_is_rejected()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.Png());
        await Store.WriteAsync(Photo, source, MediaType.Jpeg.ContentType, CancellationToken.None);

        // When
        var outcome = await Ingestor().IngestAsync(Photo, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
    }

    [Fact]
    public async Task Given_an_ingested_photo_When_a_reviewer_link_is_requested_for_the_original_Then_it_is_refused()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.JpegWithGpsExif());
        await Store.WriteAsync(Photo, source, MediaType.Jpeg.ContentType, CancellationToken.None);
        var outcome = await Ingestor().IngestAsync(Photo, MediaType.Jpeg.ContentType, CancellationToken.None);
        var links = new ReviewerMediaLink(Store);

        // When
        var derivativeUrl = await links.CreateViewUrlAsync(outcome.DerivativeKey, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        derivativeUrl.ShouldNotBeNull();
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => links.CreateViewUrlAsync(Photo, TimeSpan.FromMinutes(5), CancellationToken.None));
    }

    private MediaIngestor Ingestor() =>
        new(Store,
            new MagickNetMediaSniffer(),
            new MagickNetExifStripper(),
            new MediaPolicy(maxByteSize: 25 * 1024 * 1024, MediaType.All),
            TimeProvider.System);

    private async Task<byte[]> ReadAllAsync(BlobKey key)
    {
        await using var stored = await Store.OpenReadAsync(key, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stored.CopyToAsync(buffer, CancellationToken.None);
        return buffer.ToArray();
    }
}
