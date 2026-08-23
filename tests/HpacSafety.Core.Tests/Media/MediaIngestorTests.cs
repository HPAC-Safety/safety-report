using System.Security.Cryptography;
using System.Text;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// Ingest is where a client-supplied file stops being trusted. Nothing leaves
/// quarantine until this system has decided what it is; the original bytes are
/// then retained exactly as uploaded — they are the private source record — and the
/// derivative a reviewer sees is the stripped one, when there can be one at all.
/// See docs/data-handling.md.
/// </summary>
public class MediaIngestorTests
{
    private const string ReportId = "dQw4w9WgXcQ";

    private static readonly BlobKey Quarantined = BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.jpg");
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
    public async Task Given_a_quarantined_photo_When_it_is_ingested_Then_the_original_and_the_derivative_are_promoted()
    {
        // Given
        var store = new InMemoryBlobStore();
        var content = Encoding.ASCII.GetBytes("pretend-jpeg-bytes");
        store.Seed(Quarantined, content);
        var stripper = new RecordingExifStripper();

        // When
        var outcome = await Ingestor(store, MediaType.Jpeg, stripper).IngestAsync(Quarantined, "image/jpeg", CancellationToken.None);

        // Then
        outcome.Status.ShouldBe(MediaIngestStatus.Stripped);
        outcome.OriginalKey.Value.ShouldBe("dQw4w9WgXcQ/original/photo.jpg");
        outcome.DerivativeKey.Value.ShouldBe("dQw4w9WgXcQ/stripped/photo.jpg");
        store.Read(outcome.OriginalKey).ShouldBe(content);
        store.Read(outcome.DerivativeKey).ShouldNotBe(content);
        stripper.Invocations.ShouldBe(1);
    }

    [Fact]
    public async Task Given_a_quarantined_photo_When_it_is_ingested_Then_the_outcome_carries_the_sniffed_type_size_and_digest()
    {
        // Given
        var store = new InMemoryBlobStore();
        var content = Encoding.ASCII.GetBytes("pretend-jpeg-bytes");
        store.Seed(Quarantined, content);
        var expected = Convert.ToHexStringLower(SHA256.HashData(content));

        // When
        var outcome = await Ingestor(store, MediaType.Jpeg, new RecordingExifStripper()).IngestAsync(Quarantined, "image/jpeg", CancellationToken.None);

        // Then
        outcome.ContentType.ShouldBe(MediaType.Jpeg);
        outcome.ByteSize.ShouldBe(content.Length);
        outcome.Sha256.ShouldBe(expected);
        outcome.StrippedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task Given_a_video_When_it_is_ingested_Then_it_is_retained_but_nothing_is_viewable()
    {
        // Given
        var store = new InMemoryBlobStore();
        var content = Encoding.ASCII.GetBytes("pretend-mp4-bytes");
        var quarantined = BlobKey.For(ReportId, MediaCompartment.Quarantine, "clip.mp4");
        store.Seed(quarantined, content);
        var stripper = new RecordingExifStripper();

        // When
        var outcome = await Ingestor(store, MediaType.Mp4, stripper).IngestAsync(quarantined, "video/mp4", CancellationToken.None);

        // Then
        outcome.Status.ShouldBe(MediaIngestStatus.AwaitingStripping);
        outcome.IsAccepted.ShouldBeTrue();
        outcome.AwaitsStripping.ShouldBeTrue();
        outcome.IsViewable.ShouldBeFalse();
        store.Read(outcome.OriginalKey).ShouldBe(content);
        stripper.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_video_When_a_derivative_is_asked_for_Then_it_fails_closed_rather_than_returning_the_original()
    {
        // Given
        var store = new InMemoryBlobStore();
        var quarantined = BlobKey.For(ReportId, MediaCompartment.Quarantine, "clip.mp4");
        store.Seed(quarantined, Encoding.ASCII.GetBytes("pretend-mp4-bytes"));

        // When
        var outcome = await Ingestor(store, MediaType.Mp4, new RecordingExifStripper()).IngestAsync(quarantined, "video/mp4", CancellationToken.None);

        // Then
        // The failure that must never happen is falling through to the unstripped
        // original. See #65.
        Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
        store.Keys.ShouldNotContain("dQw4w9WgXcQ/stripped/clip.mp4");
    }

    [Fact]
    public async Task Given_a_file_claiming_image_jpeg_but_containing_a_png_When_it_is_ingested_Then_it_is_rejected()
    {
        // Given
        var store = new InMemoryBlobStore();
        store.Seed(Quarantined, Encoding.ASCII.GetBytes("pretend-png-bytes"));
        var stripper = new RecordingExifStripper();

        // When
        var outcome = await Ingestor(store, MediaType.Png, stripper).IngestAsync(Quarantined, "image/jpeg", CancellationToken.None);

        // Then
        outcome.Status.ShouldBe(MediaIngestStatus.Rejected);
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
        stripper.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task Given_a_rejected_file_When_it_is_ingested_Then_nothing_is_promoted_out_of_quarantine()
    {
        // Given
        var store = new InMemoryBlobStore();
        store.Seed(Quarantined, Encoding.ASCII.GetBytes("this is not an image at all"));

        // When
        var outcome = await Ingestor(store, sniffed: null, new RecordingExifStripper()).IngestAsync(Quarantined, "image/jpeg", CancellationToken.None);

        // Then
        // The bytes stay where the browser put them and expire on their own. No
        // delete exists, deliberately — see ADR-0026.
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
        store.Keys.ShouldBe([Quarantined.Value]);
        Should.Throw<DomainRuleViolationException>(() => outcome.OriginalKey);
        Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
    }

    [Fact]
    public async Task Given_a_file_far_larger_than_the_limit_When_it_is_ingested_Then_the_source_is_never_pulled_fully_into_memory_before_rejection()
    {
        // Given
        // 500 MB against a 1 KB limit - if the whole object were buffered before
        // the size were checked, this test would allocate half a gigabyte to
        // prove the bug exists. It never should, which is the point.
        const long maxByteSize = 1_000;
        var source = new SyntheticOversizedStream(500 * 1024 * 1024);
        var store = new SingleStreamBlobStore(source);
        var ingestor = new MediaIngestor(
            store,
            new StubMediaSniffer(MediaType.Jpeg),
            new RecordingExifStripper(),
            new MediaPolicy(maxByteSize, MediaType.All),
            new FixedClock(Now));

        // When
        var outcome = await ingestor.IngestAsync(Quarantined, "image/jpeg", CancellationToken.None);

        // Then
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);

        // The bound is generous on purpose - any reasonable streaming
        // implementation reads in chunks no larger than a few hundred KB, so
        // stopping within a few megabytes of the configured limit proves the
        // rest of a 500 MB object was never requested. A naive
        // "download everything, then check Length" implementation would have
        // served the full 500 MB here.
        source.TotalBytesServed.ShouldBeLessThan(maxByteSize + (4 * 1024 * 1024));
    }

    [Fact]
    public async Task Given_a_file_over_the_size_limit_When_it_is_ingested_Then_it_is_rejected_before_it_is_decoded()
    {
        // Given
        var store = new InMemoryBlobStore();
        store.Seed(Quarantined, new byte[64]);
        var stripper = new RecordingExifStripper();

        // When
        var outcome = await Ingestor(store, MediaType.Jpeg, stripper, maxByteSize: 32).IngestAsync(Quarantined, "image/jpeg", CancellationToken.None);

        // Then
        outcome.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
        stripper.Invocations.ShouldBe(0);
        store.Keys.ShouldBe([Quarantined.Value]);
    }

    [Fact]
    public async Task Given_a_key_outside_quarantine_When_ingest_is_asked_to_read_it_Then_it_refuses()
    {
        // Given
        var store = new InMemoryBlobStore();
        var original = BlobKey.For(ReportId, MediaCompartment.Original, "photo.jpg");
        store.Seed(original, Encoding.ASCII.GetBytes("pretend-jpeg-bytes"));

        // When / Then
        // Ingest reads unverified bytes and nothing else. Pointing it at a
        // report's private source record would re-run stripping over a file that has
        // already been accepted, which is not what this is for.
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => Ingestor(store, MediaType.Jpeg, new RecordingExifStripper()).IngestAsync(original, "image/jpeg", CancellationToken.None));
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
