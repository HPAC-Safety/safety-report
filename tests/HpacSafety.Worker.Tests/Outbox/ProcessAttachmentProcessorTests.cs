using System.Collections.Concurrent;
using System.Text;
using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     ADR-0098 against a real database and the real sniffers and image library:
///     the Worker writes an attachment's derivative from its outbox message,
///     changes nothing on redelivery, skips a deleted report, and records a file it
///     cannot clean as failed (REQ-MED-006, REQ-MED-009, REQ-MED-013, REQ-MED-022,
///     REQ-MED-023). Every file here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedWorkerPostgres.Name)]
public sealed class ProcessAttachmentProcessorTests(WorkerPostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 23, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenProcessAttachmentOutboxType_WhenAsked_ThenHandlesProcessAttachment()
	{
		// Given
		var processor = new ProcessAttachmentProcessor(null!, null!);

		// Then
		processor.HandlesType.ShouldBe(OutboxMessageType.ProcessAttachment);
	}

	[Fact]
	public async Task GivenClaimedPhotoWithExif_WhenProcessed_ThenStrippedDerivativeIsRecordedAndViewable()
	{
		// Given
		var (context, store, file) = await Claimed(Photo(), MediaType.Png);
		await using var _ = context;

		// When
		await Process(context, store, file.Id);

		// Then
		var stored = await Reload(context, file.Id);
		stored.AwaitsStripping.ShouldBeFalse();
		stored.ViewableKey.ShouldBe(BlobKey.Parse(stored.BlobKey).In(MediaCompartment.Stripped));
		using var derivative = new MagickImage(store.Read(stored.ViewableKey));
		derivative.GetExifProfile().ShouldBeNull();
	}

	[Fact]
	public async Task GivenAlreadyProcessedPhoto_WhenMessageIsDeliveredAgain_ThenNothingIsWrittenOrChanged()
	{
		// Given
		var (context, store, file) = await Claimed(Photo(), MediaType.Png);
		await using var _ = context;
		await Process(context, store, file.Id);
		var before = await Reload(context, file.Id);
		var writes = store.Writes;

		// When
		await Process(context, store, file.Id);

		// Then
		store.Writes.ShouldBe(writes);
		var after = await Reload(context, file.Id);
		after.StrippedBlobKey.ShouldBe(before.StrippedBlobKey);
		after.ExifStrippedAt.ShouldBe(before.ExifStrippedAt);
	}

	[Fact]
	public async Task GivenReportDeletedBeforeProcessing_WhenProcessed_ThenNoDerivativeIsWritten()
	{
		// Given
		var (context, store, file) = await Claimed(Photo(), MediaType.Png);
		await using var _ = context;
		var report = await context.Reports.SingleAsync(candidate => candidate.Id == file.ReportId);
		report.SoftDelete(At);
		await context.SaveChangesAsync();

		// When
		await Process(context, store, file.Id);

		// Then
		store.Writes.ShouldBe(0);
		(await Reload(context, file.Id)).AwaitsStripping.ShouldBeTrue();
	}

	[Fact]
	public async Task GivenOriginalThatIsNotTheRecordedType_WhenProcessed_ThenMarkedFailedAndNeverViewable()
	{
		// Given
		var (context, store, file) = await Claimed(Encoding.ASCII.GetBytes("not a picture at all"), MediaType.Png);
		await using var _ = context;

		// When
		await Process(context, store, file.Id);

		// Then
		var stored = await Reload(context, file.Id);
		stored.ProcessingErrorCode.ShouldNotBeNull();
		Should.Throw<DomainRuleViolationException>(() => stored.ViewableKey);
		store.Writes.ShouldBe(0);
	}

	[Fact]
	public async Task GivenClaimedDocument_WhenProcessed_ThenRetainedWithNoDerivativeAndNoFailure()
	{
		// Given
		var pdf = "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray();
		var (context, store, file) = await Claimed(pdf, MediaType.Pdf);
		await using var _ = context;

		// When
		await Process(context, store, file.Id);

		// Then
		var stored = await Reload(context, file.Id);
		stored.ProcessingErrorCode.ShouldBeNull();
		stored.AwaitsStripping.ShouldBeTrue();
		store.Writes.ShouldBe(0);
	}

	[Fact]
	public async Task GivenMessageForAFileThatDoesNotExist_WhenProcessed_ThenNothingHappens()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var store = new CountingBlobStore();

		// When / Then
		await Should.NotThrowAsync(() => Process(context, store, TinyId.New()));
		store.Writes.ShouldBe(0);
	}

	private async Task<(HpacSafetyDbContext Context, CountingBlobStore Store, ReportFile File)> Claimed(
		byte[] original,
		MediaType recorded)
	{
		var connectionString = await postgres.CreateMigratedDatabase();
		var context = WorkerPostgresFixture.ContextFor(connectionString);
		var store = new CountingBlobStore();

		// As the submission leaves it: the original copied into the report's
		// compartment under the file's id, and nothing else (ADR-0098).
		var report = new Report(Locale.EnCa, At);
		var fileId = TinyId.New();
		var key = BlobKey.For(report.Id.Value, MediaCompartment.Original, fileId.Value);
		store.Seed(key, original);
		var file = report.AddFile(fileId, key.Value, recorded.ContentType, original.Length, "synthetic.bin", At);
		context.Reports.Add(report);
		await context.SaveChangesAsync();

		return (context, store, file);
	}

	private static async Task Process(HpacSafetyDbContext context,
									  CountingBlobStore store,
									  TinyId fileId)
	{
		var ingestor = new MediaIngestor(
			store,
			MediaSnifferChain.Default(),
			new MagickNetExifStripper(MediaType.All),
			new FfmpegVideoRemuxer(Microsoft.Extensions.Logging.Abstractions.NullLogger<FfmpegVideoRemuxer>.Instance),
			new MediaPolicyOptions().ToPolicy(),
			TimeProvider.System);

		var processor = new ProcessAttachmentProcessor(context, ingestor);
		await processor.Process(
			new OutboxMessage(TinyId.New(), OutboxMessageType.ProcessAttachment, fileId.Value, At),
			CancellationToken.None);
		await context.SaveChangesAsync();
	}

	private static async Task<ReportFile> Reload(HpacSafetyDbContext context,
												 TinyId fileId)
	{
		context.ChangeTracker.Clear();
		return await context.ReportFiles.IgnoreQueryFilters().SingleAsync(file => file.Id == fileId);
	}

	private static byte[] Photo()
	{
		using var image = new MagickImage(MagickColors.SkyBlue, 16, 16) { Format = MagickFormat.Png };
		var profile = new ExifProfile();
		profile.SetValue(ExifTag.Make, "Synthetic Camera Co");
		image.SetProfile(profile);
		return image.ToByteArray();
	}

	private sealed class CountingBlobStore : IBlobStore
	{
		private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);
		private int _writes;

		public int Writes => _writes;

		public void Seed(BlobKey key,
						 byte[] content)
		{
			_blobs[key.Value] = content;
		}

		public byte[] Read(BlobKey key)
		{
			return _blobs[key.Value];
		}

		public Task<Uri> CreateReadUrl(BlobKey key,
									   string downloadFileName,
									   TimeSpan lifetime,
									   CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public Task<Uri> CreateInlineReadUrl(BlobKey key,
												 string contentType,
												 TimeSpan lifetime,
												 CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public Task<Stream> OpenRead(BlobKey key,
									 CancellationToken cancellationToken)
		{
			return Task.FromResult<Stream>(new MemoryStream(_blobs[key.Value], false));
		}

		public async Task Write(BlobKey key,
								Stream content,
								string contentType,
								CancellationToken cancellationToken)
		{
			using var buffer = new MemoryStream();
			await content.CopyToAsync(buffer, cancellationToken);
			_blobs[key.Value] = buffer.ToArray();
			Interlocked.Increment(ref _writes);
		}

		public Task<StoredBlob?> Describe(BlobKey key,
										  CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public Task Copy(BlobKey source,
						 BlobKey destination,
						 CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public Task Delete(BlobKey key,
						   CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}
	}
}
