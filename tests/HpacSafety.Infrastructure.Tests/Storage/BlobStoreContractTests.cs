using System.Text;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Tests.Media;
using ImageMagick;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
///     The contract every <see cref="IBlobStore" /> keeps. It runs against MinIO,
///     the same S3-compatible server development uses, through the one adapter
///     production uses. See ADR-0026, ADR-0096, and
///     <c>skills/test-hpac-safety/SKILL.md</c>.
/// </summary>
public abstract class BlobStoreContractTests : IAsyncLifetime
{
	private const string ReportId = "dQw4w9WgXcQ";
	private const string OtherReportId = "kJQP7kiw5Fk";

	private const string FileIdValue = "Xk3jR9pQ2mZ";

	private static readonly TinyId Report = TinyId.Parse(ReportId);
	private static readonly TinyId FileId = TinyId.Parse(FileIdValue);
	private static readonly BlobKey Original = BlobKey.For(ReportId, MediaCompartment.Original, FileIdValue);
	private static readonly BlobKey AnotherReportsOriginal = BlobKey.For(OtherReportId, MediaCompartment.Original, FileIdValue);

	private static readonly BlobKey Quarantined = BlobKey.ForUpload(UploadId.New());

	// "Exif" followed by two NULs - the APP1 marker introducing an EXIF block.
	private static ReadOnlySpan<byte> ExifApp1Marker => [0x45, 0x78, 0x69, 0x66, 0x00, 0x00];

	/// <summary>The store under test.</summary>
	protected IBlobStore Store { get; private set; } = null!;

	/// <inheritdoc />
	public virtual async Task InitializeAsync()
	{
		Store = await CreateStore();
	}

	/// <inheritdoc />
	public virtual Task DisposeAsync()
	{
		return Task.CompletedTask;
	}

	/// <summary>Builds the store. Called once the environment it needs is up.</summary>
	protected abstract Task<IBlobStore> CreateStore();

	/// <summary>Attempts the read a pre-signed URL authorises.</summary>
	protected abstract Task<bool> TryRead(Uri readUrl);

	/// <summary>Points a pre-signed URL at a different key, leaving the signature alone.</summary>
	protected abstract Uri RetargetToKey(Uri url,
										 BlobKey key);

	[Fact]
	public async Task GivenBytesWrittenToKey_WhenTheyAreReadBack_ThenTheyAreUnchanged()
	{
		// Given
		var content = Encoding.UTF8.GetBytes("the original bytes, kept exactly as uploaded");
		using var source = new MemoryStream(content);

		// When
		await Store.Write(Quarantined, source, MediaType.Jpeg.ContentType, CancellationToken.None);

		// Then
		(await ReadAll(Quarantined)).ShouldBe(content);
	}

	[Fact]
	public async Task GivenStoredUpload_WhenAskedWhetherItExists_ThenYesAndMissingKeyNo()
	{
		// Given
		await SeedQuarantine(Quarantined, [1, 2, 3], MediaType.Jpeg);

		// When / Then
		(await Store.Exists(Quarantined, CancellationToken.None)).ShouldBeTrue();
		(await Store.Exists(BlobKey.ForUpload(UploadId.New()), CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task GivenUnclaimedUpload_WhenDeleted_ThenGoneAndDeletingAgainSucceeds()
	{
		// Given
		await SeedQuarantine(Quarantined, [1, 2, 3], MediaType.Jpeg);

		// When
		await Store.Delete(Quarantined, CancellationToken.None);

		// Then
		(await Store.Exists(Quarantined, CancellationToken.None)).ShouldBeFalse();
		await Should.NotThrowAsync(() => Store.Delete(Quarantined, CancellationToken.None));
	}

	[Fact]
	public async Task GivenReportsOwnMedia_WhenDeleteIsAsked_ThenRefusedAndBytesRemain()
	{
		// Given
		using (var source = new MemoryStream([1, 2, 3]))
		{
			await Store.Write(Original, source, MediaType.Jpeg.ContentType, CancellationToken.None);
		}

		// When / Then
		// The one physical delete is for an unclaimed upload; a report's media is
		// never erased (AGENTS.md invariant 8).
		await Should.ThrowAsync<DomainRuleViolationException>(() => Store.Delete(Original, CancellationToken.None));
		(await Store.Exists(Original, CancellationToken.None)).ShouldBeTrue();
	}

	[Fact]
	public async Task GivenTwoUploads_WhenOneIsDeleted_ThenTheOtherRemains()
	{
		// Given
		// A version listing is by prefix; deleting must match the key exactly.
		await SeedQuarantine(Quarantined, [1], MediaType.Jpeg);
		var neighbour = BlobKey.ForUpload(UploadId.New());
		await SeedQuarantine(neighbour, [2], MediaType.Jpeg);

		// When
		await Store.Delete(Quarantined, CancellationToken.None);

		// Then
		(await Store.Exists(neighbour, CancellationToken.None)).ShouldBeTrue();
	}

	[Fact]
	public async Task GivenPresignedReadUrl_WhenReusedForDifferentKey_ThenReadIsRefused()
	{
		// Given
		using var source = new MemoryStream(ExifFixtures.JpegWithGpsExif());
		await Store.Write(AnotherReportsOriginal, source, MediaType.Jpeg.ContentType, CancellationToken.None);
		var url = await Store.CreateReadUrl(Original, "download.bin", TimeSpan.FromMinutes(5), CancellationToken.None);

		// When
		var accepted = await TryRead(RetargetToKey(url, AnotherReportsOriginal));

		// Then
		accepted.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenLifetimeBeyondCap_WhenReadUrlIsRequested_ThenRefused()
	{
		// Given
		var lifetime = BlobUrlLifetime.Maximum + TimeSpan.FromMinutes(1);

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() => Store.CreateReadUrl(Original, "download.bin", lifetime, CancellationToken.None));
	}

	[Fact]
	public async Task GivenPhotoWithGPSEXIF_WhenIngested_ThenDerivativeHasNoLocationData()
	{
		// Given
		var original = ExifFixtures.JpegWithGpsExif();
		await SeedQuarantine(Quarantined, original, MediaType.Jpeg);

		// The fixture really does carry a location. Without this the assertions
		// below would pass just as happily on a photo that never had one, which
		// is the failure mode that makes a redaction test worthless.
		using (var beforeIngest = new MagickImage(original))
		{
			beforeIngest.GetExifProfile()!.GetValue(ExifTag.GPSLatitude).ShouldNotBeNull();
			beforeIngest.GetExifProfile()!.GetValue(ExifTag.GPSLongitude).ShouldNotBeNull();
		}

		// When
		var outcome = await Ingestor().Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.Stripped);

		var derivative = await ReadAll(outcome.DerivativeKey);
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
	public async Task GivenHeicPhotoWithGPSEXIF_WhenIngested_ThenDerivativeIsStrippedJpeg()
	{
		// Given
		var key = BlobKey.ForUpload(UploadId.New());
		await SeedQuarantine(key, ExifFixtures.HeicWithGpsExif(), MediaType.Heic);

		// When
		var outcome = await Ingestor().Ingest(key, Report, FileId, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.Stripped);
		outcome.ContentType.ShouldBe(MediaType.Heic);

		using var stripped = new MagickImage(await ReadAll(outcome.DerivativeKey));
		stripped.Format.ShouldBe(MagickFormat.Jpeg);
		stripped.GetExifProfile().ShouldBeNull();
	}

	[Fact]
	public async Task GivenPhotoWithGPSEXIF_WhenIngested_ThenOriginalBytesAreRetainedUntouched()
	{
		// Given
		var original = ExifFixtures.JpegWithGpsExif();
		await SeedQuarantine(Quarantined, original, MediaType.Jpeg);

		// When
		var outcome = await Ingestor().Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		// The private source record keeps everything, GPS included; it is the
		// derivative that is safe to look at. See docs/data-handling.md.
		outcome.OriginalKey.Value.ShouldBe($"dQw4w9WgXcQ/original/{FileIdValue}");
		var retained = await ReadAll(outcome.OriginalKey);
		retained.ShouldBe(original);
		using var retainedImage = new MagickImage(retained);
		retainedImage.GetExifProfile().ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenVideoThatCannotBeRemuxed_WhenIngested_ThenRetainedAndNoReviewerLinkCanBeIssued()
	{
		// Given — the remux could not clean it, so the original is kept and there
		// is still nothing a reviewer may be shown inline (REQ-MED-015)
		var key = BlobKey.ForUpload(UploadId.New());
		await SeedQuarantine(key, ExifFixtures.Mp4(), MediaType.Mp4);

		// When
		var outcome = await Ingestor().Ingest(key, Report, FileId, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.AwaitingStripping);
		(await ReadAll(outcome.OriginalKey)).ShouldBe(ExifFixtures.Mp4());

		// Fails closed: there is nothing to open, rather than a fall-through to
		// the unstripped original (ADR-0094).
		Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
		await Should.ThrowAsync<DomainRuleViolationException>(() => new ReviewerMediaLink(Store).CreateViewUrl(outcome.OriginalKey, "download.jpg", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Fact]
	public async Task GivenIngestedPhoto_WhenReviewerLinkIsRequested_ThenOnlyDerivativeIsIssued()
	{
		// Given
		await SeedQuarantine(Quarantined, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg);
		var outcome = await Ingestor().Ingest(Quarantined, Report, FileId, CancellationToken.None);
		var links = new ReviewerMediaLink(Store);

		// When
		var derivativeUrl = await links.CreateViewUrl(outcome.DerivativeKey, "download.jpg", TimeSpan.FromMinutes(5), CancellationToken.None);

		// Then
		derivativeUrl.ShouldNotBeNull();
		await Should.ThrowAsync<DomainRuleViolationException>(() => links.CreateViewUrl(outcome.OriginalKey, "download.jpg", TimeSpan.FromMinutes(5), CancellationToken.None));
		await Should.ThrowAsync<DomainRuleViolationException>(() => links.CreateViewUrl(Quarantined, "download.jpg", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Fact]
	public async Task GivenFileClaimingImageJpegButContainingSomethingElse_WhenIngested_ThenRejected()
	{
		// Given
		await SeedQuarantine(Quarantined, ExifFixtures.UnrecognisedByAnySniffer(), MediaType.Jpeg);

		// When
		var outcome = await Ingestor().Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.Rejected);
		outcome.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
	}

	[Fact]
	public async Task GivenPngUploadedAsJpeg_WhenInspected_ThenRejected()
	{
		// Given
		using var upload = new MemoryStream(ExifFixtures.Png());

		// When
		var verdict = await Ingestor().Inspect(upload, MediaType.Jpeg.ContentType, CancellationToken.None);

		// Then
		verdict.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
	}

	[Fact]
	public async Task GivenRefusedUpload_WhenIngested_ThenNothingIsPromotedOutOfQuarantine()
	{
		// Given
		// Bytes no sniffer recognises. (Plain text is an accepted document, so
		// "not media" alone no longer makes a refusal once the declared type is
		// out of the picture at claim time.)
		await SeedQuarantine(Quarantined, ExifFixtures.UnrecognisedByAnySniffer(), MediaType.Jpeg);

		// When
		await Ingestor().Ingest(Quarantined, Report, FileId, CancellationToken.None);

		// Then
		// The bytes stay in quarantine, and the bucket lifecycle rule expires
		// them (ADR-0096). Ingest never deletes.
		(await Store.Exists(Original, CancellationToken.None)).ShouldBeFalse();
		(await Store.Exists(Original.In(MediaCompartment.Stripped), CancellationToken.None)).ShouldBeFalse();
		(await Store.Exists(Quarantined, CancellationToken.None)).ShouldBeTrue();
	}

	private MediaIngestor Ingestor(bool remuxProduces = false)
	{
		return new MediaIngestor(Store,
			MediaSnifferChain.Default(),
			new MagickNetExifStripper(MediaType.All),
			new RecordingVideoRemuxer(remuxProduces),
			new MediaPolicyOptions().ToPolicy(),
			TimeProvider.System);
	}

	private async Task SeedQuarantine(BlobKey key,
									  byte[] content,
									  MediaType declaredType)
	{
		using var source = new MemoryStream(content);
		await Store.Write(key, source, declaredType.ContentType, CancellationToken.None);
	}

	private async Task<byte[]> ReadAll(BlobKey key)
	{
		await using var stored = await Store.OpenRead(key, CancellationToken.None);
		using var buffer = new MemoryStream();
		await stored.CopyToAsync(buffer, CancellationToken.None);
		return buffer.ToArray();
	}
}
