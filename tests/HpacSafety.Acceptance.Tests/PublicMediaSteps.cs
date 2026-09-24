using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
///     A published report's photos, video, and documents, through the booted API
///     and the real S3-compatible store (REQ-MED-025 to REQ-MED-031, REQ-MED-037 to
///     REQ-MED-040, ADR-0117, ADR-0119): which files the report page lists, the
///     anonymous link to each, and a reviewer hiding and showing one. Every file is synthetic, and every derivative a link is minted
///     for is written to storage so the link is followed, not only parsed.
/// </summary>
[Binding]
[Scope(Feature = "Attachments")]
public sealed class PublicMediaSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string Feed = "/api/v1/public/reports";

	private static readonly byte[] SyntheticJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9];

	private static readonly byte[] SyntheticPdf = "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray();

	private readonly List<string> _fileIds = [];
	private string _reportId = null!;
	private string _imageId = null!;
	private string _documentId = null!;
	private HttpResponseMessage? _linkResponse;
	private HttpResponseMessage? _hideResponse;
	private JsonElement? _link;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"a published report whose reporter consented to publication and to sharing media")]
	public void GivenAPublishedReportWithMediaConsent()
	{
		// The report is seeded with its files by the next step, because a file
		// has to be on the report before it is saved.
	}

	[Given(@"the report has a processed image and a video with a verified derivative")]
	public async Task GivenAProcessedImageAndVideo()
	{
		await Seed("yes", report =>
		{
			var at = DateTimeOffset.UtcNow;
			_fileIds.Add(BootedReports.AddProcessedImage(report, at).Id.Value);
			_fileIds.Add(AddVideo(report, stripped: true, at.AddSeconds(1)).Id.Value);
		});
	}

	[Given(@"the report has a document the Worker has not validated yet")]
	public async Task GivenAnUnvalidatedDocument()
	{
		await SeedOne(report => AddDocument(report, validated: false, DateTimeOffset.UtcNow));
	}

	[Given(@"the report has a document whose validation failed")]
	public async Task GivenAFailedDocument()
	{
		await SeedOne(report =>
		{
			var file = AddDocument(report, validated: false, DateTimeOffset.UtcNow);
			file.RecordProcessingFailure("processing_failed");
			return file;
		});
	}

	[Given(@"a published report whose reporter consented to publication and to sharing media under wording that names documents")]
	public void GivenAPublishedReportWithDocumentConsent()
	{
		// Seeded with its files by the next step, as for photos and video.
	}

	[Given(@"the report has a validated PDF document and a processed image")]
	public async Task GivenAValidatedDocumentAndAnImage()
	{
		await Seed("yes", report =>
		{
			var at = DateTimeOffset.UtcNow;
			_fileIds.Add(_documentId = AddDocument(report, validated: true, at).Id.Value);
			_fileIds.Add(_imageId = BootedReports.AddProcessedImage(report, at.AddSeconds(1)).Id.Value);
		});
	}

	[Given(@"a published report with a processed image and a validated document")]
	public void GivenAnImageAndADocumentAwaitingConsent()
	{
		// Seeded by the next step, which says how the reporter answered.
	}

	[Given(@"^the reporter answered media consent (yes, to wording that named only photos and video|no|not at all)$")]
	public async Task GivenTheReporterAnsweredMediaConsent(string answer)
	{
		var consent = answer switch
		{
			"no" => "no",
			"not at all" => null,
			_ => "yes",
		};

		await Seed(consent, report =>
		{
			var at = DateTimeOffset.UtcNow;
			_imageId = BootedReports.AddProcessedImage(report, at).Id.Value;
			_documentId = AddDocument(report, validated: true, at.AddSeconds(1)).Id.Value;
		}, mediaConsentToEarlierWording: answer.StartsWith("yes", StringComparison.Ordinal));
	}

	[Given(@"a published report offers a validated PDF document")]
	[Given(@"a published report offers a validated document")]
	public async Task GivenAPublishedReportOffersADocument()
	{
		await Seed("yes", report => _documentId = AddDocument(report, validated: true, DateTimeOffset.UtcNow).Id.Value);
		(await Listed()).ShouldBe([_documentId]);
	}

	[Given(@"the report has a video retained without a derivative")]
	public async Task GivenAnUnstrippedVideo()
	{
		await SeedOne(report => AddVideo(report, stripped: false, DateTimeOffset.UtcNow));
	}

	[Given(@"the report has an image whose processing failed")]
	public async Task GivenAFailedImage()
	{
		await SeedOne(report =>
		{
			var file = Unprocessed(report);
			file.RecordProcessingFailure("processing_failed");
			return file;
		});
	}

	[Given(@"the report has an image the Worker has not processed yet")]
	public async Task GivenAnUnprocessedImage()
	{
		await SeedOne(Unprocessed);
	}

	[Given(@"a published report with a processed image whose media consent is {word}")]
	public async Task GivenAPublishedReportWithMediaConsent(string consent)
	{
		await Seed(consent == "unanswered" ? null : consent, report => _imageId = BootedReports.AddProcessedImage(report).Id.Value);
	}

	[Given(@"a published report shows a processed image")]
	public async Task GivenAPublishedReportShowsAnImage()
	{
		await GivenAPublishedReportWithMediaConsent("yes");
		(await Listed()).ShouldBe([_imageId]);
	}

	[Given(@"a member who is not a reviewer is signed in")]
	public void GivenAMemberWhoIsNotAReviewer()
	{
		// The member signs in in the When step below.
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"the public API returns the report")]
	public void WhenThePublicApiReturnsTheReport()
	{
		// Read in each Then step, so every assertion sees the current state.
	}

	[When(@"a visitor asks for that file's public link")]
	[When(@"an anonymous visitor asks for the image's public link")]
	public async Task WhenAVisitorAsksForTheLink()
	{
		using var client = await Anonymous();
		_linkResponse = await client.GetAsync(LinkUri(_imageId));
	}

	[When(@"an anonymous visitor asks for the document's public link")]
	public async Task WhenAVisitorAsksForTheDocumentLink()
	{
		using var client = await Anonymous();
		_linkResponse = await client.GetAsync(LinkUri(_documentId));
	}

	[When(@"a safety officer hides the image")]
	public async Task WhenASafetyOfficerHidesTheImage()
	{
		await ChangeVisibility(_imageId, "hide");
	}

	[When(@"the safety officer shows the image again")]
	public async Task WhenTheSafetyOfficerShowsTheImage()
	{
		await ChangeVisibility(_imageId, "show");
	}

	[When(@"a safety officer hides the document")]
	public async Task WhenASafetyOfficerHidesTheDocument()
	{
		await ChangeVisibility(_documentId, "hide");
	}

	[When(@"the safety officer shows the document again")]
	public async Task WhenTheSafetyOfficerShowsTheDocument()
	{
		await ChangeVisibility(_documentId, "show");
	}

	[When(@"a reviewer unpublishes the report")]
	public async Task WhenAReviewerUnpublishesTheReport()
	{
		await Change(report => report.Unpublish());
	}

	[When(@"an administrator deletes the report")]
	public async Task WhenAnAdministratorDeletesTheReport()
	{
		await Change(report => report.SoftDelete(DateTimeOffset.UtcNow));
	}

	[When(@"the member tries to hide the image")]
	public async Task WhenTheMemberTriesToHideTheImage()
	{
		using var member = await BootedApi.SignedInAs(MemberRole.User);
		_hideResponse = await member.PostAsync(AdminUri("hide"), null);
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"the report lists both files in the order they were attached")]
	public async Task ThenBothFilesAreListedInOrder()
	{
		(await Listed()).ShouldBe(_fileIds);
	}

	[Then(@"each file carries only its opaque id and whether it is an image or a video")]
	public async Task ThenEachFileCarriesOnlyIdAndKind()
	{
		var media = (await Detail()).GetProperty("media").EnumerateArray().ToList();

		// A format is a document's alone (ADR-0119); an image or video has none.
		media.ShouldAllBe(item => item.EnumerateObject().Select(property => property.Name).SequenceEqual(new[] { "id", "kind", "format" }));
		media.ShouldAllBe(item => item.GetProperty("format").ValueKind == JsonValueKind.Null);
		media.Select(item => item.GetProperty("kind").GetString()).ShouldBe(["image", "video"]);
	}

	[Then(@"the document carries only its opaque id, the kind document, and the format pdf")]
	public async Task ThenTheDocumentCarriesOnlyIdKindAndFormat()
	{
		var document = (await Detail()).GetProperty("media").EnumerateArray()
			.Single(item => item.GetProperty("id").GetString() == _documentId);

		document.EnumerateObject().Select(property => property.Name).ShouldBe(["id", "kind", "format"]);
		document.GetProperty("kind").GetString().ShouldBe("document");
		document.GetProperty("format").GetString().ShouldBe("pdf");
		document.GetRawText().ShouldNotContain("witness");
	}

	[Then(@"the report lists only the image")]
	public async Task ThenTheReportListsOnlyTheImage()
	{
		(await Listed()).ShouldBe([_imageId]);
	}

	[Then(@"the report lists the document")]
	public async Task ThenTheReportListsTheDocument()
	{
		(await Listed()).ShouldBe([_documentId]);
	}

	[Then(@"a visitor asking for the document's public link gets 404")]
	public async Task ThenTheDocumentLinkIs404()
	{
		using var client = await Anonymous();
		using var response = await client.GetAsync(LinkUri(_documentId));
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the visitor receives a pre-signed URL to the document's unchanged original that expires within fifteen minutes")]
	public async Task ThenTheVisitorReceivesTheDocumentOriginal()
	{
		var body = await Link();
		var url = new Uri(body.GetProperty("url").GetString()!);

		url.AbsolutePath.ShouldContain($"/{_reportId}/original/{_documentId}");
		int.Parse(QueryValue(url, "X-Amz-Expires"), System.Globalization.CultureInfo.InvariantCulture)
			.ShouldBeLessThanOrEqualTo((int)BlobUrlLifetime.Maximum.TotalSeconds + 1);
		body.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.Add(BlobUrlLifetime.Maximum));

		using var storage = new HttpClient();
		using var served = await storage.GetAsync(url);
		served.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await served.Content.ReadAsByteArrayAsync()).ShouldBe(SyntheticPdf);
	}

	[Then(@"the URL forces a download under a name made from the file id and the format, never the reporter's file name")]
	public async Task ThenTheDocumentDownloadsUnderAServerMintedName()
	{
		var body = await Link();
		using var storage = new HttpClient();
		using var served = await storage.GetAsync(new Uri(body.GetProperty("url").GetString()!));

		var disposition = served.Content.Headers.ContentDisposition!;
		disposition.DispositionType.ShouldBe("attachment");
		(disposition.FileNameStar ?? disposition.FileName!.Trim('"')).ShouldBe($"{_documentId}.pdf");
		served.Content.Headers.ContentDisposition!.ToString().ShouldNotContain("witness");
	}

	[Then(@"the response names no reporter file name or size")]
	public async Task ThenTheResponseNamesNoFileNameOrSize()
	{
		var body = await Link();

		body.EnumerateObject().Select(property => property.Name).ShouldBe(["url", "expiresAt"]);
		body.GetRawText().ShouldNotContain("witness");
	}

	[Then(@"the audit log records who hid the document")]
	public async Task ThenTheDocumentHideIsAudited()
	{
		var database = await Database();
		var file = await database.ReportFiles.SingleAsync(candidate => candidate.Id == TinyId.Parse(_documentId));
		var entry = await database.AuditLog.SingleAsync(candidate =>
			candidate.Action == AuditAction.HidMedia && candidate.TargetId == TinyId.Parse(_documentId));

		entry.TargetType.ShouldBe("ReportFile");
		entry.ActorSubject.ShouldBe(file.HiddenBySubject);
	}

	[Then(@"the API returns 404")]
	[Then(@"a visitor asking for the image's public link gets 404")]
	public async Task ThenTheLinkIs404()
	{
		using var client = await Anonymous();
		using var response = await client.GetAsync(LinkUri(_imageId));
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the report lists no media")]
	public async Task ThenTheReportListsNoMedia()
	{
		(await Listed()).ShouldBeEmpty();
	}

	[Then(@"the report lists the image")]
	[Then(@"the report still lists the image")]
	public async Task ThenTheReportListsTheImage()
	{
		(await Listed()).ShouldBe([_imageId]);
	}

	[Then(@"the visitor receives a pre-signed URL to the image's derivative that expires within fifteen minutes")]
	public async Task ThenTheVisitorReceivesAShortLivedUrl()
	{
		var body = await Link();
		var url = new Uri(body.GetProperty("url").GetString()!);

		url.AbsolutePath.ShouldContain($"/{_reportId}/stripped/{_imageId}");
		url.Query.ShouldContain("X-Amz-Signature");

		// The SDK rounds the remaining lifetime up to a whole second.
		int.Parse(QueryValue(url, "X-Amz-Expires"), System.Globalization.CultureInfo.InvariantCulture)
			.ShouldBeLessThanOrEqualTo((int)BlobUrlLifetime.Maximum.TotalSeconds + 1);
		body.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.Add(BlobUrlLifetime.Maximum));
	}

	[Then(@"the URL serves the derivative inline, under the derivative's own image content type")]
	public async Task ThenTheUrlServesTheDerivativeInline()
	{
		var body = await Link();
		using var storage = new HttpClient();
		using var served = await storage.GetAsync(new Uri(body.GetProperty("url").GetString()!));

		served.StatusCode.ShouldBe(HttpStatusCode.OK);
		served.Content.Headers.ContentType?.MediaType.ShouldBe(MediaType.Jpeg.ContentType);
		served.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
		(await served.Content.ReadAsByteArrayAsync()).ShouldBe(SyntheticJpeg);
	}

	[Then(@"the response carries the header X-Content-Type-Options: nosniff")]
	public void ThenTheResponseCarriesNosniff()
	{
		_linkResponse!.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
	}

	[Then(@"the response names no file name, size, or storage key")]
	public async Task ThenTheResponseNamesNothingPrivate()
	{
		var body = await Link();

		body.EnumerateObject().Select(property => property.Name).ShouldBe(["url", "expiresAt"]);
		body.GetRawText().ShouldNotContain("launch-site");
		body.GetRawText().ShouldNotContain("/original/");
	}

	[Then(@"the audit log records who hid the image")]
	public async Task ThenTheHideIsAudited()
	{
		var database = await Database();
		var file = await database.ReportFiles.SingleAsync(candidate => candidate.Id == TinyId.Parse(_imageId));
		var entry = await database.AuditLog.SingleAsync(candidate =>
			candidate.Action == AuditAction.HidMedia && candidate.TargetId == TinyId.Parse(_imageId));

		entry.TargetType.ShouldBe("ReportFile");
		entry.ActorSubject.ShouldBe(file.HiddenBySubject);
	}

	[Then(@"the audit log records who showed the image")]
	public async Task ThenTheShowIsAudited()
	{
		var database = await Database();
		var hid = await database.AuditLog.SingleAsync(candidate =>
			candidate.Action == AuditAction.HidMedia && candidate.TargetId == TinyId.Parse(_imageId));
		var showed = await database.AuditLog.SingleAsync(candidate =>
			candidate.Action == AuditAction.ShowedMedia && candidate.TargetId == TinyId.Parse(_imageId));

		// The same safety officer did both.
		showed.ActorSubject.ShouldBe(hid.ActorSubject);
		(await database.ReportFiles.SingleAsync(candidate => candidate.Id == TinyId.Parse(_imageId))).HiddenAt.ShouldBeNull();
	}

	[Then(@"the file is kept in storage throughout")]
	public async Task ThenTheFileIsKeptInStorage()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var store = scope.ServiceProvider.GetRequiredService<IBlobStore>();

		(await store.Describe(BlobKey.For(_reportId, MediaCompartment.Stripped, _imageId), CancellationToken.None)).ShouldNotBeNull();
	}

	[Then(@"the API answers 403")]
	public void ThenTheApiAnswers403()
	{
		_hideResponse!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private async Task Seed(string? mediaConsent,
							Action<Report> arrange,
							bool mediaConsentToEarlierWording = false)
	{
		_reportId = await BootedReports.Seed(ReportStatus.Published, "yes", arrange, mediaConsent: mediaConsent, mediaConsentToEarlierWording: mediaConsentToEarlierWording);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var store = scope.ServiceProvider.GetRequiredService<IBlobStore>();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var files = await database.ReportFiles.Where(file => file.ReportId == TinyId.Parse(_reportId)).ToListAsync();

		foreach (var file in files.Where(file => file.StrippedBlobKey is not null))
		{
			using var bytes = new MemoryStream(SyntheticJpeg);
			await store.Write(BlobKey.Parse(file.StrippedBlobKey), bytes, file.Kind is AttachmentKind.Video ? file.ContentType : MediaType.Jpeg.ContentType, CancellationToken.None);
		}

		// A document is offered as its unchanged original (ADR-0119).
		foreach (var file in files.Where(file => file.Kind is AttachmentKind.Document))
		{
			using var bytes = new MemoryStream(SyntheticPdf);
			await store.Write(BlobKey.Parse(file.BlobKey), bytes, file.ContentType, CancellationToken.None);
		}
	}

	private Task SeedOne(Func<Report, ReportFile> add)
	{
		return Seed("yes", report => _imageId = add(report).Id.Value);
	}

	private static ReportFile Unprocessed(Report report)
	{
		var fileId = TinyId.New();
		return report.AddFile(fileId, $"{report.Id}/original/{fileId}", MediaType.Jpeg.ContentType, 1024, "launch-site.jpg", DateTimeOffset.UtcNow);
	}

	private static ReportFile AddDocument(Report report,
										  bool validated,
										  DateTimeOffset at)
	{
		var fileId = TinyId.New();
		var file = report.AddFile(fileId, $"{report.Id}/original/{fileId}", MediaType.Pdf.ContentType, SyntheticPdf.Length, "witness-statement.pdf", at);

		if (validated)
		{
			file.RecordValidated(at);
		}

		return file;
	}

	private async Task ChangeVisibility(string fileId,
										string action)
	{
		using var officer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await officer.PostAsync(new Uri($"/api/admin/reports/{_reportId}/attachments/{fileId}/{action}", UriKind.Relative), null);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	private static ReportFile AddVideo(Report report,
									   bool stripped,
									   DateTimeOffset at)
	{
		var fileId = TinyId.New();
		var file = report.AddFile(fileId, $"{report.Id}/original/{fileId}", MediaType.Mp4.ContentType, 4096, "flight.mp4", at);

		if (stripped)
		{
			file.RecordStripped($"{report.Id}/stripped/{fileId}", at);
		}

		return file;
	}

	private async Task Change(Action<Report> change)
	{
		var database = await Database();
		var report = await database.Reports
			.Include(candidate => candidate.Answers)
			.Include(candidate => candidate.Files)
			.Include(candidate => candidate.Summary)
			.SingleAsync(candidate => candidate.Id == TinyId.Parse(_reportId));

		change(report);
		await database.SaveChangesAsync();
	}

	private async Task<JsonElement> Link()
	{
		_linkResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);
		_link ??= await _linkResponse.Content.ReadFromJsonAsync<JsonElement>();
		return _link.Value;
	}

	private async Task<JsonElement> Detail()
	{
		using var client = await Anonymous();
		using var response = await client.GetAsync(new Uri($"{Feed}/{_reportId}", UriKind.Relative));

		// An unpublished or deleted report is not public at all, so it lists nothing.
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return JsonDocument.Parse("""{"media":[]}""").RootElement;
		}

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<List<string>> Listed()
	{
		return [.. (await Detail()).GetProperty("media").EnumerateArray().Select(item => item.GetProperty("id").GetString()!)];
	}

	private Uri LinkUri(string fileId)
	{
		return new Uri($"{Feed}/{_reportId}/media/{fileId}", UriKind.Relative);
	}

	private Uri AdminUri(string action)
	{
		return new Uri($"/api/admin/reports/{_reportId}/attachments/{_imageId}/{action}", UriKind.Relative);
	}

	private static string QueryValue(Uri url,
									 string name)
	{
		return url.Query.TrimStart('?').Split('&')
			.Select(pair => pair.Split('=', 2))
			.Single(pair => pair[0] == name)[1];
	}

	private static async Task<HttpClient> Anonymous()
	{
		return (await BootedApi.Factory()).CreateClient();
	}

	private static async Task<HpacSafetyDbContext> Database()
	{
		var scope = (await BootedApi.Factory()).Services.CreateScope();
		return scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
	}
}
