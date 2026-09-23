using System.Security.Cryptography;
using System.Text;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     Ingest is where a client-supplied file stops being trusted. Nothing leaves
///     quarantine until this system has decided what it is; the original bytes are
///     then retained exactly as uploaded — they are the private source record — and the
///     derivative a reviewer sees is the stripped one, when there can be one at all.
///     See docs/data-handling.md.
/// </summary>
public class MediaIngestorTests
{
	private const string ReportId = "dQw4w9WgXcQ";
	private const string FileIdValue = "kJQP7kiw5Fk";

	private static readonly TinyId Report = TinyId.Parse(ReportId);
	private static readonly TinyId FileId = TinyId.Parse(FileIdValue);
	private static readonly BlobKey Quarantined = BlobKey.ForUpload(UploadId.New());
	private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

	private static MediaIngestor Ingestor(
		InMemoryBlobStore store,
		MediaType? sniffed,
		IExifStripper stripper,
		long maxByteSize = 1_000_000,
		IVideoRemuxer? remuxer = null)
	{
		return new MediaIngestor(store,
			new StubMediaSniffer(sniffed),
			stripper,
			remuxer ?? new RecordingVideoRemuxer(),
			new MediaPolicy(maxByteSize, MediaType.All),
			new FixedClock(Now));
	}

	[Fact]
	public async Task GivenQuarantinedPhoto_WhenIngested_ThenOriginalAndDerivativeArePromoted()
	{
		// Given
		var store = new InMemoryBlobStore();
		var content = Encoding.ASCII.GetBytes("pretend-jpeg-bytes");
		store.Seed(Quarantined, content);
		var stripper = new RecordingExifStripper();

		// When
		var outcome = await Ingestor(store, MediaType.Jpeg, stripper).Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.Stripped);
		outcome.OriginalKey.Value.ShouldBe("dQw4w9WgXcQ/original/kJQP7kiw5Fk");
		outcome.DerivativeKey.Value.ShouldBe("dQw4w9WgXcQ/stripped/kJQP7kiw5Fk");
		store.Read(outcome.OriginalKey).ShouldBe(content);
		store.Read(outcome.DerivativeKey).ShouldNotBe(content);
		stripper.Invocations.ShouldBe(1);
	}

	[Fact]
	public async Task GivenQuarantinedVideo_WhenRemuxed_ThenDerivativeIsPromotedAndIsNotTheOriginal()
	{
		// Given — REQ-MED-007: a video gets a derivative, and never a copy of itself
		var store = new InMemoryBlobStore();
		var content = Encoding.ASCII.GetBytes("pretend-mp4-bytes");
		store.Seed(Quarantined, content);
		var remuxer = new RecordingVideoRemuxer();

		// When
		var outcome = await Ingestor(store, MediaType.Mp4, new RecordingExifStripper(), remuxer: remuxer)
			.Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.Stripped);
		store.Read(outcome.OriginalKey).ShouldBe(content);
		store.Read(outcome.DerivativeKey).ShouldNotBe(content);
		remuxer.Invocations.ShouldBe(1);
	}

	[Fact]
	public async Task GivenVideoThatCannotBeRemuxed_WhenIngested_ThenOriginalIsRetainedWithoutDerivative()
	{
		// Given — REQ-MED-015: the reporter does not lose their footage because
		// our toolchain could not clean the container
		var store = new InMemoryBlobStore();
		var content = Encoding.ASCII.GetBytes("pretend-mov-bytes");
		store.Seed(Quarantined, content);

		// When
		var outcome = await Ingestor(
				store, MediaType.QuickTime, new RecordingExifStripper(), remuxer: new RecordingVideoRemuxer(false))
			.Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then — accepted and kept, with nothing a reviewer may be shown inline
		outcome.Status.ShouldBe(MediaIngestStatus.AwaitingStripping);
		outcome.IsAccepted.ShouldBeTrue();
		store.Read(outcome.OriginalKey).ShouldBe(content);
		Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
	}

	[Fact]
	public async Task GivenVideoThatCannotBeRemuxed_WhenIngested_ThenNothingIsWrittenToTheStrippedCompartment()
	{
		// Given — a partial write must never be mistaken for a derivative
		var store = new InMemoryBlobStore();
		store.Seed(Quarantined, Encoding.ASCII.GetBytes("pretend-mov-bytes"));

		// When
		await Ingestor(
				store, MediaType.QuickTime, new RecordingExifStripper(), remuxer: new RecordingVideoRemuxer(false))
			.Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		store.Keys.ShouldNotContain(key => key.Contains("/stripped/", StringComparison.Ordinal));
	}

	[Fact]
	public async Task GivenQuarantinedVideo_WhenIngested_ThenTheImageStripperIsNeverAsked()
	{
		// Given — ADR-0025 and ADR-0094: Magick.NET must not touch video
		var store = new InMemoryBlobStore();
		store.Seed(Quarantined, Encoding.ASCII.GetBytes("pretend-mp4-bytes"));
		var stripper = new RecordingExifStripper();

		// When
		await Ingestor(store, MediaType.Mp4, stripper).Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		stripper.Invocations.ShouldBe(0);
	}

	[Fact]
	public async Task GivenQuarantinedPhoto_WhenIngested_ThenOutcomeCarriesSniffedTypeSizeAndDigest()
	{
		// Given
		var store = new InMemoryBlobStore();
		var content = Encoding.ASCII.GetBytes("pretend-jpeg-bytes");
		store.Seed(Quarantined, content);
		var expected = Convert.ToHexStringLower(SHA256.HashData(content));

		// When
		var outcome = await Ingestor(store, MediaType.Jpeg, new RecordingExifStripper()).Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.ContentType.ShouldBe(MediaType.Jpeg);
		outcome.ByteSize.ShouldBe(content.Length);
		outcome.Sha256.ShouldBe(expected);
		outcome.StrippedAt.ShouldBe(Now);
	}

	[Fact]
	public async Task GivenVideoThatCannotBeRemuxed_WhenIngested_ThenRetainedButNothingIsViewable()
	{
		// Given — until ADR-0094 no video had a derivative; now only one that
		// cannot be remuxed is retained this way (REQ-MED-015)
		var store = new InMemoryBlobStore();
		var content = Encoding.ASCII.GetBytes("pretend-mp4-bytes");
		var quarantined = BlobKey.ForUpload(UploadId.New());
		store.Seed(quarantined, content);
		var stripper = new RecordingExifStripper();

		// When
		var outcome = await Ingestor(store, MediaType.Mp4, stripper, remuxer: new RecordingVideoRemuxer(false))
			.Ingest(quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.AwaitingStripping);
		outcome.IsAccepted.ShouldBeTrue();
		outcome.AwaitsStripping.ShouldBeTrue();
		outcome.IsViewable.ShouldBeFalse();
		store.Read(outcome.OriginalKey).ShouldBe(content);
		stripper.Invocations.ShouldBe(0);
	}

	[Fact]
	public async Task GivenVideoThatCannotBeRemuxed_WhenDerivativeIsAskedFor_ThenFailsClosedRatherThanReturningOriginal()
	{
		// Given — a retained video keeps its metadata, so the one thing that must
		// never happen is falling through to it as though it were a derivative
		var store = new InMemoryBlobStore();
		var quarantined = BlobKey.ForUpload(UploadId.New());
		store.Seed(quarantined, Encoding.ASCII.GetBytes("pretend-mp4-bytes"));

		// When
		var outcome = await Ingestor(
				store, MediaType.Mp4, new RecordingExifStripper(), remuxer: new RecordingVideoRemuxer(false))
			.Ingest(quarantined, Report, FileId, CancellationToken.None);

		// Then
		Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
		store.Keys.ShouldNotContain("dQw4w9WgXcQ/stripped/kJQP7kiw5Fk");
	}

	[Fact]
	public async Task GivenDocument_WhenIngested_ThenRetainedUnchangedWithNoDerivative()
	{
		// Given
		var store = new InMemoryBlobStore();
		var content = Encoding.ASCII.GetBytes("pretend-pdf-bytes");
		var quarantined = BlobKey.ForUpload(UploadId.New());
		store.Seed(quarantined, content);
		var stripper = new RecordingExifStripper();

		// When
		var outcome = await Ingestor(store, MediaType.Pdf, stripper).Ingest(quarantined, Report, FileId, CancellationToken.None);

		// Then
		// A document has no derivative at all — it is validated and kept
		// private, byte-for-byte, as the reporter-supplied original. See #310.
		outcome.Status.ShouldBe(MediaIngestStatus.AwaitingStripping);
		outcome.IsAccepted.ShouldBeTrue();
		outcome.IsViewable.ShouldBeFalse();
		store.Read(outcome.OriginalKey).ShouldBe(content);
		stripper.Invocations.ShouldBe(0);
	}

	[Fact]
	public async Task GivenFileClaimingImageJpegButContainingPng_WhenInspected_ThenRejected()
	{
		// Given
		using var upload = new MemoryStream(Encoding.ASCII.GetBytes("pretend-png-bytes"));
		var stripper = new RecordingExifStripper();

		// When
		var verdict = await Ingestor(new InMemoryBlobStore(), MediaType.Png, stripper)
			.Inspect(upload, "image/jpeg", CancellationToken.None);

		// Then
		verdict.IsAccepted.ShouldBeFalse();
		verdict.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
		stripper.Invocations.ShouldBe(0);
	}

	[Fact]
	public async Task GivenAllowlistedFileDeclaredCorrectly_WhenInspected_ThenAcceptedAsItsSniffedType()
	{
		// Given
		using var upload = new MemoryStream(Encoding.ASCII.GetBytes("pretend-pdf-bytes"));

		// When
		var verdict = await Ingestor(new InMemoryBlobStore(), MediaType.Pdf, new RecordingExifStripper())
			.Inspect(upload, "application/pdf", CancellationToken.None);

		// Then
		verdict.IsAccepted.ShouldBeTrue();
		verdict.Type.ShouldBe(MediaType.Pdf);
	}

	[Fact]
	public async Task GivenEmptyUpload_WhenInspected_ThenRejectedAsEmpty()
	{
		// Given
		using var upload = new MemoryStream();

		// When
		var verdict = await Ingestor(new InMemoryBlobStore(), MediaType.Jpeg, new RecordingExifStripper())
			.Inspect(upload, "image/jpeg", CancellationToken.None);

		// Then
		verdict.RejectionReason.ShouldBe(MediaRejectionReason.Empty);
	}

	[Fact]
	public async Task GivenUploadOverSizeLimit_WhenInspected_ThenRejectedAsTooLarge()
	{
		// Given
		using var upload = new MemoryStream(new byte[64]);

		// When
		var verdict = await Ingestor(new InMemoryBlobStore(), MediaType.Jpeg, new RecordingExifStripper(), 32)
			.Inspect(upload, "image/jpeg", CancellationToken.None);

		// Then
		verdict.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
	}

	[Fact]
	public async Task GivenUnseekableStream_WhenInspected_ThenRefuses()
	{
		// Given
		var ingestor = Ingestor(new InMemoryBlobStore(), MediaType.Jpeg, new RecordingExifStripper());

		// When / Then
		await Should.ThrowAsync<ArgumentException>(() =>
			ingestor.Inspect(new SyntheticOversizedStream(10), "image/jpeg", CancellationToken.None));
	}

	[Fact]
	public async Task GivenClaimedUpload_WhenIngested_ThenNamedByFileIdNeverByUploadId()
	{
		// Given
		var store = new InMemoryBlobStore();
		var upload = UploadId.New();
		store.Seed(BlobKey.ForUpload(upload), Encoding.ASCII.GetBytes("pretend-jpeg-bytes"));

		// When
		var outcome = await Ingestor(store, MediaType.Jpeg, new RecordingExifStripper())
			.Ingest(BlobKey.ForUpload(upload), Report, FileId, CancellationToken.None);

		// Then
		outcome.OriginalKey.FileName.ShouldBe(FileIdValue);
		outcome.DerivativeKey.FileName.ShouldBe(FileIdValue);
		outcome.OriginalKey.Value.ShouldNotContain(upload.Value);
	}

	[Fact]
	public async Task GivenEmptyUpload_WhenClaimed_ThenRejectedAsEmpty()
	{
		// Given
		var store = new InMemoryBlobStore();
		store.Seed(Quarantined, []);

		// When
		var outcome = await Ingestor(store, MediaType.Jpeg, new RecordingExifStripper())
			.Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.RejectionReason.ShouldBe(MediaRejectionReason.Empty);
	}

	[Fact]
	public async Task GivenNoFileId_WhenIngested_ThenRefuses()
	{
		// Given
		var store = new InMemoryBlobStore();
		store.Seed(Quarantined, Encoding.ASCII.GetBytes("pretend-jpeg-bytes"));

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() =>
			Ingestor(store, MediaType.Jpeg, new RecordingExifStripper())
				.Ingest(Quarantined, Report, default, CancellationToken.None));
	}

	[Fact]
	public async Task GivenRejectedFile_WhenIngested_ThenNothingIsPromotedOutOfQuarantine()
	{
		// Given
		var store = new InMemoryBlobStore();
		store.Seed(Quarantined, Encoding.ASCII.GetBytes("this is not an image at all"));

		// When
		var outcome = await Ingestor(store, null, new RecordingExifStripper()).Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		// The bytes stay in quarantine, where the lifecycle rule expires them
		// (ADR-0096); ingest itself never deletes.
		outcome.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
		store.Keys.ShouldBe([Quarantined.Value]);
		Should.Throw<DomainRuleViolationException>(() => outcome.OriginalKey);
		Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
	}

	[Fact]
	public async Task GivenFileFarLargerThanLimit_WhenIngested_ThenSourceIsNeverPulledFullyIntoMemoryBeforeRejection()
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
			new RecordingExifStripper(), new RecordingVideoRemuxer(),
			new MediaPolicy(maxByteSize, MediaType.All),
			new FixedClock(Now));

		// When
		var outcome = await ingestor.Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);

		// The bound is generous on purpose - any reasonable streaming
		// implementation reads in chunks no larger than a few hundred KB, so
		// stopping within a few megabytes of the configured limit proves the
		// rest of a 500 MB object was never requested. A naive
		// "download everything, then check Length" implementation would have
		// served the full 500 MB here.
		source.TotalBytesServed.ShouldBeLessThan(maxByteSize + 4 * 1024 * 1024);
	}

	[Fact]
	public async Task GivenFileOverSizeLimit_WhenIngested_ThenRejectedBeforeDecoded()
	{
		// Given
		var store = new InMemoryBlobStore();
		store.Seed(Quarantined, new byte[64]);
		var stripper = new RecordingExifStripper();

		// When
		var outcome = await Ingestor(store, MediaType.Jpeg, stripper, 32).Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.RejectionReason.ShouldBe(MediaRejectionReason.TooLarge);
		stripper.Invocations.ShouldBe(0);
		store.Keys.ShouldBe([Quarantined.Value]);
	}

	[Fact]
	public async Task GivenKeyOutsideQuarantine_WhenIngestIsAskedToRead_ThenRefuses()
	{
		// Given
		var store = new InMemoryBlobStore();
		var original = BlobKey.For(ReportId, MediaCompartment.Original, "photo.jpg");
		store.Seed(original, Encoding.ASCII.GetBytes("pretend-jpeg-bytes"));

		// When / Then
		// Ingest reads unverified bytes and nothing else. Pointing it at a
		// report's private source record would re-run stripping over a file that has
		// already been accepted, which is not what this is for.
		await Should.ThrowAsync<DomainRuleViolationException>(() => Ingestor(store, MediaType.Jpeg, new RecordingExifStripper()).Ingest(original, Report, FileId, CancellationToken.None));
	}
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
	public override DateTimeOffset GetUtcNow()
	{
		return now;
	}
}
