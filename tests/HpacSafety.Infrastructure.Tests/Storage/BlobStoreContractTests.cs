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
/// <c>skills/test-hpac-safety/SKILL.md</c>.
/// </para>
/// </summary>
public abstract class BlobStoreContractTests : IAsyncLifetime
{
    private const string ReportId = "dQw4w9WgXcQ";
    private const string OtherReportId = "kJQP7kiw5Fk";

    // "Exif" followed by two NULs - the APP1 marker introducing an EXIF block.
    private static ReadOnlySpan<byte> ExifApp1Marker => [0x45, 0x78, 0x69, 0x66, 0x00, 0x00];

    private static readonly BlobKey Quarantined = BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.jpg");
    private static readonly BlobKey AnotherReportsUpload = BlobKey.For(OtherReportId, MediaCompartment.Quarantine, "photo.jpg");

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
        await Store.WriteAsync(Quarantined, source, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        (await ReadAllAsync(Quarantined)).ShouldBe(content);
    }

    [Fact]
    public async Task Given_an_upload_slot_When_it_is_issued_Then_the_url_writes_only_into_quarantine()
    {
        // Given
        var slot = new MediaUploadSlot(Store);

        // When
        var upload = await slot.CreateAsync(ReportId, MediaType.Jpeg, TimeSpan.FromMinutes(5), CancellationToken.None);
        var accepted = await TryUploadAsync(upload.Url, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg.ContentType);

        // Then
        accepted.ShouldBeTrue();
        upload.Key.Compartment.ShouldBe(MediaCompartment.Quarantine);
        upload.Key.Value.ShouldStartWith("quarantine/dQw4w9WgXcQ/");
    }

    [Fact]
    public async Task Given_a_presigned_upload_url_When_it_is_reused_for_a_different_key_Then_the_upload_is_refused()
    {
        // Given
        var url = await Store.CreateUploadUrlAsync(Quarantined, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When
        var retargeted = RetargetToKey(url, AnotherReportsUpload);
        var accepted = await TryUploadAsync(retargeted, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg.ContentType);

        // Then
        // A pre-signed URL is a capability for one object, not a key to the
        // bucket - and with the report id in the key, that also means one
        // reporter's slot cannot write into another report's directory.
        accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task Given_a_presigned_read_url_When_it_is_reused_for_a_different_key_Then_the_read_is_refused()
    {
        // Given
        using var source = new MemoryStream(ExifFixtures.JpegWithGpsExif());
        await Store.WriteAsync(AnotherReportsUpload, source, MediaType.Jpeg.ContentType, CancellationToken.None);
        var url = await Store.CreateReadUrlAsync(Quarantined, TimeSpan.FromMinutes(5), CancellationToken.None);

        // When
        var accepted = await TryReadAsync(RetargetToKey(url, AnotherReportsUpload));

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
            () => Store.CreateReadUrlAsync(Quarantined, lifetime, CancellationToken.None));
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => Store.CreateUploadUrlAsync(Quarantined, MediaType.Jpeg.ContentType, lifetime, CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_ingested_Then_the_derivative_has_no_location_data()
    {
        // Given
        var original = ExifFixtures.JpegWithGpsExif();
        await SeedQuarantineAsync(Quarantined, original, MediaType.Jpeg);

        // The fixture really does carry a location. Without this the assertions
        // below would pass just as happily on a photo that never had one, which
        // is the failure mode that makes a redaction test worthless.
        using (var beforeIngest = new MagickImage(original))
        {
            beforeIngest.GetExifProfile()!.GetValue(ExifTag.GPSLatitude).ShouldNotBeNull();
            beforeIngest.GetExifProfile()!.GetValue(ExifTag.GPSLongitude).ShouldNotBeNull();
        }

        // When
        var outcome = await Ingestor().IngestAsync(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        outcome.Status.ShouldBe(MediaIngestStatus.Stripped);

        var derivative = await ReadAllAsync(outcome.DerivativeKey);
        using var stripped = new MagickImage(derivative);

        // No profile at all, so no GPS IFD to read a coordinate out of.
        stripped.GetExifProfile().ShouldBeNull();

        // And at the byte level: no APP1 EXIF segment, and none of the ASCII
        // EXIF actually carried. Asserted on the bytes because a profile parser
        // that silently stopped finding profiles would satisfy the check above.
        derivative.AsSpan().IndexOf(ExifApp1Marker).ShouldBe(-1);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CameraMake);
        Encoding.ASCII.GetString(derivative).ShouldNotContain(ExifFixtures.CapturedAt);
    }

    [Fact]
    public async Task Given_a_heic_photo_with_GPS_EXIF_When_it_is_ingested_Then_the_derivative_is_a_stripped_jpeg()
    {
        // Given
        var key = BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.heic");
        await SeedQuarantineAsync(key, ExifFixtures.HeicWithGpsExif(), MediaType.Heic);

        // When
        var outcome = await Ingestor().IngestAsync(key, MediaType.Heic.ContentType, CancellationToken.None);

        // Then
        outcome.Status.ShouldBe(MediaIngestStatus.Stripped);
        outcome.ContentType.ShouldBe(MediaType.Heic);

        using var stripped = new MagickImage(await ReadAllAsync(outcome.DerivativeKey));
        stripped.Format.ShouldBe(MagickFormat.Jpeg);
        stripped.GetExifProfile().ShouldBeNull();
    }

    [Fact]
    public async Task Given_a_photo_with_GPS_EXIF_When_it_is_ingested_Then_the_original_bytes_are_retained_untouched()
    {
        // Given
        var original = ExifFixtures.JpegWithGpsExif();
        await SeedQuarantineAsync(Quarantined, original, MediaType.Jpeg);

        // When
        var outcome = await Ingestor().IngestAsync(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        // The private source record keeps everything, GPS included; it is the
        // derivative that is safe to look at. See docs/data-handling.md.
        outcome.OriginalKey.Value.ShouldBe("dQw4w9WgXcQ/original/photo.jpg");
        var retained = await ReadAllAsync(outcome.OriginalKey);
        retained.ShouldBe(original);
        using var retainedImage = new MagickImage(retained);
        retainedImage.GetExifProfile().ShouldNotBeNull();
    }

    [Fact]
    public async Task Given_a_video_When_it_is_ingested_Then_it_is_retained_and_no_reviewer_link_can_be_issued()
    {
        // Given
        var key = BlobKey.For(ReportId, MediaCompartment.Quarantine, "clip.mp4");
        await SeedQuarantineAsync(key, ExifFixtures.Mp4(), MediaType.Mp4);

        // When
        var outcome = await Ingestor().IngestAsync(key, MediaType.Mp4.ContentType, CancellationToken.None);

        // Then
        outcome.Status.ShouldBe(MediaIngestStatus.AwaitingStripping);
        (await ReadAllAsync(outcome.OriginalKey)).ShouldBe(ExifFixtures.Mp4());

        // Fails closed: there is nothing to open, rather than a fall-through to
        // the unstripped original. See #65.
        Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => new ReviewerMediaLink(Store).CreateViewUrlAsync(outcome.OriginalKey, TimeSpan.FromMinutes(5), CancellationToken.None));
    }

    [Fact]
    public async Task Given_an_ingested_photo_When_a_reviewer_link_is_requested_Then_only_the_derivative_is_issued()
    {
        // Given
        await SeedQuarantineAsync(Quarantined, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg);
        var outcome = await Ingestor().IngestAsync(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);
        var links = new ReviewerMediaLink(Store);

        // When
        var derivativeUrl = await links.CreateViewUrlAsync(outcome.DerivativeKey, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        derivativeUrl.ShouldNotBeNull();
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => links.CreateViewUrlAsync(outcome.OriginalKey, TimeSpan.FromMinutes(5), CancellationToken.None));
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => links.CreateViewUrlAsync(Quarantined, TimeSpan.FromMinutes(5), CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_file_claiming_image_jpeg_but_containing_something_else_When_it_is_ingested_Then_it_is_rejected()
    {
        // Given
        await SeedQuarantineAsync(Quarantined, ExifFixtures.NotMedia(), MediaType.Jpeg);

        // When
        var outcome = await Ingestor().IngestAsync(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        outcome.Status.ShouldBe(MediaIngestStatus.Rejected);
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
    }

    [Fact]
    public async Task Given_a_png_uploaded_as_a_jpeg_When_it_is_ingested_Then_it_is_rejected()
    {
        // Given
        await SeedQuarantineAsync(Quarantined, ExifFixtures.Png(), MediaType.Jpeg);

        // When
        var outcome = await Ingestor().IngestAsync(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
    }

    [Fact]
    public async Task Given_a_refused_upload_When_it_is_ingested_Then_nothing_is_promoted_out_of_quarantine()
    {
        // Given
        await SeedQuarantineAsync(Quarantined, ExifFixtures.NotMedia(), MediaType.Jpeg);

        // When
        await Ingestor().IngestAsync(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

        // Then
        // The bytes stay where the browser put them, and the bucket lifecycle
        // rule expires them after 24 hours. There is no delete on IBlobStore, on
        // purpose: no code path exists that could later be pointed at a real
        // report's media. See ADR-0026.
        (await ExistsAsync(Quarantined.In(MediaCompartment.Original))).ShouldBeFalse();
        (await ExistsAsync(Quarantined.In(MediaCompartment.Stripped))).ShouldBeFalse();
        (await ExistsAsync(Quarantined)).ShouldBeTrue();
    }

    private MediaIngestor Ingestor() =>
        new(Store,
            MediaSnifferChain.Default(),
            new MagickNetExifStripper(MediaType.All),
            new MediaPolicyOptions().ToPolicy(),
            TimeProvider.System);

    private async Task SeedQuarantineAsync(BlobKey key, byte[] content, MediaType declaredType)
    {
        using var source = new MemoryStream(content);
        await Store.WriteAsync(key, source, declaredType.ContentType, CancellationToken.None);
    }

    private async Task<byte[]> ReadAllAsync(BlobKey key)
    {
        await using var stored = await Store.OpenReadAsync(key, CancellationToken.None);
        using var buffer = new MemoryStream();
        await stored.CopyToAsync(buffer, CancellationToken.None);
        return buffer.ToArray();
    }

    private async Task<bool> ExistsAsync(BlobKey key)
    {
        try
        {
            await using var stored = await Store.OpenReadAsync(key, CancellationToken.None);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (Amazon.S3.AmazonS3Exception)
        {
            return false;
        }
    }
}
