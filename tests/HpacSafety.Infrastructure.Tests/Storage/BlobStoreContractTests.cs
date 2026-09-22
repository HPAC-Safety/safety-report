using System.Text;
using Amazon.S3;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Tests.Media;
using ImageMagick;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
///     The contract every <see cref="IBlobStore" /> keeps, run unchanged against
///     MinIO and against the filesystem store.
///     <para>
///         One suite rather than two is the point: a development stand-in that is not
///         held to the production adapter's guarantees is how a guarantee quietly stops
///         being true in the environment people actually run. See ADR-0026 and
///         <c>skills/test-hpac-safety/SKILL.md</c>.
///     </para>
/// </summary>
public abstract class BlobStoreContractTests : IAsyncLifetime
{
	private const string ReportId = "dQw4w9WgXcQ";
	private const string OtherReportId = "kJQP7kiw5Fk";

	private static readonly BlobKey Quarantined = BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.jpg");
	private static readonly BlobKey AnotherReportsUpload = BlobKey.For(OtherReportId, MediaCompartment.Quarantine, "photo.jpg");

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

	/// <summary>
	///     Attempts the upload a pre-signed URL authorises, returning whether the
	///     store accepted it. S3 answers with a status code and the filesystem store
	///     throws; both collapse to the same answer here so the test can be shared.
	/// </summary>
	protected abstract Task<bool> TryUpload(Uri uploadUrl, byte[] content, string contentType);

	/// <summary>Attempts the read a pre-signed URL authorises.</summary>
	protected abstract Task<bool> TryRead(Uri readUrl);

	/// <summary>Points a pre-signed URL at a different key, leaving the signature alone.</summary>
	protected abstract Uri RetargetToKey(Uri url, BlobKey key);

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
	public async Task GivenPresignedUploadUrlIntoQuarantine_WhenUsed_ThenBytesLand()
	{
		// Given
		var key = BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.jpg");

		// When
		var url = await Store.CreateUploadUrl(key, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);
		var accepted = await TryUpload(url, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg.ContentType);

		// Then
		accepted.ShouldBeTrue();
		key.Compartment.ShouldBe(MediaCompartment.Quarantine);
		key.Value.ShouldStartWith("quarantine/dQw4w9WgXcQ/");
	}

	[Fact]
	public async Task GivenPresignedUploadUrl_WhenReusedForDifferentKey_ThenUploadIsRefused()
	{
		// Given
		var url = await Store.CreateUploadUrl(Quarantined, MediaType.Jpeg.ContentType, TimeSpan.FromMinutes(5), CancellationToken.None);

		// When
		var retargeted = RetargetToKey(url, AnotherReportsUpload);
		var accepted = await TryUpload(retargeted, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg.ContentType);

		// Then
		// A pre-signed URL is a capability for one object, not a key to the
		// bucket - and with the report id in the key, that also means one
		// reporter's slot cannot write into another report's directory.
		accepted.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenPresignedReadUrl_WhenReusedForDifferentKey_ThenReadIsRefused()
	{
		// Given
		using var source = new MemoryStream(ExifFixtures.JpegWithGpsExif());
		await Store.Write(AnotherReportsUpload, source, MediaType.Jpeg.ContentType, CancellationToken.None);
		var url = await Store.CreateReadUrl(Quarantined, "download.bin", TimeSpan.FromMinutes(5), CancellationToken.None);

		// When
		var accepted = await TryRead(RetargetToKey(url, AnotherReportsUpload));

		// Then
		accepted.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenLifetimeBeyondCap_WhenReadUrlIsRequested_ThenRefused()
	{
		// Given
		var lifetime = BlobUrlLifetime.Maximum + TimeSpan.FromMinutes(1);

		// When / Then
		await Should.ThrowAsync<DomainRuleViolationException>(() => Store.CreateReadUrl(Quarantined, "download.bin", lifetime, CancellationToken.None));
		await Should.ThrowAsync<DomainRuleViolationException>(() => Store.CreateUploadUrl(Quarantined, MediaType.Jpeg.ContentType, lifetime, CancellationToken.None));
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
		var outcome = await Ingestor().Ingest(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

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
		var key = BlobKey.For(ReportId, MediaCompartment.Quarantine, "photo.heic");
		await SeedQuarantine(key, ExifFixtures.HeicWithGpsExif(), MediaType.Heic);

		// When
		var outcome = await Ingestor().Ingest(key, MediaType.Heic.ContentType, CancellationToken.None);

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
		var outcome = await Ingestor().Ingest(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

		// Then
		// The private source record keeps everything, GPS included; it is the
		// derivative that is safe to look at. See docs/data-handling.md.
		outcome.OriginalKey.Value.ShouldBe("dQw4w9WgXcQ/original/photo.jpg");
		var retained = await ReadAll(outcome.OriginalKey);
		retained.ShouldBe(original);
		using var retainedImage = new MagickImage(retained);
		retainedImage.GetExifProfile().ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenVideo_WhenIngested_ThenRetainedAndNoReviewerLinkCanBeIssued()
	{
		// Given
		var key = BlobKey.For(ReportId, MediaCompartment.Quarantine, "clip.mp4");
		await SeedQuarantine(key, ExifFixtures.Mp4(), MediaType.Mp4);

		// When
		var outcome = await Ingestor().Ingest(key, MediaType.Mp4.ContentType, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.AwaitingStripping);
		(await ReadAll(outcome.OriginalKey)).ShouldBe(ExifFixtures.Mp4());

		// Fails closed: there is nothing to open, rather than a fall-through to
		// the unstripped original. See #65.
		Should.Throw<DomainRuleViolationException>(() => outcome.DerivativeKey);
		await Should.ThrowAsync<DomainRuleViolationException>(() => new ReviewerMediaLink(Store).CreateViewUrl(outcome.OriginalKey, "download.jpg", TimeSpan.FromMinutes(5), CancellationToken.None));
	}

	[Fact]
	public async Task GivenIngestedPhoto_WhenReviewerLinkIsRequested_ThenOnlyDerivativeIsIssued()
	{
		// Given
		await SeedQuarantine(Quarantined, ExifFixtures.JpegWithGpsExif(), MediaType.Jpeg);
		var outcome = await Ingestor().Ingest(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);
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
		var outcome = await Ingestor().Ingest(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

		// Then
		outcome.Status.ShouldBe(MediaIngestStatus.Rejected);
		outcome.RejectionReason.ShouldBe(MediaRejectionReason.UnrecognisedContent);
	}

	[Fact]
	public async Task GivenPngUploadedAsJpeg_WhenIngested_ThenRejected()
	{
		// Given
		await SeedQuarantine(Quarantined, ExifFixtures.Png(), MediaType.Jpeg);

		// When
		var outcome = await Ingestor().Ingest(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

		// Then
		outcome.RejectionReason.ShouldBe(MediaRejectionReason.DeclaredTypeMismatch);
	}

	[Fact]
	public async Task GivenRefusedUpload_WhenIngested_ThenNothingIsPromotedOutOfQuarantine()
	{
		// Given
		await SeedQuarantine(Quarantined, ExifFixtures.NotMedia(), MediaType.Jpeg);

		// When
		await Ingestor().Ingest(Quarantined, MediaType.Jpeg.ContentType, CancellationToken.None);

		// Then
		// The bytes stay where the browser put them, and the bucket lifecycle
		// rule expires them after 24 hours. There is no delete on IBlobStore, on
		// purpose: no code path exists that could later be pointed at a real
		// report's media. See ADR-0026.
		(await Exists(Quarantined.In(MediaCompartment.Original))).ShouldBeFalse();
		(await Exists(Quarantined.In(MediaCompartment.Stripped))).ShouldBeFalse();
		(await Exists(Quarantined)).ShouldBeTrue();
	}

	private MediaIngestor Ingestor()
	{
		return new MediaIngestor(Store,
			MediaSnifferChain.Default(),
			new MagickNetExifStripper(MediaType.All),
			new MediaPolicyOptions().ToPolicy(),
			TimeProvider.System);
	}

	private async Task SeedQuarantine(BlobKey key, byte[] content, MediaType declaredType)
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

	private async Task<bool> Exists(BlobKey key)
	{
		try
		{
			await using var stored = await Store.OpenRead(key, CancellationToken.None);
			return true;
		}
		catch (FileNotFoundException)
		{
			return false;
		}
		catch (AmazonS3Exception)
		{
			return false;
		}
	}
}
