using System.Collections.Concurrent;
using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The attachment scenarios the Worker satisfies (ADR-0098): it writes each
///     file's derivative from that file's own outbox message, through the real
///     sniffers and image library, against a real migrated database. Every file is
///     synthetic.
/// </summary>
[Binding]
public sealed class WorkerAttachmentSteps : IAsyncDisposable
{
	private static readonly DateTimeOffset At = new(2026, 9, 23, 9, 0, 0, TimeSpan.Zero);

	private readonly CountingBlobStore _store = new();
	private readonly List<TinyId> _fileIds = [];
	private HpacSafetyDbContext? _db;
	private Report? _report;
	private int _writesBefore;
	private string? _strippedBefore;

	public async ValueTask DisposeAsync()
	{
		if (_db is not null)
		{
			await _db.DisposeAsync();
		}
	}

	// --- REQ-MED-006: every image is re-encoded ---

	[Given(@"an accepted image attachment enters Worker processing")]
	public async Task GivenAnAcceptedImageEntersWorkerProcessing()
	{
		await Claim(PhotoWithExif(), MediaType.Png);
	}

	[When(@"the Worker produces its derivative")]
	public async Task WhenTheWorkerProducesItsDerivative()
	{
		await DrainAttachmentMessages();
	}

	[Then(@"the image is decoded and re-encoded into a supported safe representation")]
	public async Task ThenTheImageIsReEncoded()
	{
		var file = await Reload(_fileIds[0]);
		using var derivative = new MagickImage(_store.Read(file.ViewableKey));
		derivative.Format.ShouldBe(MagickFormat.Png);
	}

	[Then(@"EXIF, GPS, profiles, comments, thumbnails, and other metadata are removed")]
	public async Task ThenMetadataIsRemoved()
	{
		var file = await Reload(_fileIds[0]);
		using var derivative = new MagickImage(_store.Read(file.ViewableKey));
		derivative.GetExifProfile().ShouldBeNull();
		derivative.ProfileNames.ShouldBeEmpty();
	}

	// --- REQ-MED-009: each attachment processes independently ---

	[Given(@"a report has multiple attachments, one of which is slow or corrupt")]
	public async Task GivenAReportHasMultipleAttachmentsOneCorrupt()
	{
		await Claim(PhotoWithExif(), MediaType.Png, "not a picture at all"u8.ToArray());
	}

	[When(@"the Worker processes the report's outbox items")]
	public async Task WhenTheWorkerProcessesTheReportsOutboxItems()
	{
		await DrainAttachmentMessages();
	}

	[Then(@"each file's processing is an independent outbox item")]
	public async Task ThenEachFileIsAnIndependentOutboxItem()
	{
		var messages = await _db!.OutboxMessages
			.Where(message => message.AggregateId == _report!.Id && message.Type == OutboxMessageType.ProcessAttachment)
			.ToListAsync();

		messages.Select(message => message.Payload).ShouldBe(_fileIds.Select(id => id.Value), ignoreOrder: true);
		messages.ShouldAllBe(message => message.IsProcessed);
	}

	[Then(@"the slow or corrupt file neither rolls back the valid report nor forces an additional AI call")]
	public async Task ThenTheCorruptFileRollsBackNothing()
	{
		var good = await Reload(_fileIds[0]);
		var corrupt = await Reload(_fileIds[1]);

		good.AwaitsStripping.ShouldBeFalse();
		corrupt.ProcessingErrorCode.ShouldNotBeNull();
		(await _db!.Reports.AnyAsync(report => report.Id == _report!.Id)).ShouldBeTrue();

		// Only attachment messages were handled; the summary's own message is
		// still waiting for its one call.
		var summary = await _db.OutboxMessages.SingleAsync(message =>
			message.AggregateId == _report!.Id && message.Type == OutboxMessageType.SummarizeReport);
		summary.IsProcessed.ShouldBeFalse();
		summary.Attempts.ShouldBe(0);
	}

	// --- REQ-MED-022: processing twice changes nothing ---

	[Given(@"the Worker has already processed an image attachment")]
	public async Task GivenTheWorkerHasAlreadyProcessedAnImage()
	{
		await Claim(PhotoWithExif(), MediaType.Png);
		await DrainAttachmentMessages();
		_writesBefore = _store.Writes;
		_strippedBefore = (await Reload(_fileIds[0])).StrippedBlobKey;
	}

	[When(@"that attachment's processing message is delivered again")]
	public async Task WhenThatMessageIsDeliveredAgain()
	{
		await ProcessDirectly(_fileIds[0]);
	}

	[Then(@"no second derivative is written")]
	public void ThenNoSecondDerivativeIsWritten()
	{
		_store.Writes.ShouldBe(_writesBefore);
	}

	[Then(@"the attachment's record is unchanged")]
	public async Task ThenTheRecordIsUnchanged()
	{
		(await Reload(_fileIds[0])).StrippedBlobKey.ShouldBe(_strippedBefore);
	}

	// --- REQ-MED-023: a deleted report is skipped ---

	[Given(@"an attachment's report was deleted before the Worker processed it")]
	public async Task GivenTheReportWasDeletedFirst()
	{
		await Claim(PhotoWithExif(), MediaType.Png);
		_report!.SoftDelete(At);
		await _db!.SaveChangesAsync();
	}

	[When(@"the Worker handles that attachment's processing message")]
	public async Task WhenTheWorkerHandlesThatMessage()
	{
		await DrainAttachmentMessages();
	}

	[Then(@"no derivative is written")]
	public void ThenNoDerivativeIsWritten()
	{
		_store.Writes.ShouldBe(0);
	}

	[Then(@"the message is marked complete")]
	public async Task ThenTheMessageIsMarkedComplete()
	{
		var message = await _db!.OutboxMessages
			.IgnoreQueryFilters()
			.SingleAsync(candidate => candidate.Payload == _fileIds[0].Value && candidate.Type == OutboxMessageType.ProcessAttachment);
		message.IsProcessed.ShouldBeTrue();
	}

	/// <summary>
	///     A report as the submission leaves it: each original copied into the
	///     report's compartment under its file's id, a summary message, and one
	///     attachment message per file (ADR-0098).
	/// </summary>
	private async Task Claim(params object[] files)
	{
		_db = await WorkerDatabase.NewMigratedContext();
		_report = new Report(Locale.EnCa, At);

		var recorded = MediaType.Png;
		foreach (var entry in files)
		{
			if (entry is MediaType type)
			{
				recorded = type;
				continue;
			}

			var bytes = (byte[])entry;
			var fileId = TinyId.New();
			var key = BlobKey.For(_report.Id.Value, MediaCompartment.Original, fileId.Value);
			_store.Seed(key, bytes);
			_report.AddFile(fileId, key.Value, recorded.ContentType, bytes.Length, "synthetic.png", At);
			_fileIds.Add(fileId);
		}

		_db.Reports.Add(_report);
		_db.OutboxMessages.Add(new OutboxMessage(_report.Id, OutboxMessageType.SummarizeReport, _report.Id.Value, At));
		foreach (var fileId in _fileIds)
		{
			_db.OutboxMessages.Add(new OutboxMessage(_report.Id, OutboxMessageType.ProcessAttachment, fileId.Value, At));
		}

		await _db.SaveChangesAsync();
	}

	private async Task DrainAttachmentMessages()
	{
		// The same claim loop the Worker runs, one message at a time.
		var processor = new ProcessAttachmentProcessor(_db!, Ingestor());
		while (await OutboxClaimer.ClaimNext(
				   _db!, OutboxMessageType.ProcessAttachment, At.AddMinutes(1), processor.Process, CancellationToken.None))
		{
		}
	}

	private async Task ProcessDirectly(TinyId fileId)
	{
		var processor = new ProcessAttachmentProcessor(_db!, Ingestor());
		await processor.Process(
			new OutboxMessage(_report!.Id, OutboxMessageType.ProcessAttachment, fileId.Value, At),
			CancellationToken.None);
		await _db!.SaveChangesAsync();
	}

	private MediaIngestor Ingestor()
	{
		return new MediaIngestor(
			_store,
			MediaSnifferChain.Default(),
			new MagickNetExifStripper(MediaType.All),
			new FfmpegVideoRemuxer(NullLogger<FfmpegVideoRemuxer>.Instance),
			new MediaPolicyOptions().ToPolicy(),
			TimeProvider.System);
	}

	private async Task<ReportFile> Reload(TinyId fileId)
	{
		_db!.ChangeTracker.Clear();
		return await _db.ReportFiles.IgnoreQueryFilters().SingleAsync(file => file.Id == fileId);
	}

	private static byte[] PhotoWithExif()
	{
		using var image = new MagickImage(MagickColors.SkyBlue, 16, 16) { Format = MagickFormat.Png };
		var profile = new ExifProfile();
		profile.SetValue(ExifTag.Make, "Synthetic Camera Co");
		profile.SetValue(ExifTag.GPSLatitudeRef, "N");
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
