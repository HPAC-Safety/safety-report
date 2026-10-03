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
///     A reporter's browser sees its own report before it is published, through the
///     booted API and the <c>own_reports</c> / <c>own_report_media</c> views
///     (REQ-MOD-212 to REQ-MOD-220, ADR-0196). The receipt is a credential, so every
///     request carries it in a body. Every report here is synthetic, and every
///     report is seeded straight through the domain's own transitions.
/// </summary>
/// <remarks>
///     The booted database is shared by every scenario, so absence from the public
///     feed is asserted by paging the feed for this scenario's own report ID, never
///     by an unchanged count.
/// </remarks>
[Binding]
[Scope(Feature = "Moderation, authentication, and publication")]
public sealed class OwnReportSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string Feed = "/api/v1/public/reports";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private string _reportId = string.Empty;
	private string _receipt = string.Empty;
	private string _imageId = string.Empty;
	private string _documentId = string.Empty;
	private string _hiddenImageId = string.Empty;
	private JsonElement? _lookup;
	private JsonElement? _detail;
	private (string Receipt, string Hash)? _johns;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"John has submitted a report that is not published")]
	[Given(@"a report that is not published")]
	public async Task GivenAReportThatIsNotPublished()
	{
		await Seed(ReportStatus.Pending, consent: true);
	}

	[Given(@"Cheryl, signed in or not, holds no receipt for it")]
	public void GivenCherylHoldsNoReceipt()
	{
		// She is the visitor the When step plays; she is given nothing.
	}

	[Given(@"John holds the receipt for a report whose summary the Worker has not produced yet")]
	public async Task GivenAReportWithNoSummaryYet()
	{
		// A report whose summarization has not produced a pair has no summary
		// whatever its status.
		await Seed(ReportStatus.SummaryFailed, consent: true);
	}

	[Given(@"John holds the receipt for a Pending report whose latest summary revision a reviewer edited and has not approved")]
	public async Task GivenAReportWithAnEditedUnapprovedRevision()
	{
		await Seed(ReportStatus.Pending, consent: true);
		await Change(report => report.EditSummary("A reviewer's edited English draft.", "Le brouillon français modifié par un réviseur.", "subject-reviewer", DateTimeOffset.UtcNow));
	}

	[Given(@"John holds the receipt for a report whose reporter did not consent to publication")]
	public async Task GivenAReportWithoutConsent()
	{
		await Seed(ReportStatus.Unpublished, consent: false);
	}

	[Given(@"John holds the receipt for a report a moderator has deleted")]
	public async Task GivenADeletedReport()
	{
		await Seed(ReportStatus.Pending, consent: true);
		await Change(report => report.SoftDelete(DateTimeOffset.UtcNow));
	}

	[Given(@"John holds the receipt for a report that has since been published")]
	public async Task GivenAPublishedReport()
	{
		await Seed(ReportStatus.Published, consent: true);
	}

	[Given(@"John holds the receipt for a report with a verified image, a hidden image, an unverified image, and a validated document")]
	[Given(@"John holds the receipt for a report that is not published and has a public image")]
	public void GivenAReportWithAttachments()
	{
		// Seeded with its files by the step that says how the reporter consented,
		// because a file has to be on the report before it is saved.
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"Cheryl lists the public feed and asks for that report's page")]
	public async Task WhenCherylListsTheFeed()
	{
		// Nothing to do: the Then steps ask, so the answers are read once each.
		await Task.CompletedTask;
	}

	[When(@"John's browser looks up its receipts")]
	public async Task WhenJohnLooksUpHisReceipts()
	{
		_lookup = await Lookup([(_reportId, _receipt)]);
	}

	[When(@"a lookup names it with a receipt that is not its own, a malformed receipt, or an unknown report")]
	public void WhenALookupNamesItWithAWrongReceipt()
	{
		// Each of the three is sent by the Then step, so the answers can be compared.
	}

	[When(@"the reporter consented to media and to documents")]
	public async Task WhenTheReporterConsentedToMediaAndDocuments()
	{
		await Seed(ReportStatus.Pending, consent: true, mediaConsent: true, arrange: report =>
		{
			var at = DateTimeOffset.UtcNow;
			_imageId = BootedReports.AddProcessedImage(report, at).Id.Value;

			var hidden = BootedReports.AddProcessedImage(report, at.AddSeconds(1));
			hidden.HideBy("subject-reviewer", at.AddSeconds(2));
			_hiddenImageId = hidden.Id.Value;

			var unverifiedId = TinyId.New();
			report.AddFile(unverifiedId, $"{report.Id}/original/{unverifiedId}", MediaType.Jpeg.ContentType, 1024, "unverified.jpg", at.AddSeconds(3));

			var documentId = TinyId.New();
			var document = report.AddFile(documentId, $"{report.Id}/original/{documentId}", MediaType.Pdf.ContentType, 64, "witness-statement.pdf", at.AddSeconds(4));
			document.RecordValidated(at.AddSeconds(5));
			_documentId = documentId.Value;
		});
	}

	[When(@"the reporter did not consent to media")]
	public async Task WhenTheReporterDidNotConsentToMedia()
	{
		await Seed(ReportStatus.Pending, consent: true, mediaConsent: false, arrange: report => BootedReports.AddProcessedImage(report, DateTimeOffset.UtcNow));
	}

	[When(@"John asks for that report's page and for the image's link with his receipt")]
	public async Task WhenJohnAsksForThePageAndLink()
	{
		await Seed(ReportStatus.Pending, consent: true, mediaConsent: true, arrange: report => _imageId = BootedReports.AddProcessedImage(report, DateTimeOffset.UtcNow).Id.Value);
		_detail = await Detail(_reportId, _receipt);
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"the feed does not list it and the page answers 404")]
	public async Task ThenTheFeedDoesNotListIt()
	{
		(await FeedIds()).ShouldNotContain(_reportId);

		using var client = await Anonymous();
		using var page = await client.GetAsync(new Uri($"{Feed}/{_reportId}", UriKind.Relative));
		page.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"asking with a receipt that is not John's shows nothing either")]
	public async Task ThenAnotherReceiptShowsNothing()
	{
		var other = BrowserReceipt.New().Receipt;
		var lookup = await Lookup([(_reportId, other)]);

		lookup.GetProperty("items").GetArrayLength().ShouldBe(0);
		lookup.GetProperty("settled").EnumerateArray().Select(item => item.GetString()).ShouldBe([_reportId]);

		using var response = await DetailResponse(_reportId, other);
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the report is listed with its submitted date and no summary")]
	public void ThenListedWithSubmittedDateAndNoSummary()
	{
		var item = Own();
		item.GetProperty("submittedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-10));
		item.GetProperty("aiSummaryEn").ValueKind.ShouldBe(JsonValueKind.Null);
		item.GetProperty("aiSummaryFr").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the entry carries only the report ID, its submitted date, whether it is for publication, its summary, and its attachment count, never an answer, a question, or any member data")]
	public void ThenTheEntryCarriesOnlyItsAllowlist()
	{
		Own().EnumerateObject().Select(property => property.Name)
			.ShouldBe(["id", "submittedAt", "forPublication", "aiSummaryEn", "aiSummaryFr", "attachmentCount"], ignoreOrder: true);
	}

	[Then(@"the entry carries that latest revision's English and French text")]
	public void ThenTheEntryCarriesTheLatestRevision()
	{
		var item = Own();
		item.GetProperty("aiSummaryEn").GetString().ShouldBe("A reviewer's edited English draft.");
		item.GetProperty("aiSummaryFr").GetString().ShouldBe("Le brouillon français modifié par un réviseur.");
	}

	[Then(@"the entry is marked not yet published")]
	public async Task ThenTheEntryIsNotYetPublished()
	{
		Own().GetProperty("forPublication").GetBoolean().ShouldBeTrue();
		(await FeedIds()).ShouldNotContain(_reportId);
	}

	[Then(@"the report is listed as not for publication, with no summary")]
	public void ThenListedAsNotForPublication()
	{
		var item = Own();
		item.GetProperty("forPublication").GetBoolean().ShouldBeFalse();
		item.GetProperty("aiSummaryEn").ValueKind.ShouldBe(JsonValueKind.Null);
		item.GetProperty("aiSummaryFr").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"it stays listed until a moderator deletes it")]
	public async Task ThenItStaysListedUntilDeleted()
	{
		(await Lookup([(_reportId, _receipt)])).GetProperty("items").GetArrayLength().ShouldBe(1);

		await Change(report => report.SoftDelete(DateTimeOffset.UtcNow));

		var after = await Lookup([(_reportId, _receipt)]);
		after.GetProperty("items").GetArrayLength().ShouldBe(0);
		after.GetProperty("settled").EnumerateArray().Select(item => item.GetString()).ShouldBe([_reportId]);
	}

	[Then(@"the report is not listed")]
	public void ThenTheReportIsNotListed()
	{
		_lookup!.Value.GetProperty("items").GetArrayLength().ShouldBe(0);
	}

	[Then(@"the report is not listed among his own")]
	public void ThenTheReportIsNotListedAmongHisOwn()
	{
		_lookup!.Value.GetProperty("items").GetArrayLength().ShouldBe(0);
	}

	[Then(@"the lookup tells the browser to drop that receipt")]
	public void ThenTheLookupSettlesTheReceipt()
	{
		_lookup!.Value.GetProperty("settled").EnumerateArray().Select(item => item.GetString()).ShouldBe([_reportId]);
	}

	[Then(@"the public feed lists the report for everyone")]
	public async Task ThenThePublicFeedListsTheReport()
	{
		(await FeedIds()).ShouldContain(_reportId);
	}

	[Then(@"each answer is the same: nothing is listed and the receipt is settled")]
	public async Task ThenEachAnswerIsTheSame()
	{
		var unknown = TinyId.New().Value;
		var attempts = new[]
		{
			(_reportId, BrowserReceipt.New().Receipt),
			(_reportId, "not-a-receipt"),
			(unknown, _receipt),
		};

		var shapes = new List<string>();

		foreach (var (id, receipt) in attempts)
		{
			var lookup = await Lookup([(id, receipt)]);

			lookup.GetProperty("items").GetArrayLength().ShouldBe(0);
			lookup.GetProperty("settled").EnumerateArray().Select(item => item.GetString()).ShouldBe([id]);
			shapes.Add(lookup.GetRawText().Replace(id, "ID", StringComparison.Ordinal));

			using var page = await DetailResponse(id, receipt);
			page.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		}

		shapes.Distinct().Count().ShouldBe(1);
	}

	[Then(@"no response says whether the report exists")]
	public void ThenNoResponseSaysWhetherTheReportExists()
	{
		// Asserted by the previous step: the three answers are one shape, bar the echoed ID.
	}

	[Then(@"the entry and page count and list only the verified image and the validated document")]
	public async Task ThenOnlyTheVerifiedImageAndDocumentAreListed()
	{
		var lookup = await Lookup([(_reportId, _receipt)]);
		lookup.GetProperty("items")[0].GetProperty("attachmentCount").GetInt32().ShouldBe(2);

		var detail = await Detail(_reportId, _receipt);
		detail.GetProperty("attachmentCount").GetInt32().ShouldBe(2);
		var media = detail.GetProperty("media").EnumerateArray().ToList();
		media.Select(item => item.GetProperty("id").GetString()).ShouldBe([_imageId, _documentId]);
		media.Select(item => item.GetProperty("kind").GetString()).ShouldBe(["image", "document"]);
		media.ShouldAllBe(item => item.EnumerateObject().Select(property => property.Name).SequenceEqual(new[] { "id", "kind", "format" }));
	}

	[Then(@"the image is offered only as its verified derivative and the document only as a forced download")]
	public async Task ThenTheImageAndDocumentAreOfferedAsPublicly()
	{
		var image = await Link(_imageId);
		var imageUrl = new Uri(image.GetProperty("url").GetString()!);
		imageUrl.AbsolutePath.ShouldContain($"/{_reportId}/stripped/{_imageId}");

		var document = await Link(_documentId);
		var documentUrl = new Uri(document.GetProperty("url").GetString()!);
		Uri.UnescapeDataString(documentUrl.Query).ShouldContain("attachment");

		// An original image and a hidden one are never served.
		using var original = await LinkResponse(_hiddenImageId);
		original.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"no attachment is counted or listed")]
	public async Task ThenNoAttachmentIsCountedOrListed()
	{
		var lookup = await Lookup([(_reportId, _receipt)]);
		lookup.GetProperty("items")[0].GetProperty("attachmentCount").GetInt32().ShouldBe(0);

		var detail = await Detail(_reportId, _receipt);
		detail.GetProperty("attachmentCount").GetInt32().ShouldBe(0);
		detail.GetProperty("media").GetArrayLength().ShouldBe(0);
	}

	[Then(@"the page carries its summary, submitted date, language, and image")]
	public void ThenThePageCarriesItsContents()
	{
		var detail = _detail!.Value;
		detail.GetProperty("aiSummaryEn").GetString().ShouldNotBeNullOrWhiteSpace();
		detail.GetProperty("submittedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-10));
		detail.GetProperty("language").GetString().ShouldBe("en-CA");
		detail.GetProperty("media").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ShouldBe([_imageId]);
	}

	[Then(@"the link is a pre-signed URL that lives at most 15 minutes")]
	public async Task ThenTheLinkLivesAtMost15Minutes()
	{
		var link = await Link(_imageId);
		var url = new Uri(link.GetProperty("url").GetString()!);

		url.Query.ShouldContain("X-Amz-Signature");
		var expires = url.Query.TrimStart('?').Split('&').Select(pair => pair.Split('=')).Single(pair => pair[0] == "X-Amz-Expires")[1];
		int.Parse(expires, System.Globalization.CultureInfo.InvariantCulture).ShouldBeLessThanOrEqualTo((int)BlobUrlLifetime.Maximum.TotalSeconds + 1);
		link.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.Add(BlobUrlLifetime.Maximum));
	}

	[Then(@"the same requests without his receipt answer 404")]
	public async Task ThenTheSameRequestsWithoutHisReceiptAnswer404()
	{
		foreach (var receipt in new[] { string.Empty, BrowserReceipt.New().Receipt })
		{
			using var page = await DetailResponse(_reportId, receipt);
			page.StatusCode.ShouldBe(HttpStatusCode.NotFound);

			using var link = await LinkResponse(_imageId, receipt);
			link.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		}
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private async Task Seed(ReportStatus status,
							bool consent,
							bool? mediaConsent = null,
							Action<Report>? arrange = null)
	{
		_johns = BrowserReceipt.New();
		_receipt = _johns.Value.Receipt;
		var hash = _johns.Value.Hash;

		_reportId = await BootedReports.Seed(
			status,
			consent,
			report =>
			{
				report.AttachReceipt(hash);
				arrange?.Invoke(report);
			},
			mediaConsent: mediaConsent);
	}

	private async Task Change(Action<Report> change)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = await database.Reports
			.Include(candidate => candidate.Answers)
			.Include(candidate => candidate.Files)
			.Include(candidate => candidate.Summary)
			.SingleAsync(candidate => candidate.Id == TinyId.Parse(_reportId));

		change(report);
		await database.SaveChangesAsync();
	}

	private static async Task<HttpClient> Anonymous()
	{
		return (await BootedApi.Factory()).CreateClient();
	}

	private async Task<JsonElement> Lookup(IEnumerable<(string ReportId, string Receipt)> receipts)
	{
		using var client = await Anonymous();
		using var response = await client.PostAsJsonAsync(
			new Uri($"{Feed}/own", UriKind.Relative),
			new { receipts = receipts.Select(entry => new { reportId = entry.ReportId, receipt = entry.Receipt }) },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<HttpResponseMessage> DetailResponse(string reportId,
														   string receipt)
	{
		using var client = await Anonymous();
		return await client.PostAsJsonAsync(new Uri($"{Feed}/own/{reportId}", UriKind.Relative), new { receipt }, JsonOptions);
	}

	private async Task<JsonElement> Detail(string reportId,
										   string receipt)
	{
		using var response = await DetailResponse(reportId, receipt);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<HttpResponseMessage> LinkResponse(string mediaId,
														 string? receipt = null)
	{
		using var client = await Anonymous();
		return await client.PostAsJsonAsync(
			new Uri($"{Feed}/own/{_reportId}/media/{mediaId}", UriKind.Relative), new { receipt = receipt ?? _receipt }, JsonOptions);
	}

	private async Task<JsonElement> Link(string mediaId)
	{
		using var response = await LinkResponse(mediaId);
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private JsonElement Own()
	{
		return _lookup!.Value.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetString() == _reportId);
	}

	private async Task<List<string>> FeedIds()
	{
		using var client = await Anonymous();
		var ids = new List<string>();
		string? after = null;

		do
		{
			var page = await client.GetFromJsonAsync<JsonElement>(
				new Uri(after is null ? Feed : $"{Feed}?after={Uri.EscapeDataString(after)}", UriKind.Relative));

			ids.AddRange(page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!));
			after = page.GetProperty("next").ValueKind == JsonValueKind.Null ? null : page.GetProperty("next").GetString();
		}
		while (after is not null);

		return ids;
	}
}
