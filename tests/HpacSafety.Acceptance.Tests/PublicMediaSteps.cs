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
///     A published report's photos and video, through the booted API and the real
///     S3-compatible store (REQ-MED-025 to REQ-MED-031, ADR-0117): which files the
///     report page lists, the anonymous link to each, and a reviewer hiding and
///     showing one. Every file is synthetic, and every derivative a link is minted
///     for is written to storage so the link is followed, not only parsed.
/// </summary>
[Binding]
[Scope(Feature = "Attachments")]
public sealed class PublicMediaSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string Feed = "/api/v1/public/reports";

	private static readonly byte[] SyntheticJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9];

	private readonly List<string> _fileIds = [];
	private string _reportId = null!;
	private string _imageId = null!;
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

	[Given(@"the report has a document")]
	public async Task GivenADocument()
	{
		await SeedOne(report => report.AddFile(TinyId.New(), $"{report.Id}/original/{TinyId.New()}", MediaType.Pdf.ContentType, 1024, "witness.pdf", DateTimeOffset.UtcNow));
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

	[When(@"a safety officer hides the image")]
	public async Task WhenASafetyOfficerHidesTheImage()
	{
		using var officer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await officer.PostAsync(AdminUri("hide"), null);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[When(@"the safety officer shows the image again")]
	public async Task WhenTheSafetyOfficerShowsTheImage()
	{
		using var officer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await officer.PostAsync(AdminUri("show"), null);
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
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

		media.ShouldAllBe(item => item.EnumerateObject().Select(property => property.Name).SequenceEqual(new[] { "id", "kind" }));
		media.Select(item => item.GetProperty("kind").GetString()).ShouldBe(["image", "video"]);
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
							Action<Report> arrange)
	{
		_reportId = await BootedReports.Seed(ReportStatus.Published, "yes", arrange, mediaConsent: mediaConsent);

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var store = scope.ServiceProvider.GetRequiredService<IBlobStore>();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var files = await database.ReportFiles.Where(file => file.ReportId == TinyId.Parse(_reportId)).ToListAsync();

		foreach (var file in files.Where(file => file.StrippedBlobKey is not null))
		{
			using var bytes = new MemoryStream(SyntheticJpeg);
			await store.Write(BlobKey.Parse(file.StrippedBlobKey), bytes, file.Kind is AttachmentKind.Video ? file.ContentType : MediaType.Jpeg.ContentType, CancellationToken.None);
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
