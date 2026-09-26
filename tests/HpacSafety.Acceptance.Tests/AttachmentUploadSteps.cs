using System.Globalization;
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
using HpacSafety.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The attachment scenarios that describe minting an upload through
///     <c>POST /api/v1/uploads</c>, the pre-signed PUT straight to storage,
///     <c>DELETE /api/v1/uploads</c>, and the claim a submission makes, against the
///     booted API and its S3-compatible bucket (ADR-0096, ADR-0097, ADR-0126). Every
///     file here is synthetic.
/// </summary>
/// <remarks>
///     Scenarios run in parallel against one bucket, so nothing here counts objects.
///     An assertion names the key its own scenario minted.
/// </remarks>
[Binding]
public sealed class AttachmentUploadSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

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
	private long _declaredSize;
	private string? _uploadId;
	private Uri? _uploadUrl;
	private string? _fileName;
	private string? _reportId;
	private int _abortedAfterBytes;
	private string? _mintedKey;

	// --- REQ-SUB-072: minting returns a pre-signed PUT for one quarantine key ---

	[Given(@"a member asks to upload an allowlisted file within its kind's size limit")]
	public async Task GivenAMemberAsksToUploadAnAllowlistedFile()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_declaredType = "application/pdf";
		_declaredSize = SyntheticPdf.Length;
	}

	[When(@"the API mints the upload")]
	[When(@"the API checks the declared type and size")]
	public async Task WhenTheApiMintsTheUpload()
	{
		// The declaration is all the request carries: no filename, no bytes.
		_response = await DirectUpload.Mint(_reporter!, _declaredType, _declaredSize);
		await _response.Content.LoadIntoBufferAsync();
	}

	[Then(@"the response is 201 Created with an opaque upload ID, the attachment's kind, a pre-signed PUT URL, and when that URL expires")]
	public async Task ThenTheResponseIs201WithAnUploadIdKindUrlAndExpiry()
	{
		var text = await _response!.Content.ReadAsStringAsync();
		_response.StatusCode.ShouldBe(HttpStatusCode.Created, text);
		var upload = JsonSerializer.Deserialize<JsonElement>(text);
		_uploadId = upload.GetProperty("uploadId").GetString();
		UploadId.TryParse(_uploadId, out _).ShouldBeTrue();
		upload.GetProperty("kind").GetString().ShouldBe("document");
		_uploadUrl = new Uri(upload.GetProperty("uploadUrl").GetString()!);
		upload.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow);
		upload.EnumerateObject().Select(property => property.Name)
			.ShouldBe(["uploadId", "kind", "uploadUrl", "expiresAt"], ignoreOrder: true);
	}

	[Then(@"the URL writes only the quarantine key named by that upload ID, and lives at most 15 minutes")]
	public void ThenTheUrlWritesOnlyThatQuarantineKey()
	{
		_uploadUrl!.AbsolutePath.ShouldBe($"/{BootedApi.BucketName}/quarantine/{_uploadId}");
		QueryValue(_uploadUrl, "X-Amz-Expires").ShouldNotBeNull();
		int.Parse(QueryValue(_uploadUrl, "X-Amz-Expires")!, CultureInfo.InvariantCulture)
			.ShouldBeLessThanOrEqualTo((int)BlobUrlLifetime.Maximum.TotalSeconds);
	}

	[Then(@"the URL is signed for the declared content type and the exact declared size")]
	public async Task ThenTheUrlIsSignedForTypeAndSize()
	{
		QueryValue(_uploadUrl!, "X-Amz-SignedHeaders")!.Split(';').ShouldContain("content-length");
		QueryValue(_uploadUrl!, "X-Amz-SignedHeaders")!.Split(';').ShouldContain("content-type");

		// And storage holds it to both: exactly what was declared is accepted.
		using var put = await DirectUpload.Put(_uploadUrl!, new ByteArrayContent(SyntheticPdf), _declaredType);
		put.IsSuccessStatusCode.ShouldBeTrue();
		(await StoredLength($"quarantine/{_uploadId}")).ShouldBe(SyntheticPdf.Length);
	}

	[Then(@"the request carries no filename, and none is persisted or logged for it")]
	public async Task ThenTheRequestCarriesNoFilename()
	{
		// The request is the declaration alone (DirectUpload.Mint): a type and a
		// size. The stored object carries only its type — no metadata, no
		// disposition — so there is nothing a filename could have gone into.
		var metadata = await BootedApi.Storage.GetObjectMetadataAsync(BootedApi.BucketName, $"quarantine/{_uploadId}");
		metadata.Metadata.Keys.ShouldBeEmpty();
		metadata.Headers.ContentDisposition.ShouldBeNullOrEmpty();
	}

	[Then(@"the response never echoes a client filename or a report ID")]
	public async Task ThenTheResponseNeverEchoesAFilenameOrReportId()
	{
		var text = await _response!.Content.ReadAsStringAsync();
		text.ShouldNotContain("fileName", Case.Insensitive);
		text.ShouldNotContain("reportId", Case.Insensitive);
		_response.Headers.Location.ShouldBeNull();
	}

	[Then(@"nothing is written to object storage or the database")]
	public async Task ThenNothingIsWrittenToStorageOrTheDatabase()
	{
		// Checked against a second mint, whose URL nobody uses: minting alone
		// writes nothing.
		var (untouched, _) = await DirectUpload.MintOrThrow(_reporter!, _declaredType, _declaredSize);
		(await StoredLength($"quarantine/{untouched}")).ShouldBeNull();

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.ReportFiles.IgnoreQueryFilters().AnyAsync(file => file.BlobKey.Contains(untouched))).ShouldBeFalse();
	}

	// --- REQ-SUB-073: a declaration the API will not accept gets no URL ---

	[Given(@"^a member asks to upload (a file of zero bytes|a video one byte larger than 250 MB|an image one byte larger than 25 MB|a document one byte larger than 25 MB|a file whose declared type is not on the allowlist)$")]
	public async Task GivenAMemberAsksToUpload(string file)
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		const long megabyte = 1024 * 1024;
		(_declaredType, _declaredSize) = file switch
		{
			"a file of zero bytes" => ("application/pdf", 0L),
			"a video one byte larger than 250 MB" => ("video/mp4", (250 * megabyte) + 1),
			"an image one byte larger than 25 MB" => ("image/jpeg", (25 * megabyte) + 1),
			"a document one byte larger than 25 MB" => ("application/pdf", (25 * megabyte) + 1),
			_ => ("application/x-msdownload", 10L),
		};
	}

	[Then(@"the API rejects it with a safe rejection reason of ""(.*)""")]
	public async Task ThenTheApiRejectsItWithReason(string reason)
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = JsonSerializer.Deserialize<JsonElement>(await _response.Content.ReadAsStringAsync());
		problem.GetProperty("reason").GetString().ShouldBe(reason);
	}

	[Then(@"no upload URL is minted")]
	public async Task ThenNoUploadUrlIsMinted()
	{
		var problem = JsonSerializer.Deserialize<JsonElement>(await _response!.Content.ReadAsStringAsync());
		problem.TryGetProperty("uploadUrl", out _).ShouldBeFalse();
		problem.TryGetProperty("uploadId", out _).ShouldBeFalse();
	}

	// --- REQ-SUB-074: storage accepts only the upload the URL was signed for ---

	[Given(@"the API minted an upload URL")]
	public async Task GivenTheApiMintedAnUploadUrl()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_declaredType = "application/pdf";
		_declaredSize = SyntheticPdf.Length;
		(_uploadId, _uploadUrl) = await DirectUpload.MintOrThrow(_reporter, _declaredType, _declaredSize);
		_mintedKey = $"quarantine/{_uploadId}";
	}

	[When(@"the browser sends (.*)")]
	public async Task WhenTheBrowserSends(string request)
	{
		switch (request)
		{
			case "a body larger or smaller than the declared size":
				using (var longer = await DirectUpload.Put(_uploadUrl!, new ByteArrayContent([.. SyntheticPdf, 0x20]), _declaredType))
				{
					longer.IsSuccessStatusCode.ShouldBeFalse();
				}

				_response = await DirectUpload.Put(_uploadUrl!, new ByteArrayContent(SyntheticPdf[..^1]), _declaredType);

				break;
			case "a content type other than the declared one":
				_response = await DirectUpload.Put(_uploadUrl!, new ByteArrayContent(SyntheticPdf), "image/png");
				break;
			case "the PUT after the URL has expired":
				// A URL minted by the same chokepoint the API uses, living one
				// second, so its expiry is real rather than simulated.
				await using (var scope = (await BootedApi.Factory()).Services.CreateAsyncScope())
				{
					var store = scope.ServiceProvider.GetRequiredService<IBlobStore>();
					var uploadId = UploadId.New();
					_mintedKey = BlobKey.ForUpload(uploadId).Value;
					var shortLived = await store.CreateUploadUrl(
						BlobKey.ForUpload(uploadId), _declaredType, SyntheticPdf.Length, TimeSpan.FromSeconds(1), CancellationToken.None);
					await Task.Delay(TimeSpan.FromSeconds(2.5));
					_response = await DirectUpload.Put(shortLived, new ByteArrayContent(SyntheticPdf), _declaredType);
				}

				break;
			case "the PUT to any key other than the one it was minted for":
				var elsewhere = $"quarantine/{UploadId.New().Value}";
				var retargeted = new UriBuilder(_uploadUrl!) { Path = $"/{BootedApi.BucketName}/{elsewhere}" }.Uri;
				_response = await DirectUpload.Put(retargeted, new ByteArrayContent(SyntheticPdf), _declaredType);
				(await StoredLength(elsewhere)).ShouldBeNull();
				break;
			default:
				throw new NotSupportedException($"Unmapped request: '{request}'.");
		}
	}

	[Then(@"storage refuses it")]
	public void ThenStorageRefusesIt()
	{
		_response!.IsSuccessStatusCode.ShouldBeFalse();
		((int)_response.StatusCode).ShouldBeInRange(400, 499);
	}

	[Then(@"nothing is stored under that upload's quarantine key")]
	public async Task ThenNothingIsStoredUnderThatKey()
	{
		(await StoredLength(_mintedKey!)).ShouldBeNull();
	}

	// --- REQ-SUB-043: an unauthenticated upload is rejected ---

	[Given(@"an upload request carries no bearer token")]
	public void GivenAnUploadRequestCarriesNoBearerToken()
	{
		_declaredType = "application/pdf";
		_declaredSize = SyntheticPdf.Length;
	}

	[When(@"the API receives it")]
	public async Task WhenTheApiReceivesAnAnonymousUpload()
	{
		using var anonymous = (await BootedApi.Factory()).CreateClient();
		_response = await DirectUpload.Mint(anonymous, _declaredType, _declaredSize);
	}

	[Then(@"the API rejects it before anything is written to object storage")]
	public async Task ThenTheApiRejectsItBeforeAnythingIsStored()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		(await _response.Content.ReadAsStringAsync()).ShouldNotContain("uploadUrl");
	}

	// --- REQ-MED-002: declared content type must agree with detected content type ---

	[Given(@"an attachment's declared content type differs from its detected, allowlisted type")]
	public async Task GivenADeclaredTypeThatDisagrees()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);

		// A PDF declared as a PNG, under a name whose extension agrees with the
		// declaration — so only the bytes say what it really is.
		_uploadId = await DirectUpload.Send(_reporter, UniquePdf(), "image/png");
		_fileName = "photo.png";
	}

	[When(@"the API validates the attachment")]
	public async Task WhenTheApiValidatesTheAttachment()
	{
		_response = await SubmitClaiming(_uploadId!, _fileName);
		await _response.Content.LoadIntoBufferAsync();
	}

	[Then(@"the API rejects the attachment")]
	public async Task ThenTheApiRejectsTheAttachment()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var refused = (await _response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refusedUploads").EnumerateArray().Single();
		refused.GetProperty("uploadId").GetString().ShouldBe(_uploadId);
		refused.GetProperty("reason").GetString().ShouldBe("declared_type_mismatch");
	}

	[Then(@"the file extension and client filename are never trusted as the basis for acceptance")]
	public async Task ThenTheNameIsNeverTrusted()
	{
		// The same kind of file, declared truthfully under the same misleading
		// name, is accepted as what its bytes are: the name decided neither.
		var truthful = await DirectUpload.Send(_reporter!, UniquePdf(), "application/pdf");
		using var response = await SubmitClaiming(truthful, "photo.png");
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
	}

	// --- REQ-MED-045: a sent upload waits, unvalidated, in quarantine ---

	[Given(@"a reporter's browser has sent a file through the pre-signed PUT the API minted for it")]
	public async Task GivenAReportersBrowserHasSentAFile()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);

		// Bytes no sniffer recognises, declared as a PDF: storage takes them,
		// because nothing has judged them yet.
		_bytes = Unrecognisable();
		_uploadId = await DirectUpload.Send(_reporter, _bytes, "application/pdf");
	}

	[Then(@"its bytes sit at a private quarantine key named only by the minted upload ID")]
	public async Task ThenItsBytesSitInQuarantine()
	{
		(await StoredLength($"quarantine/{_uploadId}")).ShouldBe(_bytes.Length);
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

	[Then(@"it is not validated until a submission claims it")]
	public async Task ThenItIsNotValidatedUntilClaimed()
	{
		// Stored as sent, though no sniffer recognises it; the claim is what refuses it.
		using var response = await SubmitClaiming(_uploadId!, null);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var refused = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refusedUploads").EnumerateArray().Single();
		refused.GetProperty("reason").GetString().ShouldBe("unrecognised_content");
	}

	// --- REQ-MED-016: removing an upload erases every version of it ---

	[Given(@"an unclaimed upload exists in quarantine")]
	public async Task GivenAnUnclaimedUploadExistsInQuarantine()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_uploadId = await DirectUpload.Send(_reporter, SyntheticPdf, "application/pdf");
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

	// --- REQ-MED-017: a cancelled upload leaves nothing in storage ---

	[Given(@"a reporter's upload is still being received")]
	public async Task GivenAReportersUploadIsStillBeingReceived()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_bytes = UniquePdf();
		(_uploadId, _uploadUrl) = await DirectUpload.MintOrThrow(_reporter, "application/pdf", _bytes.Length);
	}

	[When(@"the browser aborts the request")]
	public async Task WhenTheBrowserAbortsTheRequest()
	{
		// Half the file, then Cancel: the PUT to storage is aborted mid-body.
		var half = _bytes[..(_bytes.Length / 2)];
		using var abort = new CancellationTokenSource();
		using var body = new StallingContent(half, _bytes.Length, abort);

		await Should.ThrowAsync<OperationCanceledException>(() =>
			DirectUpload.Put(_uploadUrl!, body, "application/pdf", abort.Token));
		_abortedAfterBytes = half.Length;

		// Give storage a moment to observe the abort and unwind.
		await Task.Delay(TimeSpan.FromMilliseconds(250));
	}

	[Then(@"nothing is written to object storage for it")]
	public async Task ThenNothingIsWrittenToObjectStorageForIt()
	{
		_abortedAfterBytes.ShouldBeGreaterThan(0);
		(await StoredLength($"quarantine/{_uploadId}")).ShouldBeNull();
	}

	// --- The client filename is kept only as a reviewer's download name ---

	[Given(@"a submission names an attachment with a client-supplied filename")]
	public async Task GivenASubmissionNamesAnAttachmentWithAFilename()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_fileName = "pilot-smith-launch.pdf";
		_uploadId = await DirectUpload.Send(_reporter, SyntheticPdf, "application/pdf");
	}

	[Given(@"a submission names an attachment with the filename (.*)")]
	public async Task GivenASubmissionNamesAnAttachmentWithTheFilename(string fileName)
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_fileName = fileName == "(blank)" ? "   " : fileName;
		_uploadId = await DirectUpload.Send(_reporter, SyntheticPdf, "application/pdf");
	}

	[Given(@"a submission claims an accepted upload")]
	public async Task GivenASubmissionClaimsAnAcceptedUpload()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_fileName = "launch-site.pdf";
		_uploadId = await DirectUpload.Send(_reporter, SyntheticPdf, "application/pdf");
	}

	[When(@"the API claims the attachment")]
	[When(@"the API ingests it")]
	public async Task WhenTheApiClaimsTheAttachment()
	{
		using var response = await SubmitClaiming(_uploadId!, _fileName);
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

	/// <summary>Submits a report whose one file-upload answer claims <paramref name="uploadId" />.</summary>
	private async Task<HttpResponseMessage> SubmitClaiming(string uploadId,
														   string? fileName)
	{
		var consent = await ReportSubmissionEndpointSteps.ConsentRevisionId();
		var fileRevision = await FileUploadRevisionId();
		var dto = new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consent, value = (bool?)true },
				new { questionRevisionId = fileRevision, attachments = new[] { new { uploadId, fileName } } },
			},
		};

		using var content = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), System.Text.Encoding.UTF8, "application/json");
		return await _reporter!.PostAsync(Submit, content);
	}

	private static async Task<long?> StoredLength(string key)
	{
		try
		{
			return (await BootedApi.Storage.GetObjectMetadataAsync(BootedApi.BucketName, key)).ContentLength;
		}
		catch (AmazonS3Exception missing) when (missing.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	private static string? QueryValue(Uri url,
									  string name)
	{
		return url.Query.TrimStart('?').Split('&')
			.Select(pair => pair.Split('=', 2))
			.Where(pair => string.Equals(pair[0], name, StringComparison.Ordinal))
			.Select(pair => Uri.UnescapeDataString(pair[1]))
			.SingleOrDefault();
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
	///     A body that promises its full length, sends part of it, then stalls and
	///     cancels — a browser whose reporter pressed Cancel partway through.
	/// </summary>
	private sealed class StallingContent(byte[] prefix,
										 long declaredLength,
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
			length = declaredLength;
			return true;
		}
	}
}
