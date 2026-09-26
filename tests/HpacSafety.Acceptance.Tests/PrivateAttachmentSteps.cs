using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.PrivateAttachments;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Staff-only private attachments on a report, through the booted API and the
///     S3-compatible server behind it (REQ-MED-046..051, REQ-MOD-107..112,
///     REQ-MOD-114, ADR-0135).
/// </summary>
/// <remarks>
///     Each reviewer is a token for a fresh subject, so a scenario can tell who
///     added or removed what. Every file is synthetic random bytes.
/// </remarks>
[Binding]
public sealed partial class PrivateAttachmentSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string Description = "Synthetic: received from the coroner.";
	private const string PrivateFileName = "Synthetic coroner report.zip";
	private const long Megabyte = 1024L * 1024;
	private const long Gigabyte = 1024L * Megabyte;

	private readonly string _officer = $"officer:{Guid.NewGuid():n}";
	private readonly string _administrator = $"admin:{Guid.NewGuid():n}";
	private readonly List<HttpStatusCode> _answers = [];
	private readonly List<string> _publicBodies = [];
	private readonly List<(string Id, string FileName, string? Description, string AddedBy, long ByteSize)> _added = [];
	private readonly List<JsonElement> _links = [];

	private WebApplicationFactory<Program>? _host;
	private string _reportId = string.Empty;
	private string _otherReportId = string.Empty;
	private string _attachmentId = string.Empty;
	private string _otherAttachmentId = string.Empty;
	private string _noteId = string.Empty;
	private string _uploadId = string.Empty;
	private byte[] _bytes = [];
	private long _cap = PrivateAttachmentPolicyDefaults.MaxByteSize;
	private int _outboxBefore;
	private HttpResponseMessage? _response;
	private JsonElement _minted;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"^the private attachment cap is configured as (.+)$")]
	public async Task GivenTheCapIsConfigured(string cap)
	{
		_cap = Size(cap);
		var booted = await BootedApi.Factory();

		// The default needs no override; any other cap is configuration alone.
		_host = _cap == PrivateAttachmentPolicyDefaults.MaxByteSize
			? booted
			: booted.WithWebHostBuilder(builder =>
				builder.UseSetting("HpacSafety:Media:PrivateAttachments:MaxByteSize", _cap.ToString(System.Globalization.CultureInfo.InvariantCulture)));
	}

	[Given(@"^a (pending|published|unpublished|summary-failed|no-consent) report that staff add private attachments to$")]
	public async Task GivenAReportInStatus(string status)
	{
		_reportId = status switch
		{
			"pending" => await BootedReports.Seed(ReportStatus.Pending, true),
			"published" => await BootedReports.Seed(ReportStatus.Published, true),
			"unpublished" => await BootedReports.Seed(ReportStatus.Unpublished, true),
			"summary-failed" => await BootedReports.Seed(ReportStatus.SummaryFailed, true),
			_ => await BootedReports.Seed(ReportStatus.Unpublished, false),
		};
	}

	[Given(@"a report carrying one private attachment")]
	public async Task GivenAReportCarryingOneAttachment()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Pending, true);
		_attachmentId = await AddThroughTheApi(_officer, "safety_officer", PrivateFileName, Description);
	}

	[Given(@"^a report carries the private attachment ""(.+)""$")]
	public async Task GivenAReportCarriesTheAttachment(string fileName)
	{
		_reportId = await BootedReports.Seed(ReportStatus.Pending, true);
		_attachmentId = await AddThroughTheApi(_officer, "safety_officer", fileName, null);
	}

	[Given(@"a safety officer's browser has sent a zip archive through a private upload minted for a report")]
	public async Task GivenAZipWasSent()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Pending, true);
		_bytes = Synthetic(64 * 1024, zip: true);
		_uploadId = await Send(_officer, "safety_officer", _bytes, "application/zip");
	}

	[Given(@"a safety officer's browser has sent a file through a private upload minted for a report")]
	public async Task GivenAFileWasSent()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Pending, true);
		_bytes = Synthetic(4 * 1024);
		_uploadId = await Send(_officer, "safety_officer", _bytes, string.Empty);
	}

	[Given(@"a published report whose reporter consented to publication and media carries one private attachment")]
	public async Task GivenAPublishedConsentedReportWithAnAttachment()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Published, true, report => BootedReports.AddProcessedImage(report), mediaConsent: true);
		_attachmentId = await AddThroughTheApi(_officer, "safety_officer", PrivateFileName, Description);
	}

	[Given(@"a report carrying one private attachment, and another report carrying one of its own")]
	public async Task GivenTwoReportsWithAttachments()
	{
		await GivenAReportCarryingOneAttachment();
		var first = _reportId;

		_reportId = await BootedReports.Seed(ReportStatus.Pending, true);
		_otherAttachmentId = await AddThroughTheApi(_officer, "safety_officer", "Synthetic other.pdf", null);
		_otherReportId = _reportId;
		_reportId = first;
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"^a safety officer declares an? (.+) file of (.+) for that report's private attachments$")]
	public async Task WhenAnOfficerDeclaresATypedFile(string declared, string size)
	{
		var contentType = declared switch
		{
			"typeless" => string.Empty,
			"malformed-type" => "not a type",
			_ => declared,
		};

		using var officer = Reviewer(_officer, "safety_officer");
		_response = await officer.PostAsJsonAsync(UploadsUri(), new { contentType, byteSize = Size(size) });
	}

	[When(@"^a safety officer declares a file of (.+) for that report's private attachments$")]
	public async Task WhenAnOfficerDeclaresAFile(string size)
	{
		using var officer = Reviewer(_officer, "safety_officer");
		_response = await officer.PostAsJsonAsync(UploadsUri(), new { contentType = "application/zip", byteSize = Size(size) });
	}

	[When(@"^the safety officer adds that upload to the report as ""(.+)""$")]
	public async Task WhenTheOfficerAddsThatUpload(string fileName)
	{
		_outboxBefore = await OutboxCount();
		using var officer = Reviewer(_officer, "safety_officer");
		using var added = await officer.PostAsJsonAsync(AttachmentsUri(), new { uploadId = _uploadId, fileName });
		added.StatusCode.ShouldBe(HttpStatusCode.Created, await added.Content.ReadAsStringAsync());
		_attachmentId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
	}

	[When(@"a safety officer asks for its download link twice")]
	public async Task WhenAnOfficerAsksForTheLinkTwice()
	{
		using var officer = Reviewer(_officer, "safety_officer");

		for (var attempt = 0; attempt < 2; attempt++)
		{
			using var link = await officer.GetAsync(DownloadUri());
			link.StatusCode.ShouldBe(HttpStatusCode.OK);
			_links.Add(await link.Content.ReadFromJsonAsync<JsonElement>());
		}
	}

	[When(@"^(an anonymous visitor|a User|a SafetyOfficer|an Administrator) mints a private upload for, adds, lists, downloads, and removes private attachments on it$")]
	public async Task WhenSomeoneUsesEveryRoute(string who)
	{
		var reviewer = who switch
		{
			"a SafetyOfficer" => (_officer, "safety_officer"),
			"an Administrator" => ((string, string)?)(_administrator, "administrator"),
			_ => null,
		};

		// A reviewer claims a real upload; anyone else is refused before the body matters.
		var uploadId = reviewer is { } staff
			? await Send(staff.Item1, staff.Item2, Synthetic(1024), "application/pdf")
			: UploadId.New().Value;

		using var client = await ClientFor(who);

		using (var minted = await client.PostAsJsonAsync(UploadsUri(), new { contentType = "application/zip", byteSize = 10 }))
		{
			_answers.Add(minted.StatusCode);
		}

		using (var added = await client.PostAsJsonAsync(AttachmentsUri(), new { uploadId, fileName = "Synthetic.pdf" }))
		{
			_answers.Add(added.StatusCode);
		}

		using (var listed = await client.GetAsync(AttachmentsUri()))
		{
			_answers.Add(listed.StatusCode);
		}

		using (var downloaded = await client.GetAsync(DownloadUri()))
		{
			_answers.Add(downloaded.StatusCode);
		}

		using (var removed = await client.DeleteAsync(AttachmentUri()))
		{
			_answers.Add(removed.StatusCode);
		}
	}

	[When(@"a safety officer adds a private attachment with a description and then an administrator adds one without")]
	public async Task WhenStaffAddTwo()
	{
		_outboxBefore = await OutboxCount();

		foreach (var (subject, role, fileName, description) in new[]
				 {
					 (_officer, "safety_officer", "Synthetic police report.pdf", (string?)Description),
					 (_administrator, "administrator", "Synthetic archive.zip", null),
				 })
		{
			var id = await AddThroughTheApi(subject, role, fileName, description);
			_added.Add((id, fileName, description, subject, _bytes.Length));

			// Newest first is by when each was added; keep them apart.
			await Task.Delay(TimeSpan.FromMilliseconds(5));
		}
	}

	[When(@"an administrator removes that private attachment")]
	public async Task WhenAnAdministratorRemoves()
	{
		using var admin = Reviewer(_administrator, "administrator");
		using var removed = await admin.DeleteAsync(AttachmentUri());
		removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[When(@"^a safety officer adds a private attachment whose (file name|description|upload) is (.+)$")]
	public async Task WhenAnOfficerAddsAnInvalidAttachment(string field, string value)
	{
		var uploadId = field == "upload"
			? (await MintOnly(_officer, "safety_officer", "application/zip", 100)).UploadId
			: await Send(_officer, "safety_officer", Synthetic(100), "application/zip");

		var (fileName, description) = (field, value) switch
		{
			("file name", "empty") => (string.Empty, (string?)null),
			("file name", "only reserved characters") => ("\"/:*?<>|;", null),
			("description", "501 characters long") => ("Synthetic.zip", new string('a', PrivateAttachment.DescriptionMaxLength + 1)),
			("upload", "one that was never sent") => ("Synthetic.zip", null),
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Not an example this step knows."),
		};

		using var officer = Reviewer(_officer, "safety_officer");
		_response = await officer.PostAsJsonAsync(AttachmentsUri(), new { uploadId, fileName, description });
	}

	[When(@"a safety officer deletes the report carrying that private attachment")]
	public async Task WhenAnOfficerDeletesTheReport()
	{
		using var officer = Reviewer(_officer, "safety_officer");
		using var deleted = await officer.DeleteAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[When(@"an anonymous visitor and a User read the public feed, that report's public page, and its public media")]
	public async Task WhenPublicReadersRead()
	{
		using var anonymous = (await BootedApi.Factory()).CreateClient();
		using var user = await BootedApi.SignedInAs(MemberRole.User);

		foreach (var client in new[] { anonymous, user })
		{
			foreach (var path in new[] { "/api/v1/public/reports", $"/api/v1/public/reports/{_reportId}" })
			{
				using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
				response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
				_publicBodies.Add(await response.Content.ReadAsStringAsync());
			}

			// The report's public media: each file it lists, by its public link.
			var body = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/public/reports/{_reportId}", UriKind.Relative));
			foreach (var media in body.GetProperty("media").EnumerateArray())
			{
				using var link = await client.GetAsync(new Uri($"/api/v1/public/reports/{_reportId}/media/{media.GetProperty("id").GetString()}", UriKind.Relative));
				link.StatusCode.ShouldBe(HttpStatusCode.OK);
				_publicBodies.Add(await link.Content.ReadAsStringAsync());
			}
		}
	}

	[When(@"a safety officer adds a private note referring to the first report's private attachment")]
	public async Task WhenAnOfficerAddsANoteReferringToIt()
	{
		using var officer = Reviewer(_officer, "safety_officer");
		using var added = await officer.PostAsJsonAsync(NotesUri(), new { text = "Synthetic: see the coroner's report.", attachmentId = _attachmentId });
		added.StatusCode.ShouldBe(HttpStatusCode.Created, await added.Content.ReadAsStringAsync());
		_noteId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
	}

	[When(@"an administrator edits that private note to refer to no private attachment")]
	public async Task WhenAnAdministratorDropsTheReference()
	{
		using var admin = Reviewer(_administrator, "administrator");
		using var edited = await admin.PutAsJsonAsync(
			new Uri($"{NotesUri()}/{_noteId}", UriKind.Relative),
			new { text = "Synthetic: the coroner's report was not needed.", revision = 1, attachmentId = (string?)null });
		edited.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"the API mints a pre-signed PUT to a quarantine key named only by a new upload ID")]
	public async Task ThenAPutToQuarantineIsMinted()
	{
		var body = await _response!.Content.ReadAsStringAsync();
		_response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
		_minted = JsonDocument.Parse(body).RootElement.Clone();
		_uploadId = _minted.GetProperty("uploadId").GetString()!;
		var url = new Uri(_minted.GetProperty("uploadUrl").GetString()!);

		url.AbsolutePath.ShouldEndWith($"/quarantine/{_uploadId}");
		url.AbsolutePath.ShouldNotContain(_reportId);
		int.Parse(Query(url, "X-Amz-Expires")!, System.Globalization.CultureInfo.InvariantCulture)
			.ShouldBeLessThanOrEqualTo((int)BlobUrlLifetime.Maximum.TotalSeconds, url.Query);
		(await StoredLength($"quarantine/{_uploadId}")).ShouldBeNull();
	}

	[Then(@"^the PUT is signed for the content type (.+) and exactly (.+)$")]
	public async Task ThenThePutIsSignedFor(string contentType, string size)
	{
		var minted = _minted;
		var url = new Uri(minted.GetProperty("uploadUrl").GetString()!);
		minted.GetProperty("contentType").GetString().ShouldBe(contentType);

		var headers = Query(url, "X-Amz-SignedHeaders")!.Split(';');
		headers.ShouldContain("content-length");
		headers.ShouldContain("content-type");

		var byteSize = Size(size);
		if (byteSize > Megabyte)
		{
			// Too large to send here; the signed headers above are what storage checks.
			return;
		}

		// Storage refuses one byte short and takes exactly the declared size.
		using (var shorter = await DirectUpload.Put(url, new ByteArrayContent(new byte[byteSize - 1]), contentType))
		{
			shorter.IsSuccessStatusCode.ShouldBeFalse();
		}

		using var exact = await DirectUpload.Put(url, new ByteArrayContent(new byte[byteSize]), contentType);
		exact.IsSuccessStatusCode.ShouldBeTrue();
	}

	[Then(@"nothing about the upload is written to the database")]
	public async Task ThenNothingIsWritten()
	{
		(await StoredCount()).ShouldBe(0);
	}

	[Then(@"^the API answers 400 with the reason (.+) and mints nothing$")]
	public async Task ThenRefusedWithReason(string reason)
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await _response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("reason").GetString().ShouldBe(reason);
		problem.TryGetProperty("uploadUrl", out _).ShouldBeFalse();
		(await StoredCount()).ShouldBe(0);
	}

	[Then(@"^a file of exactly (.+) is minted$")]
	public async Task ThenAFileOfExactlyTheCapIsMinted(string cap)
	{
		Size(cap).ShouldBe(_cap);
		using var officer = Reviewer(_officer, "safety_officer");
		using var minted = await officer.PostAsJsonAsync(UploadsUri(), new { contentType = "application/zip", byteSize = _cap });
		minted.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Then(@"its bytes sit, byte for byte and nowhere else on the report, at the report's private key named by the attachment's id")]
	public async Task ThenItsBytesSitInThePrivateCompartment()
	{
		var key = $"{_reportId}/private/{_attachmentId}";
		(await Read(key)).ShouldBe(_bytes);

		var listed = await BootedApi.Storage.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BootedApi.BucketName, Prefix = $"{_reportId}/" });
		(listed.S3Objects ?? []).Select(stored => stored.Key).ShouldBe([key]);

		var stored = await Stored();
		stored.BlobKey.ShouldBe(key);
		stored.ByteSize.ShouldBe(_bytes.Length);
		stored.ContentType.ShouldBe("application/zip");
		stored.AddedBySubject.ShouldBe(_officer);
	}

	[Then(@"the upload no longer sits in quarantine")]
	public async Task ThenTheUploadIsReleased()
	{
		(await StoredLength($"quarantine/{_uploadId}")).ShouldBeNull();
	}

	[Then(@"no reporter attachment, derivative, or outbox message was created for it")]
	public async Task ThenNoReporterAttachmentOrWork()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);

		(await database.ReportFiles.IgnoreQueryFilters().CountAsync(file => file.ReportId == reportId)).ShouldBe(0);
		(await OutboxCount()).ShouldBe(_outboxBefore);
	}

	[Then(@"^each link is a pre-signed GET that lives at most 15 minutes and forces a download named ""(.+)""$")]
	public void ThenEachLinkForcesADownload(string fileName)
	{
		_links.Count.ShouldBe(2);

		foreach (var link in _links)
		{
			var url = new Uri(link.GetProperty("url").GetString()!);
			link.GetProperty("fileName").GetString().ShouldBe(fileName);
			int.Parse(Query(url, "X-Amz-Expires")!, System.Globalization.CultureInfo.InvariantCulture).ShouldBeLessThanOrEqualTo(900);
			link.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(15));
			url.AbsolutePath.ShouldEndWith($"/{_reportId}/private/{_attachmentId}");
		}
	}

	[Then(@"the bytes each link serves are identical to those uploaded")]
	public async Task ThenTheBytesAreIdentical()
	{
		using var storage = new HttpClient();

		foreach (var link in _links)
		{
			using var served = await storage.GetAsync(new Uri(link.GetProperty("url").GetString()!));
			served.StatusCode.ShouldBe(HttpStatusCode.OK);
			(await served.Content.ReadAsByteArrayAsync()).ShouldBe(_bytes);

			var disposition = served.Content.Headers.ContentDisposition!;
			disposition.DispositionType.ShouldBe("attachment");
			(disposition.FileNameStar ?? disposition.FileName!.Trim('"')).ShouldBe(link.GetProperty("fileName").GetString());
		}
	}

	[Then(@"two DownloadedPrivateAttachment audit entries record the safety officer's token subject and the attachment")]
	public async Task ThenTwoDownloadsAreAudited()
	{
		var entries = await AuditEntries(AuditAction.DownloadedPrivateAttachment);
		entries.Count.ShouldBe(2);
		entries.ShouldAllBe(entry => entry.ActorSubject == _officer && entry.TargetType == "PrivateAttachment");
		entries.ShouldAllBe(entry => (entry.Detail ?? string.Empty).Length == 0);
	}

	[Then(@"its bytes wait under the quarantine key named by the minted upload ID, with no row or report linked to them")]
	public async Task ThenTheBytesWaitInQuarantine()
	{
		(await StoredLength($"quarantine/{_uploadId}")).ShouldBe(_bytes.Length);
		BlobKey.Parse($"quarantine/{_uploadId}").ReportId.ShouldBeNull();
		(await StoredCount()).ShouldBe(0);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.PrivateAttachments.IgnoreQueryFilters().AnyAsync(attachment => attachment.BlobKey.Contains(_uploadId))).ShouldBeFalse();

		var stored = await BootedApi.Storage.GetObjectMetadataAsync(BootedApi.BucketName, $"quarantine/{_uploadId}");
		stored.Metadata.Keys.ShouldBeEmpty();
	}

	[Then(@"that key falls under the storage lifecycle rule that expires every unclaimed quarantine upload")]
	public void ThenTheLifecycleRuleCoversIt()
	{
		// The deployed rule is Terraform's; the one literal prefix it expires is
		// the prefix every upload, reporter's or staff's, is minted under.
		var storage = File.ReadAllText(Path.Combine(RepositoryRoot(), "infra", "storage.tf"));
		var rule = QuarantineRule().Match(storage);

		rule.Success.ShouldBeTrue("infra/storage.tf has no expire-quarantine rule filtered by a prefix.");
		$"quarantine/{_uploadId}".ShouldStartWith(rule.Groups["prefix"].Value);
	}

	[Then(@"the reviewer media link and the public media link both refuse the private attachment's key")]
	public async Task ThenTheReviewerAndPublicLinksRefuse()
	{
		var key = BlobKey.Parse((await Stored()).BlobKey);
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var reviewer = scope.ServiceProvider.GetRequiredService<ReviewerMediaLink>();
		var visitor = scope.ServiceProvider.GetRequiredService<PublicMediaLink>();

		await Should.ThrowAsync<DomainRuleViolationException>(() => reviewer.CreateViewUrl(key, "a.zip", BlobUrlLifetime.Maximum, CancellationToken.None));
		await Should.ThrowAsync<DomainRuleViolationException>(() => reviewer.CreateDocumentDownloadUrl(key, AttachmentKind.Document, "a.zip", BlobUrlLifetime.Maximum, CancellationToken.None));
		await Should.ThrowAsync<DomainRuleViolationException>(() => visitor.CreateUrl(key, "application/zip", BlobUrlLifetime.Maximum, CancellationToken.None));
		await Should.ThrowAsync<DomainRuleViolationException>(() => visitor.CreateDocumentDownloadUrl(TinyId.Parse(_attachmentId), key, MediaType.Pdf, BlobUrlLifetime.Maximum, CancellationToken.None));
	}

	[Then(@"the private attachment link refuses every key outside the private compartment")]
	public async Task ThenThePrivateLinkRefusesOtherKeys()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var links = scope.ServiceProvider.GetRequiredService<PrivateAttachmentLink>();

		foreach (var key in new[]
				 {
					 BlobKey.For(_reportId, MediaCompartment.Original, _attachmentId),
					 BlobKey.For(_reportId, MediaCompartment.Stripped, _attachmentId),
					 BlobKey.ForUpload(UploadId.New()),
				 })
		{
			await Should.ThrowAsync<DomainRuleViolationException>(() => links.CreateDownloadUrl(key, "a.zip", BlobUrlLifetime.Maximum, CancellationToken.None));
		}
	}

	[Then(@"the reviewer attachment endpoints answer 404 for the private attachment's id")]
	public async Task ThenTheReviewerAttachmentEndpointsAnswer404()
	{
		using var officer = Reviewer(_officer, "safety_officer");
		var root = $"/api/admin/reports/{_reportId}/attachments/{_attachmentId}";

		using var view = await officer.GetAsync(new Uri($"{root}/view", UriKind.Relative));
		using var download = await officer.GetAsync(new Uri($"{root}/download", UriKind.Relative));
		using var hide = await officer.PostAsync(new Uri($"{root}/hide", UriKind.Relative), null);

		new[] { view.StatusCode, download.StatusCode, hide.StatusCode }.ShouldAllBe(status => status == HttpStatusCode.NotFound);
	}

	[Then(@"^the API answers (401|403|with success) to every one of those private-attachment requests$")]
	public void ThenEveryRequestAnswers(string outcome)
	{
		HttpStatusCode[] expected = outcome switch
		{
			"401" => [.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 5)],
			"403" => [.. Enumerable.Repeat(HttpStatusCode.Forbidden, 5)],
			_ => [HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NoContent],
		};

		_answers.ShouldBe(expected);
	}

	[Then(@"both private attachments are listed, newest first")]
	public async Task ThenBothAreListedNewestFirst()
	{
		var ids = (await Listed()).Select(attachment => attachment.GetProperty("id").GetString()!).ToList();
		ids.ShouldBe(_added.Select(added => added.Id).Reverse().ToList());
	}

	[Then(@"each lists its file name, size, description, adder's token subject, and when it was added")]
	public async Task ThenEachListsItsDetails()
	{
		foreach (var listed in await Listed())
		{
			var added = _added.Single(candidate => candidate.Id == listed.GetProperty("id").GetString());
			listed.GetProperty("fileName").GetString().ShouldBe(added.FileName);
			listed.GetProperty("byteSize").GetInt64().ShouldBe(added.ByteSize);
			(listed.GetProperty("description").ValueKind == JsonValueKind.Null ? null : listed.GetProperty("description").GetString()).ShouldBe(added.Description);
			listed.GetProperty("addedBy").GetString().ShouldBe(added.AddedBy);
			listed.GetProperty("addedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
		}
	}

	[Then(@"adding them queued no work for the Worker")]
	public async Task ThenNoWorkQueued()
	{
		(await OutboxCount()).ShouldBe(_outboxBefore);
	}

	[Then(@"the report's detail view lists neither among its attachments")]
	public async Task ThenTheDetailViewListsNeither()
	{
		using var officer = Reviewer(_officer, "safety_officer");
		using var detail = await officer.GetAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		detail.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await detail.Content.ReadAsStringAsync();

		foreach (var added in _added)
		{
			body.ShouldNotContain(added.Id);
			body.ShouldNotContain(added.FileName);
		}

		using var document = JsonDocument.Parse(body);
		document.RootElement.GetProperty("attachments").GetArrayLength().ShouldBe(0);
	}

	[Then(@"the private attachment is no longer listed, and downloading or removing it answers 404")]
	public async Task ThenNoLongerListed()
	{
		(await Listed()).Select(attachment => attachment.GetProperty("id").GetString()).ShouldNotContain(_attachmentId);

		using var officer = Reviewer(_officer, "safety_officer");
		using var downloaded = await officer.GetAsync(DownloadUri());
		using var removed = await officer.DeleteAsync(AttachmentUri());
		new[] { downloaded.StatusCode, removed.StatusCode }.ShouldAllBe(status => status == HttpStatusCode.NotFound);
	}

	[Then(@"its row is stamped deleted with the administrator's token subject, and its bytes are still stored")]
	public async Task ThenStampedDeletedAndKept()
	{
		var stored = await Stored();
		stored.Deleted.ShouldNotBeNull();
		stored.DeletedBySubject.ShouldBe(_administrator);
		(await StoredLength(stored.BlobKey)).ShouldBe(stored.ByteSize);
	}

	[Then(@"one audit entry records the administrator's token subject, RemovedPrivateAttachment, the attachment, and the time")]
	public async Task ThenTheRemovalIsAudited()
	{
		var stored = await Stored();
		var entries = await AuditEntries(AuditAction.RemovedPrivateAttachment);

		entries.Count.ShouldBe(1);
		entries[0].ActorSubject.ShouldBe(_administrator);
		entries[0].TargetType.ShouldBe("PrivateAttachment");
		entries[0].OccurredAt.ShouldBe(stored.Deleted!.Value);
		(entries[0].Detail ?? string.Empty).ShouldNotContain("Synthetic");
	}

	[Then(@"the API answers 400 and no private attachment is stored")]
	public async Task ThenRefusedAndNothingStored()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await StoredCount()).ShouldBe(0);

		var listed = await BootedApi.Storage.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BootedApi.BucketName, Prefix = $"{_reportId}/private/" });
		(listed.S3Objects ?? []).ShouldBeEmpty();
	}

	[Then(@"the private attachment is stamped deleted at the report's deletion time, and its bytes are still stored")]
	public async Task ThenTheAttachmentWentWithTheReport()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		var report = await database.Reports.IgnoreQueryFilters().AsNoTracking().SingleAsync(candidate => candidate.Id == reportId);
		var stored = await Stored();

		report.Deleted.ShouldNotBeNull();
		stored.Deleted.ShouldBe(report.Deleted);
		stored.DeletedBySubject.ShouldBe(_officer);
		(await StoredLength(stored.BlobKey)).ShouldBe(stored.ByteSize);
	}

	[Then(@"minting, adding, listing, or downloading private attachments on that report answers 404")]
	public async Task ThenEveryRouteIsNotFound()
	{
		using var officer = Reviewer(_officer, "safety_officer");
		using var minted = await officer.PostAsJsonAsync(UploadsUri(), new { contentType = "application/zip", byteSize = 10 });
		using var added = await officer.PostAsJsonAsync(AttachmentsUri(), new { uploadId = UploadId.New().Value, fileName = "Synthetic.zip" });
		using var listed = await officer.GetAsync(AttachmentsUri());
		using var downloaded = await officer.GetAsync(DownloadUri());

		new[] { minted.StatusCode, added.StatusCode, listed.StatusCode, downloaded.StatusCode }
			.ShouldAllBe(status => status == HttpStatusCode.NotFound);
	}

	[Then(@"no response carries the private attachment's name, description, or identifier, or any count of private attachments")]
	public void ThenNoPublicResponseCarriesIt()
	{
		// Feed and page for each reader, and the one public image's link for each.
		_publicBodies.Count.ShouldBe(6);

		foreach (var body in _publicBodies)
		{
			body.ShouldNotContain(_attachmentId);
			body.ShouldNotContain(PrivateFileName);
			body.ShouldNotContain(Description);
			using var document = JsonDocument.Parse(body);
			PropertyNames(document.RootElement)
				.ShouldNotContain(name => name.Contains("private", StringComparison.OrdinalIgnoreCase));
		}

		// The report itself is public, so the reads above saw it.
		_publicBodies.ShouldContain(body => body.Contains(_reportId, StringComparison.Ordinal));
	}

	[Then(@"asking for the private attachment's identifier as public media answers 404")]
	public async Task ThenThePublicMediaLinkIsNotFound()
	{
		using var anonymous = (await BootedApi.Factory()).CreateClient();
		using var link = await anonymous.GetAsync(new Uri($"/api/v1/public/reports/{_reportId}/media/{_attachmentId}", UriKind.Relative));
		link.StatusCode.ShouldBe(HttpStatusCode.NotFound);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.PublicReports.AnyAsync(report => report.Id == _reportId)).ShouldBeTrue();
		(await database.PublicReportMedia.CountAsync(media => media.ReportId == _reportId)).ShouldBe(1);
		(await database.PublicReportMedia.AnyAsync(media => media.Id == _attachmentId)).ShouldBeFalse();
	}

	[Then(@"no database view reads the private-attachment table")]
	public async Task ThenNoViewReadsTheTable()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var views = await database.Database
			.SqlQueryRaw<string>("SELECT viewname AS \"Value\" FROM pg_views WHERE schemaname = 'public'")
			.ToListAsync();
		var readers = await database.Database
			.SqlQueryRaw<string>("SELECT viewname AS \"Value\" FROM pg_views WHERE schemaname = 'public' AND definition ILIKE '%private_attachment%'")
			.ToListAsync();

		views.ShouldContain("public_reports");
		views.ShouldContain("public_report_media");
		readers.ShouldBeEmpty();
	}

	[Then(@"the private note lists the private attachment it refers to, by identifier and file name")]
	public async Task ThenTheNoteListsItsAttachment()
	{
		var note = (await ListedNotes()).Single(candidate => candidate.GetProperty("id").GetString() == _noteId);
		var attachment = note.GetProperty("attachment");
		attachment.GetProperty("id").GetString().ShouldBe(_attachmentId);
		attachment.GetProperty("fileName").GetString().ShouldBe(PrivateFileName);
		attachment.GetProperty("removed").GetBoolean().ShouldBeFalse();
	}

	[Then(@"the private note refers to none, and its history shows the first revision still referring to it")]
	public async Task ThenTheNoteRefersToNone()
	{
		var note = (await ListedNotes()).Single(candidate => candidate.GetProperty("id").GetString() == _noteId);
		note.GetProperty("attachment").ValueKind.ShouldBe(JsonValueKind.Null);

		using var officer = Reviewer(_officer, "safety_officer");
		var history = (await officer.GetFromJsonAsync<JsonElement>(new Uri($"{NotesUri()}/{_noteId}/revisions", UriKind.Relative))).EnumerateArray().ToList();
		history.Count.ShouldBe(2);
		history[0].GetProperty("attachment").GetProperty("id").GetString().ShouldBe(_attachmentId);
		history[1].GetProperty("attachment").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"a private note referring to the other report's private attachment is refused with 400 and nothing is stored")]
	public async Task ThenAnotherReportsAttachmentIsRefused()
	{
		var before = (await ListedNotes()).Count;

		using var officer = Reviewer(_officer, "safety_officer");
		using var added = await officer.PostAsJsonAsync(NotesUri(), new { text = "Synthetic: wrong report.", attachmentId = _otherAttachmentId });
		added.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		using var edited = await officer.PutAsJsonAsync(
			new Uri($"{NotesUri()}/{_noteId}", UriKind.Relative),
			new { text = "Synthetic: wrong report.", revision = 2, attachmentId = _otherAttachmentId });
		edited.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		(await ListedNotes()).Count.ShouldBe(before);
		_otherReportId.ShouldNotBe(_reportId);
	}

	[Then(@"a private note referring to a removed private attachment is refused with 400")]
	public async Task ThenARemovedAttachmentIsRefused()
	{
		using var admin = Reviewer(_administrator, "administrator");
		using (var removed = await admin.DeleteAsync(AttachmentUri()))
		{
			removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		}

		using var added = await admin.PostAsJsonAsync(NotesUri(), new { text = "Synthetic: too late.", attachmentId = _attachmentId });
		added.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		// The first revision still names it, now marked removed.
		using var officer = Reviewer(_officer, "safety_officer");
		var history = (await officer.GetFromJsonAsync<JsonElement>(new Uri($"{NotesUri()}/{_noteId}/revisions", UriKind.Relative))).EnumerateArray().ToList();
		history[0].GetProperty("attachment").GetProperty("removed").GetBoolean().ShouldBeTrue();
	}

	// --- REQ-MED-052: nothing anonymizes a private attachment ---

	private byte[] _downloaded = [];

	[Given(@"a safety officer adds a JPEG photo carrying its camera's location metadata as a private attachment")]
	public async Task GivenAPhotoWithLocationMetadata()
	{
		_reportId = await BootedReports.Seed(ReportStatus.Pending, true);

		// A JPEG's EXIF segment carrying a synthetic GPS position, then random
		// scan data. Nothing reads it; the point is that nothing removes it.
		using var photo = new MemoryStream();
		photo.Write([0xFF, 0xD8, 0xFF, 0xE1]);
		var exif = System.Text.Encoding.ASCII.GetBytes("Exif\0\0GPSLatitude=49.2827 GPSLongitude=-123.1207 synthetic");
		photo.Write([(byte)((exif.Length + 2) >> 8), (byte)((exif.Length + 2) & 0xFF)]);
		photo.Write(exif);
		photo.Write(RandomNumberGenerator.GetBytes(4096));
		photo.Write([0xFF, 0xD9]);
		_bytes = photo.ToArray();

		_outboxBefore = await OutboxCount();
		var uploadId = await Send(_officer, "safety_officer", _bytes, "image/jpeg");
		using var officer = Reviewer(_officer, "safety_officer");
		using var added = await officer.PostAsJsonAsync(AttachmentsUri(), new { uploadId, fileName = "Synthetic site photo.jpg" });
		added.StatusCode.ShouldBe(HttpStatusCode.Created);
		_attachmentId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
	}

	[When(@"the safety officer downloads it")]
	public async Task WhenTheOfficerDownloadsIt()
	{
		using var officer = Reviewer(_officer, "safety_officer");
		var link = await officer.GetFromJsonAsync<JsonElement>(DownloadUri());
		using var storage = new HttpClient();
		_downloaded = await storage.GetByteArrayAsync(new Uri(link.GetProperty("url").GetString()!));
	}

	[Then(@"the stored bytes and the downloaded bytes are identical to those uploaded, location metadata included")]
	public async Task ThenTheBytesAreUntouched()
	{
		(await Read($"{_reportId}/private/{_attachmentId}")).ShouldBe(_bytes);
		_downloaded.ShouldBe(_bytes);
		System.Text.Encoding.ASCII.GetString(_downloaded).ShouldContain("GPSLatitude=49.2827");
		(await Stored()).ContentType.ShouldBe("image/jpeg");
	}

	[Then(@"no derivative of it exists and no outbox message asks for one")]
	public async Task ThenNoDerivativeExists()
	{
		var listed = await BootedApi.Storage.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BootedApi.BucketName, Prefix = $"{_reportId}/" });
		(listed.S3Objects ?? []).Select(stored => stored.Key).ShouldBe([$"{_reportId}/private/{_attachmentId}"]);
		(await OutboxCount()).ShouldBe(_outboxBefore);
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	[GeneratedRegex(@"id\s*=\s*""expire-quarantine""[\s\S]*?filter\s*\{\s*prefix\s*=\s*""(?<prefix>[^""]+)""", RegexOptions.None, 1000)]
	private static partial Regex QuarantineRule();

	private static long Size(string size)
	{
		return size switch
		{
			"zero bytes" => 0,
			"10 KB" => 10 * 1024,
			"1 MB" => Megabyte,
			"1 MB plus one byte" => Megabyte + 1,
			"300 MB" => 300 * Megabyte,
			"1 GB" => Gigabyte,
			"1 GB plus one byte" => Gigabyte + 1,
			_ => throw new ArgumentOutOfRangeException(nameof(size), size, "Not a size this step knows."),
		};
	}

	private static byte[] Synthetic(int length,
									bool zip = false)
	{
		var bytes = RandomNumberGenerator.GetBytes(length);

		if (zip)
		{
			// A zip's local-file-header signature; nothing reads past it.
			bytes[0] = 0x50;
			bytes[1] = 0x4B;
			bytes[2] = 0x03;
			bytes[3] = 0x04;
		}

		return bytes;
	}

	private HttpClient Reviewer(string subject,
								string role)
	{
		return BootedApi.SignedInAsMember(_host ?? BootedApi.Factory().GetAwaiter().GetResult(), subject, role);
	}

	private async Task<HttpClient> ClientFor(string who)
	{
		return who switch
		{
			"an anonymous visitor" => (await BootedApi.Factory()).CreateClient(),
			"a User" => await BootedApi.SignedInAs(MemberRole.User),
			"a SafetyOfficer" => Reviewer(_officer, "safety_officer"),
			_ => Reviewer(_administrator, "administrator"),
		};
	}

	private async Task<(string UploadId, Uri UploadUrl, string ContentType)> MintOnly(string subject,
																					  string role,
																					  string contentType,
																					  long byteSize)
	{
		using var client = Reviewer(subject, role);
		using var minted = await client.PostAsJsonAsync(UploadsUri(), new { contentType, byteSize });
		minted.StatusCode.ShouldBe(HttpStatusCode.Created, await minted.Content.ReadAsStringAsync());
		var body = await minted.Content.ReadFromJsonAsync<JsonElement>();
		return (body.GetProperty("uploadId").GetString()!, new Uri(body.GetProperty("uploadUrl").GetString()!), body.GetProperty("contentType").GetString()!);
	}

	/// <summary>Mints a private upload and PUTs the bytes, as the report page does.</summary>
	private async Task<string> Send(string subject,
									string role,
									byte[] bytes,
									string contentType)
	{
		var (uploadId, url, signedType) = await MintOnly(subject, role, contentType, bytes.Length);
		using var put = await DirectUpload.Put(url, new ByteArrayContent(bytes), signedType);
		put.IsSuccessStatusCode.ShouldBeTrue();
		return uploadId;
	}

	private async Task<string> AddThroughTheApi(string subject,
												string role,
												string fileName,
												string? description)
	{
		_bytes = Synthetic(2048, zip: true);
		var uploadId = await Send(subject, role, _bytes, "application/zip");

		using var client = Reviewer(subject, role);
		using var added = await client.PostAsJsonAsync(AttachmentsUri(), new { uploadId, fileName, description });
		added.StatusCode.ShouldBe(HttpStatusCode.Created, await added.Content.ReadAsStringAsync());
		return (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
	}

	private async Task<List<JsonElement>> Listed()
	{
		using var officer = Reviewer(_officer, "safety_officer");
		var listed = await officer.GetFromJsonAsync<JsonElement>(AttachmentsUri());
		return [.. listed.EnumerateArray()];
	}

	private async Task<List<JsonElement>> ListedNotes()
	{
		using var officer = Reviewer(_officer, "safety_officer");
		var listed = await officer.GetFromJsonAsync<JsonElement>(NotesUri());
		return [.. listed.EnumerateArray()];
	}

	private Uri UploadsUri()
	{
		return new Uri($"/api/admin/reports/{_reportId}/private-attachments/uploads", UriKind.Relative);
	}

	private Uri AttachmentsUri()
	{
		return new Uri($"/api/admin/reports/{_reportId}/private-attachments", UriKind.Relative);
	}

	private Uri AttachmentUri()
	{
		return new Uri($"/api/admin/reports/{_reportId}/private-attachments/{_attachmentId}", UriKind.Relative);
	}

	private Uri DownloadUri()
	{
		return new Uri($"/api/admin/reports/{_reportId}/private-attachments/{_attachmentId}/download", UriKind.Relative);
	}

	private Uri NotesUri()
	{
		return new Uri($"/api/admin/reports/{_reportId}/private-notes", UriKind.Relative);
	}

	private async Task<int> StoredCount()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		return await database.PrivateAttachments.IgnoreQueryFilters().CountAsync(attachment => attachment.ReportId == reportId);
	}

	private async Task<PrivateAttachment> Stored()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_attachmentId);
		return await database.PrivateAttachments.IgnoreQueryFilters().AsNoTracking().SingleAsync(attachment => attachment.Id == id);
	}

	private async Task<List<AuditLogEntry>> AuditEntries(AuditAction action)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_attachmentId);
		return await database.AuditLog.Where(entry => entry.TargetId == id && entry.Action == action).ToListAsync();
	}

	/// <summary>
	///     Outbox messages naming this report or any of its private attachments.
	///     Scenarios run in parallel against one database, so nothing wider can be counted.
	/// </summary>
	private async Task<int> OutboxCount()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);
		var attachments = await database.PrivateAttachments.IgnoreQueryFilters()
			.Where(attachment => attachment.ReportId == reportId)
			.Select(attachment => attachment.Id)
			.ToListAsync();
		List<TinyId> named = [reportId, .. attachments];
		var payloads = named.ConvertAll(id => id.Value);

		return await database.OutboxMessages.IgnoreQueryFilters()
			.CountAsync(message => named.Contains(message.AggregateId) || payloads.Contains(message.Payload));
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

	private static async Task<byte[]> Read(string key)
	{
		using var stored = await BootedApi.Storage.GetObjectAsync(BootedApi.BucketName, key);
		using var buffer = new MemoryStream();
		await stored.ResponseStream.CopyToAsync(buffer);
		return buffer.ToArray();
	}

	private static string? Query(Uri url,
								 string name)
	{
		return url.Query.TrimStart('?').Split('&')
			.Select(pair => pair.Split('=', 2))
			.Where(pair => string.Equals(Uri.UnescapeDataString(pair[0]), name, StringComparison.Ordinal))
			.Select(pair => Uri.UnescapeDataString(pair[1]))
			.FirstOrDefault();
	}

	/// <summary>Every property name anywhere in a JSON document.</summary>
	private static IEnumerable<string> PropertyNames(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.Object => element.EnumerateObject()
				.SelectMany(property => PropertyNames(property.Value).Prepend(property.Name)),
			JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
			_ => [],
		};
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null
			   && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
	}

	/// <summary>The cap a deployment runs with when nothing configures one.</summary>
	private static class PrivateAttachmentPolicyDefaults
	{
		public const long MaxByteSize = Gigabyte;
	}
}
