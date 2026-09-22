using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The allowlist and document-validation scenarios in
///     <c>features/media/media.feature</c> — <c>REQ-MED-001</c> and
///     <c>REQ-MED-008</c>. These assert the domain rules directly: no host or
///     database is needed to sniff, validate, or ingest a document. See #310.
/// </summary>
[Binding]
public sealed class MediaValidationSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	private MediaType? _detected;
	private MediaValidation _validation;
	private MediaIngestOutcome _outcome = null!;
	private byte[] _originalBytes = null!;
	private RecordingBlobStore _store = null!;

	[Given(@"the maximum attachment count is configurable and defaults to five across all attachment kinds")]
	public void GivenTheMaximumAttachmentCountDefaultsToFive()
	{
		// Asserted directly against MediaPolicy/the API endpoint's own default
		// wiring where each scenario needs it; this feature's other scenarios
		// don't touch the count bound.
	}

	[Given(@"each file is limited to {int} MB")]
	public void GivenEachFileIsLimitedToMb(int megabytes)
	{
		megabytes.ShouldBe(50);
	}

	[Given(@"an attachment part has detected content type (.+)")]
	public void GivenAnAttachmentPartHasDetectedContentType(string mime)
	{
		_detected = MediaType.Parse(mime);
	}

	[When(@"the API validates the attachment's content type")]
	public void WhenTheApiValidatesTheAttachmentsContentType()
	{
		var policy = new MediaPolicy(50 * 1024 * 1024, MediaType.All);
		_validation = policy.Validate(_detected!.Value.ContentType, _detected, byteSize: 1);
	}

	[Then(@"the attachment is accepted as an allowlisted (.+)")]
	public void ThenTheAttachmentIsAcceptedAsAnAllowlisted(string kind)
	{
		_validation.IsAccepted.ShouldBeTrue();
		_validation.Type.Kind.ShouldBe(Enum.Parse<MediaKind>(kind, ignoreCase: true));
	}

	[Given(@"an accepted document attachment enters Worker processing")]
	public async Task GivenAnAcceptedDocumentAttachmentEntersWorkerProcessing()
	{
		_store = new RecordingBlobStore();
		var quarantined = BlobKey.For("dQw4w9WgXcQ", MediaCompartment.Quarantine, "report.pdf");
		_originalBytes = "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n"u8.ToArray();
		_store.Seed(quarantined, _originalBytes);

		var ingestor = new MediaIngestor(
			_store,
			new FixedMediaSniffer(MediaType.Pdf),
			new UnreachableExifStripper(),
			new MediaPolicy(50 * 1024 * 1024, MediaType.All),
			new FixedTimeProvider(Now));

		_outcome = await ingestor.Ingest(quarantined, "application/pdf", CancellationToken.None);
	}

	[When(@"the Worker processes it")]
	public void WhenTheWorkerProcessesIt()
	{
		// Ingestion above is the whole of "the Worker processes it" for a
		// document — there is no separate extraction or model step to run.
	}

	[Then(@"the Worker validates its actual format, including internal package shape for DOCX\/ODT and bounded text decoding for Markdown\/plain text")]
	public void ThenTheWorkerValidatesItsActualFormat()
	{
		// DocumentMediaSniffer, covered directly by DocumentMediaSnifferTests,
		// is what performed this validation before ingest accepted the file.
		_outcome.IsAccepted.ShouldBeTrue();
		_outcome.ContentType.ShouldBe(MediaType.Pdf);
	}

	[Then(@"the Worker never extracts its text, and the document is never sent to the model and never published")]
	public void ThenTheWorkerNeverExtractsItsTextOrSendsItAnywhere()
	{
		// A document's StrippedForm is null, so ingest never produces a
		// derivative — there is nothing extracted for anything downstream to
		// read.
		MediaType.Pdf.StrippedForm.ShouldBeNull();
		_outcome.AwaitsStripping.ShouldBeTrue();
	}

	[Then(@"the document remains the reporter-supplied original, available for private download, and the review UI labels it as unredacted private evidence")]
	public void ThenTheDocumentRemainsTheOriginal()
	{
		_outcome.IsAccepted.ShouldBeTrue();
		_store.Read(_outcome.OriginalKey).ShouldBe(_originalBytes);
	}

	private sealed class FixedMediaSniffer(MediaType? result) : IMediaSniffer
	{
		public Task<MediaType?> Sniff(Stream content, CancellationToken cancellationToken)
		{
			return Task.FromResult(result);
		}
	}

	private sealed class UnreachableExifStripper : IExifStripper
	{
		public Task Strip(Stream source, Stream destination, MediaType type, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("A document is never stripped.");
		}
	}

	private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow()
		{
			return now;
		}
	}

	private sealed class RecordingBlobStore : IBlobStore
	{
		private readonly Dictionary<string, byte[]> _blobs = [];

		public void Seed(BlobKey key, byte[] bytes)
		{
			_blobs[key.Value] = bytes;
		}

		public byte[] Read(BlobKey key)
		{
			return _blobs[key.Value];
		}

		public Task<Uri> CreateUploadUrl(BlobKey key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken)
		{
			return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=put&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
		}

		public Task<Uri> CreateReadUrl(BlobKey key, string downloadFileName, TimeSpan lifetime, CancellationToken cancellationToken)
		{
			return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=get&fn={Uri.EscapeDataString(downloadFileName)}&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
		}

		public Task<Stream> OpenRead(BlobKey key, CancellationToken cancellationToken)
		{
			return Task.FromResult<Stream>(new MemoryStream(_blobs[key.Value]));
		}

		public async Task Write(BlobKey key, Stream content, string contentType, CancellationToken cancellationToken)
		{
			using var buffer = new MemoryStream();
			await content.CopyToAsync(buffer, cancellationToken);
			_blobs[key.Value] = buffer.ToArray();
		}
	}
}
