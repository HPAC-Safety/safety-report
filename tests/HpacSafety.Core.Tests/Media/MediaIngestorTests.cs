using System.Security.Cryptography;
using System.Text;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// Ingest is where a client-supplied file stops being trusted. The original
/// bytes stay exactly as uploaded — they are the Restricted record — and the
/// derivative a reviewer sees is the stripped one. See docs/data-handling.md.
/// </summary>
public class MediaIngestorTests
{
    private static readonly BlobKey Original = BlobKey.Parse("reports/9f1c/photo.jpg");
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static MediaIngestor Ingestor(
        InMemoryBlobStore store,
        MediaType? sniffed,
        IExifStripper stripper,
        long maxByteSize = 1_000_000) =>
        new(store,
            new StubMediaSniffer(sniffed),
            stripper,
            new MediaPolicy(maxByteSize, MediaType.All),
            new FixedClock(Now));

    [Fact]
    public async Task Given_an_uploaded_photo_When_it_is_ingested_Then_a_derivative_is_written_under_a_different_key()
    {
        // Given
        var store = new InMemoryBlobStore();
        var content = Encoding.ASCII.GetBytes("pretend-jpeg-bytes");
        store.Seed(Original, content);
        var stripper = new RecordingExifStripper();

        // When
        var outcome = await Ingestor(store, MediaType.Jpeg, stripper).IngestAsync(Original, "image/jpeg", CancellationToken.None);

        // Then
        outcome.IsAccepted.ShouldBeTrue();
        outcome.DerivativeKey.ShouldNotBe(Original);
        outcome.DerivativeKey.Value.ShouldBe("stripped/reports/9f1c/photo.jpg");
        stripper.Invocations.ShouldBe(1);
        store.Read(outcome.DerivativeKey).ShouldNotBe(content);
    }

    [Fact]
    public async Task Given_an_uploaded_photo_When_it_is_ingested_Then_the_original_bytes_are_left_untouched()
    {
        // Given
        var store = new InMemoryBlobStore();
        var content = Encoding.ASCII.GetBytes("pretend-jpeg-bytes");
        store.Seed(Original, content);

        // When
        await Ingestor(store, MediaType.Jpeg, new RecordingExifStripper()).IngestAsync(Original, "image/jpeg", CancellationToken.None);

        // Then
        store.Read(Original).ShouldBe(content);
    }

    [Fact]
    public async Task Given_an_uploaded_photo_When_it_is_ingested_Then_the_outcome_carries_the_sniffed_type_size_and_digest()
    {
        // Given
        var store = new InMemoryBlobStore();
        var content = Encoding.ASCII.GetBytes("pretend-jpeg-bytes");
        store.Seed(Original, content);
        var expected = Convert.ToHexStringLower(SHA256.HashData(content));

        // When
        var outcome = await Ingestor(store, MediaType.Jpeg, new RecordingExifStripper()).IngestAsync(Original, "image/jpeg", CancellationToken.None);

        // Then
        outcome.ContentType.ShouldBe(MediaType.Jpeg);
        outcome.ByteSize.ShouldBe(content.Length);
        outcome.Sha256.ShouldBe(expected);
        outcome.StrippedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Given_a_file_claiming_image_jpeg_but_containing_a_png_When_it_is_ingested_Then_it_is_rejected()
    {
        // Given
        var store = new InMemoryBlobStore();
        store.Seed(Original, Encoding.ASCII.GetBytes("pretend-png-bytes"));
        var stripper = new RecordingExifStripper();

        // When
        var outcome = await Ingestor(store, MediaType.Png, stripper).IngestAsync(Original, "image/jpeg", CancellationToken.None);

        // Then
        outcome.IsAccepted.ShouldBeFalse();
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
        stripper.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_rejected_file_When_it_is_ingested_Then_no_derivative_is_written()
    {
        // Given
        var store = new InMemoryBlobStore();
        store.Seed(Original, Encoding.ASCII.GetBytes("this is not an image at all"));

        // When
        var outcome = await Ingestor(store, sniffed: null, new RecordingExifStripper()).IngestAsync(Original, "image/jpeg", CancellationToken.None);

        // Then
        outcome.IsAccepted.ShouldBeFalse();
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
        store.Keys.ShouldBe([Original.Value]);
    }

    [Fact]
    public async Task Given_a_file_over_the_size_limit_When_it_is_ingested_Then_it_is_rejected_before_it_is_decoded()
    {
        // Given
        var store = new InMemoryBlobStore();
        store.Seed(Original, new byte[64]);
        var stripper = new RecordingExifStripper();

        // When
        var outcome = await Ingestor(store, MediaType.Jpeg, stripper, maxByteSize: 32).IngestAsync(Original, "image/jpeg", CancellationToken.None);

        // Then
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
        stripper.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_rejected_file_When_the_outcome_is_inspected_Then_it_exposes_no_derivative_to_show_a_reviewer()
    {
        // Given
        var store = new InMemoryBlobStore();
        store.Seed(Original, Encoding.ASCII.GetBytes("nope"));

        // When
        var outcome = await Ingestor(store, sniffed: null, new RecordingExifStripper()).IngestAsync(Original, "image/jpeg", CancellationToken.None);

        // Then
        Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
