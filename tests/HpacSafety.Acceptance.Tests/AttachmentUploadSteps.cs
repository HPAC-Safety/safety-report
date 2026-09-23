using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The attachment scenarios that describe <c>POST</c> and
///     <c>DELETE /api/v1/uploads</c> and the claim a submission makes, against the
///     booted API and its MinIO bucket (ADR-0096, ADR-0097). Every file here is
///     synthetic.
/// </summary>
/// <remarks>
///     Scenarios run in parallel against one bucket, so nothing here counts objects.
///     An assertion names the key its own scenario produced, or — where a refusal
///     leaves no key — looks for an object of a length only that scenario sent.
/// </remarks>
[Binding]
public sealed class AttachmentUploadSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri Uploads = new("/api/v1/uploads", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	// A PDF is the one allowlisted format whose bytes are plain text, so a
	// synthetic file needs no imaging library.
	private static readonly byte[] SyntheticPdf =
		"%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray();

	private HttpClient? _reporter;
	private HttpResponseMessage? _response;
	private byte[] _bytes = SyntheticPdf;
	private string _declaredType = "application/pdf";
	private string? _uploadId;
	private string? _fileName;
	private string? _reportId;
	private int _abortedAfterBytes;
	private bool _oversized;

	// --- An accepted upload returns an opaque upload ID; a refused one is never stored ---

	[Given(@"a member uploads (.*)")]
	public async Task GivenAMemberUploads(string file)
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_oversized = file == "a file one byte larger than 50 MB";
		(_bytes, _declaredType) = file switch
		{
			"an allowlisted file within the size limit" => (SyntheticPdf, "application/pdf"),
			"an empty file" => ([], "application/pdf"),
			// Unique bytes, so "nothing was stored" can look for exactly them.
			"a file whose bytes match no known format" => (Unrecognisable(), "application/pdf"),
			"a file declared as one allowlisted type but containing another" => (UniquePdf(), "image/png"),
			// Sent as a generated stream, not an array: this suite also measures
			// allocation (REQ-MED-024), and a 50 MB array here would skew it.
			"a file one byte larger than 50 MB" => ([], "application/pdf"),
			_ => throw new NotSupportedException($"Unmapped upload example: '{file}'."),
		};
	}

	[When(@"the API accepts the upload")]
	[When(@"the API validates the upload")]
	public async Task WhenTheApiReceivesIt()
	{
		HttpContent body = _bytes.Length == 0 && _declaredType == "application/pdf" && _oversized
			? new StreamContent(new GeneratedStream((50L * 1024 * 1024) + 1))
			: new ByteArrayContent(_bytes);
		body.Headers.ContentType = new MediaTypeHeaderValue(_declaredType);
		_response = await _reporter!.PostAsync(Uploads, body);
		await _response.Content.LoadIntoBufferAsync();
	}

	[Then(@"the response is 201 Created with an opaque upload ID and the attachment's kind")]
	public async Task ThenTheResponseIs201WithAnOpaqueUploadId()
	{
		var text = await _response!.Content.ReadAsStringAsync();
		_response.StatusCode.ShouldBe(HttpStatusCode.Created, text);
		var upload = JsonSerializer.Deserialize<JsonElement>(text);
		UploadId.TryParse(upload.GetProperty("uploadId").GetString(), out _).ShouldBeTrue();
		upload.GetProperty("kind").GetString().ShouldBe("document");
	}

	[Then(@"the response never echoes a client filename, storage key, or URL")]
	public async Task ThenTheResponseNeverEchoesAFilenameKeyOrUrl()
	{
		var upload = JsonSerializer.Deserialize<JsonElement>(await _response!.Content.ReadAsStringAsync());
		upload.EnumerateObject().Select(property => property.Name).ShouldBe(["uploadId", "kind"], ignoreOrder: true);
		_response.Headers.Location.ShouldBeNull();
	}

	[Then(@"the API rejects it with a safe rejection reason of ""(.*)""")]
	public async Task ThenTheApiRejectsItWithReason(string reason)
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await _response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("reason").GetString().ShouldBe(reason);
	}

	[Then(@"nothing is written to object storage")]
	public async Task ThenNothingIsWrittenToObjectStorage()
	{
		// The endpoint writes only after it has accepted a file, so a refusal
		// carries no id to look up. What can be checked is that no object of
		// exactly these bytes' length reached quarantine.
		(await QuarantineHoldsObjectOfSize(_oversized ? (50L * 1024 * 1024) + 1 : _bytes.Length)).ShouldBeFalse();
	}

	// --- An unauthenticated upload is rejected ---

	[Given(@"an upload request carries no bearer token")]
	public void GivenAnUploadRequestCarriesNoBearerToken()
	{
		_bytes = Unrecognisable();
	}

	[When(@"the API receives it")]
	public async Task WhenTheApiReceivesAnAnonymousUpload()
	{
		using var anonymous = (await BootedApi.Factory()).CreateClient();
		using var body = new ByteArrayContent(_bytes);
		body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
		_response = await anonymous.PostAsync(Uploads, body);
	}

	[Then(@"the API rejects it before anything is written to object storage")]
	public async Task ThenTheApiRejectsItBeforeAnythingIsStored()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await QuarantineHoldsObjectOfSize(_bytes.Length)).ShouldBeFalse();
	}

	// --- An accepted upload waits in a private quarantine compartment ---

	[Given(@"a reporter's upload passes the size bound and validation")]
	public async Task GivenAReportersUploadPassesValidation()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
	}

	[When(@"the API stores it")]
	public async Task WhenTheApiStoresIt()
	{
		_uploadId = await Upload(SyntheticPdf, "application/pdf");
	}

	[Then(@"its bytes are written to a private quarantine key named only by a minted upload ID")]
	public async Task ThenItsBytesAreWrittenToQuarantine()
	{
		var stored = await BootedApi.Storage.GetObjectMetadataAsync(BootedApi.BucketName, $"quarantine/{_uploadId}");
		stored.ContentLength.ShouldBe(SyntheticPdf.Length);
		BlobKey.Parse($"quarantine/{_uploadId}").ReportId.ShouldBeNull();
	}

	[Then(@"no database row, report, or member is linked to it")]
	public async Task ThenNoRowReportOrMemberIsLinkedToIt()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.ReportFiles.IgnoreQueryFilters().AnyAsync(file => file.BlobKey.Contains(_uploadId!))).ShouldBeFalse();

		// And the object itself carries no metadata that could name a member.
		var stored = await BootedApi.Storage.GetObjectMetadataAsync(BootedApi.BucketName, $"quarantine/{_uploadId}");
		stored.Metadata.Keys.ShouldBeEmpty();
	}

	[Then(@"no reviewer link can be issued for it")]
	public void ThenNoReviewerLinkCanBeIssuedForIt()
	{
		ReviewerMediaLink.IsViewable(BlobKey.Parse($"quarantine/{_uploadId}")).ShouldBeFalse();
	}

	// --- Removing an upload erases every version of it ---

	[Given(@"an unclaimed upload exists in quarantine")]
	public async Task GivenAnUnclaimedUploadExistsInQuarantine()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_uploadId = await Upload(SyntheticPdf, "application/pdf");
	}

	[When(@"the reporter's browser deletes it")]
	public async Task WhenTheReportersBrowserDeletesIt()
	{
		_response = await _reporter!.DeleteAsync(new Uri($"/api/v1/uploads/{_uploadId}", UriKind.Relative));
	}

	[Then(@"every stored version of that quarantine object is deleted at once")]
	public async Task ThenEveryVersionIsDeleted()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var versions = await BootedApi.Storage.ListVersionsAsync(new ListVersionsRequest
		{
			BucketName = BootedApi.BucketName,
			Prefix = $"quarantine/{_uploadId}",
		});
		(versions.Versions ?? []).ShouldBeEmpty();
	}

	[Then(@"deleting it again succeeds without error")]
	public async Task ThenDeletingItAgainSucceeds()
	{
		using var again = await _reporter!.DeleteAsync(new Uri($"/api/v1/uploads/{_uploadId}", UriKind.Relative));
		again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	// --- A cancelled upload leaves nothing in storage ---

	[Given(@"a reporter's upload is still being received")]
	public async Task GivenAReportersUploadIsStillBeingReceived()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
	}

	[When(@"the browser aborts the request")]
	public async Task WhenTheBrowserAbortsTheRequest()
	{
		var prefix = UniquePdf();
		using var abort = new CancellationTokenSource();
		using var body = new StallingContent(prefix, abort);
		body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

		await Should.ThrowAsync<OperationCanceledException>(() => _reporter!.PostAsync(Uploads, body, abort.Token));
		_abortedAfterBytes = prefix.Length;

		// Give the server a moment to observe the abort and unwind.
		await Task.Delay(TimeSpan.FromMilliseconds(250));
	}

	[Then(@"nothing is written to object storage for it")]
	public async Task ThenNothingIsWrittenToObjectStorageForIt()
	{
		(await QuarantineHoldsObjectOfSize(_abortedAfterBytes)).ShouldBeFalse();
	}

	// --- The client filename is kept only as a reviewer's download name ---

	[Given(@"a submission names an attachment with a client-supplied filename")]
	public async Task GivenASubmissionNamesAnAttachmentWithAFilename()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_fileName = "pilot-smith-launch.pdf";
		_uploadId = await Upload(SyntheticPdf, "application/pdf");
	}

	[Given(@"a submission names an attachment with the filename (.*)")]
	public async Task GivenASubmissionNamesAnAttachmentWithTheFilename(string fileName)
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_fileName = fileName == "(blank)" ? "   " : fileName;
		_uploadId = await Upload(SyntheticPdf, "application/pdf");
	}

	[Given(@"a submission claims an accepted upload")]
	public async Task GivenASubmissionClaimsAnAcceptedUpload()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_fileName = "launch-site.pdf";
		_uploadId = await Upload(SyntheticPdf, "application/pdf");
	}

	[When(@"the API claims the attachment")]
	[When(@"the API ingests it")]
	public async Task WhenTheApiClaimsTheAttachment()
	{
		var consent = await ReportSubmissionEndpointSteps.ConsentRevisionId();
		var fileRevision = await FileUploadRevisionId();

		var dto = new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consent, value = (string?)"yes" },
				new
				{
					questionRevisionId = fileRevision,
					attachments = new[] { new { uploadId = _uploadId!, fileName = _fileName } },
				},
			},
		};

		using var content = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), System.Text.Encoding.UTF8, "application/json");
		using var response = await _reporter!.PostAsync(Submit, content);
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		_reportId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
	}

	[Then(@"the sanitized filename is stored on the report file")]
	public async Task ThenTheSanitizedFilenameIsStored()
	{
		(await ClaimedFile()).OriginalFileName.ShouldBe(_fileName);
	}

	[Then(@"the stored filename is (.*)")]
	public async Task ThenTheStoredFilenameIs(string expected)
	{
		(await ClaimedFile()).OriginalFileName.ShouldBe(expected == "(none)" ? null : expected);
	}

	[Then(@"it is not logged, placed in an exception, used in a key, sent to the model, or included in any public DTO")]
	public async Task ThenTheFilenameReachesNothingElse()
	{
		// The key is asserted here. Logging and exceptions never receive it:
		// the submission endpoint's only log line names an exception type, and
		// its problems are fixed strings. The model DTO and public DTOs select
		// no report-file column at all (REQ-MED-014, ai-anonymization).
		var file = await ClaimedFile();
		file.BlobKey.ShouldNotContain("smith");
		(file.StrippedBlobKey ?? string.Empty).ShouldNotContain("smith");
	}

	[Then(@"the object key encodes only an opaque upload, report, or file identity and a managed compartment")]
	public async Task ThenTheObjectKeyIsOpaque()
	{
		var file = await ClaimedFile();
		var key = BlobKey.Parse(file.BlobKey);
		key.ReportId.ShouldBe(_reportId);
		key.FileName.ShouldBe(file.Id.Value);
	}

	[Then(@"the upload's bytes are copied unchanged, inside storage, to the report's original compartment")]
	public async Task ThenTheUploadIsCopiedUnchanged()
	{
		var file = await ClaimedFile();
		BlobKey.Parse(file.BlobKey).Compartment.ShouldBe(MediaCompartment.Original);

		using var stored = await BootedApi.Storage.GetObjectAsync(BootedApi.BucketName, file.BlobKey);
		using var bytes = new MemoryStream();
		await stored.ResponseStream.CopyToAsync(bytes);
		bytes.ToArray().ShouldBe(SyntheticPdf);
		stored.Headers.ContentType.ShouldBe("application/pdf");
	}

	[Then(@"the original is named by the report file's own id, never by the upload ID or the reporter's filename")]
	public async Task ThenTheOriginalIsNamedByTheFileId()
	{
		var file = await ClaimedFile();
		file.BlobKey.ShouldBe($"{_reportId}/original/{file.Id}");
		file.BlobKey.ShouldNotContain(_uploadId!);
		file.BlobKey.ShouldNotContain("launch");
	}

	[Then(@"no derivative is written before the Worker processes the file")]
	public async Task ThenNoDerivativeIsWrittenBeforeTheWorker()
	{
		var file = await ClaimedFile();
		file.AwaitsStripping.ShouldBeTrue();
		var listing = await BootedApi.Storage.ListObjectsV2Async(new ListObjectsV2Request
		{
			BucketName = BootedApi.BucketName,
			Prefix = $"{_reportId}/stripped/",
		});
		(listing.S3Objects ?? []).ShouldBeEmpty();
	}

	private async Task<string> Upload(byte[] bytes,
									  string declaredType)
	{
		using var body = new ByteArrayContent(bytes);
		body.Headers.ContentType = new MediaTypeHeaderValue(declaredType);
		using var response = await _reporter!.PostAsync(Uploads, body);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadId").GetString()!;
	}

	private async Task<ReportFile> ClaimedFile()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.ReportFiles.SingleAsync(file => file.ReportId == TinyId.Parse(_reportId!));
	}

	private static async Task<string> FileUploadRevisionId()
	{
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		using var response = await admin.PostAsJsonAsync(new Uri("/api/admin/questions", UriKind.Relative), new
		{
			key,
			type = "file_upload",
			labelEn = "Attach a photo",
			labelFr = "Joindre une photo",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = Array.Empty<object>(),
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionId").GetString()!;
	}

	private static async Task<bool> QuarantineHoldsObjectOfSize(long size)
	{
		var request = new ListObjectsV2Request { BucketName = BootedApi.BucketName, Prefix = "quarantine/" };
		ListObjectsV2Response page;

		do
		{
			page = await BootedApi.Storage.ListObjectsV2Async(request);
			if ((page.S3Objects ?? []).Any(entry => entry.Size == size))
			{
				return true;
			}

			request.ContinuationToken = page.NextContinuationToken;
		}
		while (page.IsTruncated == true);

		return false;
	}

	// A valid PDF of a length no other scenario is likely to upload, so "no
	// object of this size" is a check on this scenario alone.
	private static byte[] UniquePdf()
	{
		var padding = new string('x', 5000 + Random.Shared.Next(1, 4000));
		return System.Text.Encoding.ASCII.GetBytes($"%PDF-1.7\n%{padding}\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");
	}

	// Random bytes of an odd length no other scenario uses, starting with a
	// byte no format signature begins with.
	private static byte[] Unrecognisable()
	{
		var bytes = new byte[4099 + Random.Shared.Next(1, 997)];
		Random.Shared.NextBytes(bytes);
		bytes[0] = 0x00;
		bytes[1] = 0x13;
		return bytes;
	}

	/// <summary>
	///     A body that sends its bytes, then stalls and cancels — a browser whose
	///     reporter pressed Cancel partway through.
	/// </summary>
	private sealed class StallingContent(byte[] prefix,
										 CancellationTokenSource abort) : HttpContent
	{
		protected override async Task SerializeToStreamAsync(Stream stream,
															 TransportContext? context)
		{
			await stream.WriteAsync(prefix);
			await stream.FlushAsync();
			await abort.CancelAsync();
			await Task.Delay(Timeout.Infinite, abort.Token);
		}

		protected override bool TryComputeLength(out long length)
		{
			length = -1;
			return false;
		}
	}

	/// <summary>A readable stream of a given length that holds none of its bytes.</summary>
	private sealed class GeneratedStream(long length) : Stream
	{
		private long _position;

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => length;

		public override long Position
		{
			get => _position;
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer,
								 int offset,
								 int count)
		{
			var served = (int)Math.Min(count, length - _position);
			Array.Clear(buffer, offset, served);
			_position += served;
			return served;
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset,
								  SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer,
								   int offset,
								   int count)
		{
			throw new NotSupportedException();
		}
	}
}
