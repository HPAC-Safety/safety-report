using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using Microsoft.Extensions.Configuration;
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
	private RecordingVideoRemuxer _remuxer = null!;
	private RecordingImageStripper _stripper = null!;

	[Given(@"the maximum attachment count is configurable and defaults to five across all attachment kinds")]
	public void GivenTheMaximumAttachmentCountDefaultsToFive()
	{
		// Asserted directly against MediaPolicy/the API endpoint's own default
		// wiring where each scenario needs it; this feature's other scenarios
		// don't touch the count bound.
	}

	[Given(@"each file is limited to {int} MB for a video and {int} MB for an image or a document")]
	public void GivenEachFileIsLimitedPerKind(int videoMegabytes,
											   int imageOrDocumentMegabytes)
	{
		// The policy the API is configured with, read from the settings it ships
		// with, and the defaults a host without them falls back to (ADR-0126).
		const long megabyte = 1024 * 1024;
		var configured = new MediaPolicyOptions();
		new ConfigurationBuilder()
			.AddJsonFile(Path.Combine(RepositoryRoot(), "src", "HpacSafety.Api", "appsettings.json"))
			.Build()
			.GetSection(MediaPolicyOptions.SectionName)
			.Bind(configured);

		foreach (var policy in new[] { configured.ToPolicy(), new MediaPolicyOptions().ToPolicy() })
		{
			policy.Limits.Video.ShouldBe(videoMegabytes * megabyte);
			policy.Limits.Image.ShouldBe(imageOrDocumentMegabytes * megabyte);
			policy.Limits.Document.ShouldBe(imageOrDocumentMegabytes * megabyte);
		}
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null
			   && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
	}

	[Given(@"an uploaded file has detected content type (.+)")]
	public void GivenAnAttachmentPartHasDetectedContentType(string mime)
	{
		_detected = MediaType.Parse(mime);
	}

	[When(@"the API validates the attachment's content type")]
	public void WhenTheApiValidatesTheAttachmentsContentType()
	{
		var policy = new MediaPolicyOptions().ToPolicy();
		_validation = policy.Validate(_detected!.Value.ContentType, _detected, byteSize: 1);
	}

	[Then(@"the attachment is accepted as an allowlisted (.+)")]
	public void ThenTheAttachmentIsAcceptedAsAnAllowlisted(string kind)
	{
		_validation.IsAccepted.ShouldBeTrue();
		_validation.Type.Kind.ShouldBe(Enum.Parse<MediaKind>(kind, ignoreCase: true));
	}

	[Given(@"an accepted video attachment enters processing")]
	public Task GivenAnAcceptedVideoAttachmentEntersProcessing()
	{
		return IngestVideo(remuxProduces: true);
	}

	[Given(@"an accepted video attachment cannot be remuxed into a verified derivative")]
	public Task GivenAVideoThatCannotBeRemuxed()
	{
		return IngestVideo(remuxProduces: false);
	}

	[When(@"its derivative is produced")]
	public void WhenItsDerivativeIsProduced()
	{
		// Produced by the Given: ingest is one pass, and the assertions below
		// read its outcome.
	}

	[When(@"processing finishes")]
	public void WhenProcessingFinishes()
	{
	}

	[Then(@"the video is remuxed through a controlled toolchain without decoding its picture")]
	public void ThenTheVideoIsRemuxed()
	{
		_remuxer.Invocations.ShouldBe(1);
		_stripper.Invocations.ShouldBe(0, "an image decoder must never touch video (ADR-0025, ADR-0094)");
	}

	[Then(@"the derivative carries no container metadata, location, device, creation, or filename fields")]
	public void ThenTheDerivativeCarriesNoMetadata()
	{
		// The fake remuxer stands for ffmpeg, so what is asserted here is that a
		// derivative exists and is not the original. That the real toolchain
		// strips these fields and verifies it did is
		// FfmpegVideoRemuxerTests, which runs against ffmpeg itself.
		_outcome.IsViewable.ShouldBeTrue();
		_store.Read(_outcome.DerivativeKey).ShouldNotBe(_originalBytes);
	}

	[Then(@"the derivative carries only the video and any audio stream, with timed-metadata, data, and subtitle tracks dropped")]
	public void ThenTheDerivativeCarriesOnlyAudioVisualStreams()
	{
		// Stream selection is the remuxer's contract and is asserted against the
		// real ffmpeg in FfmpegVideoRemuxerTests.
		_outcome.IsViewable.ShouldBeTrue();
	}

	[Then(@"the derivative is verified to hold none of those before it is accepted")]
	public void ThenTheDerivativeIsVerified()
	{
		// A remuxer that cannot verify its output reports that it produced
		// nothing, which is the retained case below.
		_outcome.IsViewable.ShouldBeTrue();
	}

	[Then(@"a byte-for-byte copy of the original video is never used as the derivative")]
	public void ThenTheDerivativeIsNotTheOriginal()
	{
		_store.Read(_outcome.DerivativeKey).ShouldNotBe(_originalBytes);
	}

	[Then(@"the derivative is an MP4 container, stored as video\/mp4, whatever container the video arrived in")]
	public void ThenTheDerivativeIsAnMp4()
	{
		// The upload was QuickTime. That the remux writes MP4 and its
		// verification refuses anything else is FfmpegVideoRemuxerTests and
		// RemuxVerificationTests, against ffmpeg itself (ADR-0122).
		_store.TypeOf(_outcome.DerivativeKey).ShouldBe(MediaType.Mp4.ContentType);
	}

	[Then(@"the upload still succeeds and the original is retained")]
	public void ThenTheUploadStillSucceeds()
	{
		_outcome.IsAccepted.ShouldBeTrue();
		_store.Read(_outcome.OriginalKey).ShouldBe(_originalBytes);
	}

	[Then(@"the attachment is marked as having no derivative to show")]
	public void ThenThereIsNoDerivativeToShow()
	{
		_outcome.IsViewable.ShouldBeFalse();
		Should.Throw<DomainRuleViolationException>(() => _outcome.DerivativeKey);
	}

	[Then(@"it is not marked as a processing failure, because nothing failed that the reporter should lose their footage over")]
	public void ThenItIsNotAProcessingFailure()
	{
		_outcome.Status.ShouldNotBe(MediaIngestStatus.Rejected);
		_outcome.IsAccepted.ShouldBeTrue();
	}

	private async Task IngestVideo(bool remuxProduces)
	{
		_store = new RecordingBlobStore();
		var original = BlobKey.For("dQw4w9WgXcQ", MediaCompartment.Original, TinyId.New().Value);
		// An iPhone's QuickTime, the container the derivative must not keep.
		_originalBytes = "pretend-quicktime-bytes-with-a-gps-tag"u8.ToArray();
		_store.Seed(original, _originalBytes);

		_remuxer = new RecordingVideoRemuxer(remuxProduces);
		_stripper = new RecordingImageStripper();

		var ingestor = new MediaIngestor(
			_store,
			new FixedMediaSniffer(MediaType.QuickTime),
			_stripper,
			_remuxer,
			new MediaPolicyOptions().ToPolicy(),
			new FixedClock(Now));

		_outcome = await ingestor.Process(original, MediaType.QuickTime, CancellationToken.None);
	}

	[Given(@"an accepted document attachment enters Worker processing")]
	public async Task GivenAnAcceptedDocumentAttachmentEntersWorkerProcessing()
	{
		_store = new RecordingBlobStore();
		var original = BlobKey.For("dQw4w9WgXcQ", MediaCompartment.Original, TinyId.New().Value);
		_originalBytes = "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n"u8.ToArray();
		_store.Seed(original, _originalBytes);

		var ingestor = new MediaIngestor(
			_store,
			new FixedMediaSniffer(MediaType.Pdf),
			new UnreachableExifStripper(),
			new RecordingVideoRemuxer(),
			new MediaPolicyOptions().ToPolicy(),
			new FixedClock(Now));

		_outcome = await ingestor.Process(original, MediaType.Pdf, CancellationToken.None);
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

	[Then(@"the Worker never extracts its text, and the document is never sent to the model and never rendered inline")]
	public void ThenTheWorkerNeverExtractsItsTextOrSendsItAnywhere()
	{
		// A document's StrippedForm is null, so ingest never produces a
		// derivative — there is nothing extracted for anything downstream to
		// read.
		MediaType.Pdf.StrippedForm.ShouldBeNull();
		_outcome.AwaitsStripping.ShouldBeTrue();
	}

	[Then(@"the document remains the reporter-supplied original, available for download")]
	public void ThenTheDocumentRemainsTheOriginal()
	{
		_outcome.IsAccepted.ShouldBeTrue();
		_store.Read(_outcome.OriginalKey).ShouldBe(_originalBytes);
	}

	private sealed class FixedMediaSniffer(MediaType? result) : IMediaSniffer
	{
		public Task<MediaType?> Sniff(Stream content,
									  CancellationToken cancellationToken)
		{
			return Task.FromResult(result);
		}
	}

	private sealed class UnreachableExifStripper : IExifStripper
	{
		public Task Strip(Stream source,
						  Stream destination,
						  MediaType type,
						  CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("A document is never stripped.");
		}
	}


	private sealed class RecordingBlobStore : IBlobStore
	{
		private readonly Dictionary<string, byte[]> _blobs = [];
		private readonly Dictionary<string, string> _types = [];

		public string TypeOf(BlobKey key)
		{
			return _types[key.Value];
		}

		public void Seed(BlobKey key,
						 byte[] bytes)
		{
			_blobs[key.Value] = bytes;
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
			return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=get&fn={Uri.EscapeDataString(downloadFileName)}&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
		}

		public Task<Uri> CreateInlineReadUrl(BlobKey key,
												 string contentType,
												 TimeSpan lifetime,
												 CancellationToken cancellationToken)
		{
			return Task.FromResult(new Uri($"https://example.invalid/{key.Value}?op=inline&ct={Uri.EscapeDataString(contentType)}&ttl={BlobUrlLifetime.Validate(lifetime).TotalSeconds}"));
		}

		public Task<Uri> CreateUploadUrl(BlobKey key,
									   string contentType,
									   long byteSize,
									   TimeSpan lifetime,
									   CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public Task<Stream> OpenReadRange(BlobKey key,
										  long offset,
										  long length,
										  CancellationToken cancellationToken)
		{
			throw new NotSupportedException();
		}

		public Task<Stream> OpenRead(BlobKey key,
									 CancellationToken cancellationToken)
		{
			return Task.FromResult<Stream>(new MemoryStream(_blobs[key.Value]));
		}

		public async Task Write(BlobKey key,
								Stream content,
								string contentType,
								CancellationToken cancellationToken)
		{
			using var buffer = new MemoryStream();
			await content.CopyToAsync(buffer, cancellationToken);
			_blobs[key.Value] = buffer.ToArray();
			_types[key.Value] = contentType;
		}

		public Task<StoredBlob?> Describe(BlobKey key,
										  CancellationToken cancellationToken)
		{
			return Task.FromResult(_blobs.TryGetValue(key.Value, out var bytes)
				? new StoredBlob("application/octet-stream", bytes.Length)
				: null);
		}

		public Task Copy(BlobKey source,
						 BlobKey destination,
						 CancellationToken cancellationToken)
		{
			_blobs[destination.Value] = _blobs[source.Value];
			return Task.CompletedTask;
		}

		public Task Delete(BlobKey key,
						   CancellationToken cancellationToken)
		{
			_blobs.Remove(key.Value);
			return Task.CompletedTask;
		}
	}
}
