using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HpacSafety.Api.PublicReports;
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
///     The anonymous public feed and detail through the booted API, read from the
///     <c>public_reports</c> view (#28): the allowlisted DTO (REQ-MOD-036),
///     the paginated feed (REQ-MOD-037), the indistinguishable 404
///     (REQ-MOD-038), and the publication invariant as that view states it
///     (REQ-DOM-003/004).
/// </summary>
/// <remarks>
///     The booted database is shared by every scenario, so the feed holds other
///     scenarios' reports too. Each assertion here is about the reports this
///     scenario seeded, and about ordering across whatever else is present.
///     Every violation in REQ-DOM-004 is written straight into the tables, so what
///     is being tested is the view's own predicate rather than a domain guard that
///     would have refused to produce the row. Every report is synthetic.
/// </remarks>
[Binding]
[Scope(Feature = "Public feed")]
[Scope(Feature = "Domain and lifecycle")]
public sealed class PublicReportFeedSteps(SeededReport seeded)
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string Feed = "/api/v1/public/reports";

	private static readonly string[] Allowlist =
		["id", "aiSummaryEn", "aiSummaryFr", "publishedAt", "commentCount", "attachmentCount", "language", "media", "staffAttachments"];

	private readonly List<string> _publishable = [];
	private readonly List<string> _hidden = [];
	private readonly List<HttpResponseMessage> _responses = [];
	private readonly List<JsonElement> _pages = [];
	private readonly List<(string Cursor, string LastItemId)> _cursors = [];
	private HttpResponseMessage? _response;
	private JsonElement? _body;
	private string? _mixedVisibilityReportId;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"^a report written in (French|English) has been published$")]
	public async Task GivenAReportWrittenInALanguageHasBeenPublished(string written)
	{
		seeded.Id = await BootedReports.Seed(
			ReportStatus.Published,
			true,
			language: written == "French" ? Locale.FrCa : Locale.EnCa);
	}

	[Given(@"a published report has one public attachment and one attachment only staff may see")]
	public async Task GivenAReportWithOnePublicAndOneStaffOnlyAttachment()
	{
		var now = DateTimeOffset.UtcNow;

		// Submitted far in the future, like the pagination scenario above, so
		// this scenario's report is always the feed's first (and only, per this
		// scenario) page item — the shared database holds other scenarios'
		// reports too.
		var at = now.AddYears(60);

		_mixedVisibilityReportId = await BootedReports.Seed(
			ReportStatus.Published,
			true,
			report =>
			{
				var pub = report.AddFile($"reports/{Guid.NewGuid():n}.jpg", "image/jpeg", 1024, now);
				pub.RecordStripped($"{report.Id}/stripped/{pub.Id}", now);
				// Still processing — no derivative, so only staff may see it.
				report.AddFile($"reports/{Guid.NewGuid():n}.jpg", "image/jpeg", 1024, now);
			},
			at: at,
			mediaConsent: true);
	}

	[Given(@"a published report has a public image and a hidden image")]
	public async Task GivenAReportWithAPublicAndAHiddenImage()
	{
		var now = DateTimeOffset.UtcNow;
		_mixedVisibilityReportId = await BootedReports.Seed(
			ReportStatus.Published,
			true,
			report =>
			{
				var pub = report.AddFile($"reports/{Guid.NewGuid():n}.jpg", "image/jpeg", 1024, now);
				pub.RecordStripped($"{report.Id}/stripped/{pub.Id}", now);
				var hidden = report.AddFile($"reports/{Guid.NewGuid():n}.jpg", "image/jpeg", 1024, now);
				hidden.RecordStripped($"{report.Id}/stripped/{hidden.Id}", now);
				hidden.HideBy("synthetic-reviewer", now);
			},
			mediaConsent: true);
	}

	[Given(@"some reports are publishable and others are not")]
	public async Task GivenSomeReportsArePublishableAndOthersAreNot()
	{
		// One more than a page, all published in the same instant, so the feed
		// has to page and has to break ties by ID to do it deterministically.
		var instant = DateTimeOffset.UtcNow.AddYears(50);

		for (var index = 0; index <= PublicReportEndpoints.PageSize; index++)
		{
			_publishable.Add(await BootedReports.Seed(ReportStatus.Published, true, at: instant));
		}

		_hidden.Add(await BootedReports.Seed(ReportStatus.Pending, true, at: instant));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Unpublished, true, at: instant));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Unpublished, false, at: instant));
		_hidden.Add(await Deleted(await BootedReports.Seed(ReportStatus.Published, true, at: instant)));
	}

	[Given(@"a report id is unknown, deleted, pending, unpublished, or not consented")]
	public async Task GivenNonPublicReportIds()
	{
		_hidden.Add(TinyId.New().Value);
		_hidden.Add(await Deleted(await BootedReports.Seed(ReportStatus.Published, true)));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Pending, true));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Unpublished, true));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Unpublished, false));
	}

	[Given(@"a report and its summary row are not deleted")]
	[Given(@"a report otherwise satisfies every publication invariant")]
	public async Task GivenAPublishableReport()
	{
		seeded.Id = await BootedReports.Seed(ReportStatus.Published, true);
	}

	[Given(@"ConsentPublish is exactly true")]
	[Given(@"both English and French summary texts are nonblank")]
	[Given(@"the pair has a current human approval")]
	[Given(@"the report is Published")]
	public void GivenTheRestOfTheInvariantHolds()
	{
		// Contextual — the report seeded above was published through the domain's
		// own transitions, which is every one of these.
	}

	[Given(@"the report or summary row is deleted")]
	public async Task GivenTheSummaryRowIsDeleted()
	{
		await Violate($"UPDATE summaries SET deleted = now() WHERE report_id = {seeded.Id}");
	}

	[Given(@"ConsentPublish is not exactly true")]
	public async Task GivenConsentIsNotExactlyTrue()
	{
		// A locked column: written the way a migration would (ADR-0178).
		(await PastTheImmutabilityTriggers.Write("reports", $"UPDATE reports SET consent_publish = NULL WHERE id = {seeded.Id}")).ShouldBe(1);
	}

	[Given(@"the English or French summary text is blank")]
	public async Task GivenASummaryTextIsBlank()
	{
		(await PastTheImmutabilityTriggers.Write(
			"summary_revisions",
			$"UPDATE summary_revisions SET ai_summary_fr = '  ' WHERE summary_id IN (SELECT id FROM summaries WHERE report_id = {seeded.Id})")).ShouldBe(1);
	}

	[Given(@"the pair has no current human approval")]
	public async Task GivenThePairHasNoApproval()
	{
		await Violate($"UPDATE summary_revisions SET approved_at = NULL, approved_by_subject = NULL WHERE summary_id IN (SELECT id FROM summaries WHERE report_id = {seeded.Id})");
	}

	[Given(@"the report is not Published")]
	public async Task GivenTheReportIsNotPublished()
	{
		await Violate($"UPDATE reports SET status = 'unpublished' WHERE id = {seeded.Id}");
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"the public API returns it")]
	[When(@"the public API returns the report")]
	[When(@"the public query evaluates the report")]
	public async Task WhenThePublicApiReturnsTheReport()
	{
		using var client = await Anonymous();
		_response = await client.GetAsync(new Uri($"{Feed}/{seeded.Id}", UriKind.Relative));
	}

	[When(@"the public feed is queried")]
	public async Task WhenThePublicFeedIsQueried()
	{
		using var client = await Anonymous();
		string? after = null;

		do
		{
			var uri = after is null ? Feed : $"{Feed}?after={Uri.EscapeDataString(after)}";
			var page = await client.GetFromJsonAsync<JsonElement>(new Uri(uri, UriKind.Relative));
			_pages.Add(page);
			after = page.GetProperty("next").GetString();

			if (after is not null)
			{
				var lastItemId = page.GetProperty("items").EnumerateArray().Last().GetProperty("id").GetString()!;
				_cursors.Add((after, lastItemId));
			}
		}
		while (after is not null && _pages.Count < 1000);
	}

	[When(@"an anonymous visitor lists the feed")]
	public async Task WhenAnAnonymousVisitorListsTheFeed()
	{
		using var client = await Anonymous();
		_response = await client.GetAsync(new Uri(Feed, UriKind.Relative));
	}

	[When(@"a signed-in safety officer lists the feed")]
	public async Task WhenASignedInSafetyOfficerListsTheFeed()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_response = await client.GetAsync(new Uri(Feed, UriKind.Relative));
	}

	[When(@"a signed-in member with the User role lists the feed")]
	public async Task WhenASignedInMemberWithTheUserRoleListsTheFeed()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.User);
		_response = await client.GetAsync(new Uri(Feed, UriKind.Relative));
	}

	[When(@"a signed-in Administrator lists the feed")]
	public async Task WhenASignedInAdministratorListsTheFeed()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.Administrator);
		_response = await client.GetAsync(new Uri(Feed, UriKind.Relative));
	}

	[When(@"a signed-in safety officer asks the public API for that report")]
	public async Task WhenASignedInSafetyOfficerAsksForThatReport()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_response = await client.GetAsync(new Uri($"{Feed}/{_mixedVisibilityReportId}", UriKind.Relative));
	}

	[Then(@"the report's attachment count is (\d+)")]
	public async Task ThenTheReportsAttachmentCountIs(int expected)
	{
		// Reads _response fresh rather than through the shared Body() cache: this
		// scenario issues two requests, and Body()'s cache is meant for scenarios
		// that issue only one.
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await _response.Content.ReadFromJsonAsync<JsonElement>();
		var item = body.GetProperty("items").EnumerateArray().Single(candidate => candidate.GetProperty("id").GetString() == _mixedVisibilityReportId);
		item.GetProperty("attachmentCount").GetInt32().ShouldBe(expected);
	}

	[Then(@"the response carries a staff attachment for each file, with its state and public visibility")]
	public async Task ThenTheResponseCarriesAStaffAttachmentForEachFile()
	{
		var body = await Body();
		var attachments = body.GetProperty("staffAttachments").EnumerateArray().ToList();
		attachments.Count.ShouldBe(2);
		attachments.ShouldAllBe(item => item.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
			.SetEquals(new[] { "id", "kind", "state", "visibility", "format" }));
	}

	[Then(@"the hidden file's visibility reads ""(.*)""")]
	public async Task ThenTheHiddenFilesVisibilityReads(string expected)
	{
		var body = await Body();
		body.GetProperty("staffAttachments").EnumerateArray()
			.Count(item => item.GetProperty("visibility").GetString() == expected)
			.ShouldBe(1);
	}

	[Then(@"the public file's visibility reads ""(.*)""")]
	public async Task ThenThePublicFilesVisibilityReads(string expected)
	{
		var body = await Body();
		body.GetProperty("staffAttachments").EnumerateArray()
			.Count(item => item.GetProperty("visibility").GetString() == expected)
			.ShouldBe(1);
	}

	[When(@"the public API is asked for that report")]
	public async Task WhenThePublicApiIsAskedForEachReport()
	{
		using var client = await Anonymous();

		foreach (var id in _hidden)
		{
			_responses.Add(await client.GetAsync(new Uri($"{Feed}/{id}", UriKind.Relative)));
		}
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"the response contains only the opaque report ID, ai_summary_en, ai_summary_fr, the publication timestamp, the number of visible comments, the viewer-scoped attachment count, the language the report was written in, each public file's opaque id, kind, and — for a document only — coarse format, and the staff attachment list, null for this anonymous viewer")]
	public async Task ThenTheResponseIsExactlyTheAllowlist()
	{
		var body = await Body();
		body.EnumerateObject().Select(property => property.Name).ShouldBe(Allowlist, ignoreOrder: true);
		body.GetProperty("id").GetString().ShouldBe(seeded.Id);
		body.GetProperty("attachmentCount").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
		body.GetProperty("staffAttachments").ValueKind.ShouldBe(JsonValueKind.Null);

		var media = body.GetProperty("media").EnumerateArray().ToList();
		media.ShouldNotBeEmpty();
		media.ShouldAllBe(item => item.EnumerateObject().Select(property => property.Name).SequenceEqual(new[] { "id", "kind", "format" }));
		media.Where(item => item.GetProperty("kind").GetString() != "document")
			.ShouldAllBe(item => item.GetProperty("format").ValueKind == JsonValueKind.Null);
	}

	[Then(@"it never contains question keys, labels, answers, consent values, private flags, raw reports, attachment names, sizes, content types, keys, or URLs, member or reviewer identities, model provenance, or audit records")]
	public async Task ThenItNeverContainsAnythingElse()
	{
		var raw = (await Body()).GetRawText();

		// The allowlist above already rules these out by shape; this rules out
		// their values turning up inside an allowed field.
		raw.ShouldNotContain(BootedReports.PilotName);
		raw.ShouldNotContain("pilot_");
		raw.ShouldNotContain("synthetic-approver");
		raw.ShouldNotContain("gemini");
		raw.ShouldNotContain("summarize-anonymize");
		raw.ShouldNotContain("image/");
		raw.ShouldNotContain("launch-site");
		raw.ShouldNotContain("stripped");
		raw.ShouldNotContain("original");
	}

	[Then(@"the response's language is ""(.*)""")]
	public async Task ThenTheResponsesLanguageIs(string code)
	{
		(await Body()).GetProperty("language").GetString().ShouldBe(code);
	}

	[Then(@"no feed item carries a language")]
	public void ThenNoFeedItemCarriesALanguage()
	{
		var items = _pages.SelectMany(page => page.GetProperty("items").EnumerateArray()).ToList();
		items.ShouldNotBeEmpty();
		items.Any(item => item.EnumerateObject().Any(property => property.Name == "language")).ShouldBeFalse();
	}

	[Then(@"the response is a deterministic paginated list containing only publishable reports")]
	public void ThenTheFeedIsPaginatedAndContainsThePublishableReports()
	{
		_pages.Count.ShouldBeGreaterThan(1);
		_pages.ShouldAllBe(page => page.GetProperty("items").GetArrayLength() <= PublicReportEndpoints.PageSize);
		Listed().Select(item => item.Id).ShouldBeUnique();
		_publishable.ShouldBeSubsetOf(Listed().Select(item => item.Id));
	}

	[Then(@"no non-publishable report ever appears")]
	public void ThenNoNonPublishableReportAppears()
	{
		Listed().Select(item => item.Id).Intersect(_hidden).ShouldBeEmpty();
	}

	[Then(@"the list is newest submitted first, a tie broken by report ID, and each page names the cursor that continues it")]
	public async Task ThenTheOrderIsTotalAndTheCursorContinuesIt()
	{
		var listed = Listed();
		var submittedAt = await SubmittedAtByReportId();
		var expected = listed
			.OrderByDescending(item => submittedAt[item.Id])
			.ThenByDescending(item => item.Id, StringComparer.Ordinal)
			.ToList();

		listed.ShouldBe(expected);
		_pages[^1].GetProperty("next").ValueKind.ShouldBe(JsonValueKind.Null);
		_pages[..^1].ShouldAllBe(page => page.GetProperty("next").ValueKind == JsonValueKind.String);
	}

	[Then(@"no feed entry names its submission time")]
	public void ThenNoFeedEntryNamesItsSubmissionTime()
	{
		foreach (var item in _pages.SelectMany(page => page.GetProperty("items").EnumerateArray()))
		{
			item.EnumerateObject().Select(property => property.Name).ShouldNotContain("submittedAt");
		}
	}

	[Then(@"no cursor reveals a submission time")]
	public void ThenNoCursorRevealsASubmissionTime()
	{
		// A cursor is base64url of exactly its page's last report ID — an opaque
		// value already public on that report's own page — never a timestamp or
		// anything derived from one (#570). Independent of the endpoint's own
		// Cursor type, so this proves the wire format, not just its code.
		_cursors.ShouldNotBeEmpty();

		foreach (var (cursor, lastItemId) in _cursors)
		{
			Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).ShouldBe(lastItemId);
		}
	}

	[Then(@"the API returns 404")]
	public void ThenEveryResponseIs404()
	{
		_responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.NotFound);
	}

	[Then(@"non-public ids are indistinguishable from unknown ids")]
	public async Task ThenNonPublicIdsAreIndistinguishableFromUnknownIds()
	{
		var unknown = _responses[0];
		var unknownBody = await unknown.Content.ReadAsStringAsync();

		foreach (var response in _responses.Skip(1))
		{
			(await response.Content.ReadAsStringAsync()).ShouldBe(unknownBody);
			response.Content.Headers.ContentType.ShouldBe(unknown.Content.Headers.ContentType);
			response.Content.Headers.ContentLength.ShouldBe(unknown.Content.Headers.ContentLength);
		}
	}

	[Then(@"the report is publishable")]
	public void ThenTheReportIsPublishable()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Then(@"the report is not publishable")]
	public void ThenTheReportIsNotPublishable()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private static async Task<HttpClient> Anonymous()
	{
		return (await BootedApi.Factory()).CreateClient();
	}

	private static async Task<string> Deleted(string reportId)
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(reportId);
		var report = await database.Reports
			.Include(candidate => candidate.Answers)
			.Include(candidate => candidate.Files)
			.Include(candidate => candidate.Summary)
			.SingleAsync(candidate => candidate.Id == id);

		report.SoftDelete(DateTimeOffset.UtcNow);
		await database.SaveChangesAsync();
		return reportId;
	}

	private static async Task<Dictionary<string, DateTimeOffset>> SubmittedAtByReportId()
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var rows = await database.Reports
			.Select(report => new { report.Id, report.SubmittedAt })
			.ToListAsync();
		return rows.ToDictionary(row => row.Id.Value, row => row.SubmittedAt);
	}

	private static async Task Violate(FormattableString sql)
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.Database.ExecuteSqlInterpolatedAsync(sql)).ShouldBe(1);
	}

	private async Task<JsonElement> Body()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
		_body ??= await _response.Content.ReadFromJsonAsync<JsonElement>();
		return _body.Value;
	}

	private List<(string Id, DateTimeOffset PublishedAt)> Listed()
	{
		return
		[
			.. _pages.SelectMany(page => page.GetProperty("items").EnumerateArray())
				.Select(item => (item.GetProperty("id").GetString()!, item.GetProperty("publishedAt").GetDateTimeOffset())),
		];
	}
}
