using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Api.PublicReports;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The anonymous public feed and detail through the booted API, read from the
///     <c>public_reports</c> view (#28): the allowlisted DTO (REQ-MOD-036,
///     REQ-MED-014), the paginated feed (REQ-MOD-037), the indistinguishable 404
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
[Scope(Feature = "Moderation, authentication, and publication")]
[Scope(Feature = "Attachments")]
[Scope(Feature = "Domain and lifecycle")]
public sealed class PublicReportFeedSteps(SeededReport seeded)
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string Feed = "/api/v1/public/reports";

	private static readonly string[] Allowlist = ["id", "aiSummaryEn", "aiSummaryFr", "publishedAt", "commentCount"];

	private readonly List<string> _publishable = [];
	private readonly List<string> _hidden = [];
	private readonly List<HttpResponseMessage> _responses = [];
	private readonly List<JsonElement> _pages = [];
	private HttpResponseMessage? _response;
	private JsonElement? _body;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"a report has been published")]
	public async Task GivenAReportHasBeenPublished()
	{
		// It carries an attachment, so there is something the DTO could leak.
		seeded.Id = await BootedReports.Seed(
			ReportStatus.Published,
			"yes",
			report => report.AddFile($"reports/{Guid.NewGuid():n}.jpg", "image/jpeg", 1024, DateTimeOffset.UtcNow));
	}

	[Given(@"some reports are publishable and others are not")]
	public async Task GivenSomeReportsArePublishableAndOthersAreNot()
	{
		// One more than a page, all published in the same instant, so the feed
		// has to page and has to break ties by ID to do it deterministically.
		var instant = DateTimeOffset.UtcNow.AddYears(50);

		for (var index = 0; index <= PublicReportEndpoints.PageSize; index++)
		{
			_publishable.Add(await BootedReports.Seed(ReportStatus.Published, "yes", at: instant));
		}

		_hidden.Add(await BootedReports.Seed(ReportStatus.PendingReview, "yes", at: instant));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Rejected, "yes", at: instant));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Approved, "no", at: instant));
		_hidden.Add(await Deleted(await BootedReports.Seed(ReportStatus.Published, "yes", at: instant)));
	}

	[Given(@"a report id is unknown, deleted, unapproved, rejected, or not consented")]
	public async Task GivenNonPublicReportIds()
	{
		_hidden.Add(TinyId.New().Value);
		_hidden.Add(await Deleted(await BootedReports.Seed(ReportStatus.Published, "yes")));
		_hidden.Add(await BootedReports.Seed(ReportStatus.PendingReview, "yes"));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Rejected, "yes"));
		_hidden.Add(await BootedReports.Seed(ReportStatus.Approved, "no"));
	}

	[Given(@"a report and its summary row are not deleted")]
	[Given(@"a report otherwise satisfies every publication invariant")]
	public async Task GivenAPublishableReport()
	{
		seeded.Id = await BootedReports.Seed(ReportStatus.Published, "yes");
	}

	[Given(@"ConsentPublish is exactly true")]
	[Given(@"both English and French summary texts are nonblank")]
	[Given(@"the pair has a current human approval")]
	[Given(@"the report has not been rejected")]
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
		await Violate($"UPDATE reports SET consent_publish = NULL WHERE id = {seeded.Id}");
	}

	[Given(@"the English or French summary text is blank")]
	public async Task GivenASummaryTextIsBlank()
	{
		await Violate($"UPDATE summaries SET ai_summary_fr = '  ' WHERE report_id = {seeded.Id}");
	}

	[Given(@"the pair has no current human approval")]
	public async Task GivenThePairHasNoApproval()
	{
		await Violate($"UPDATE summaries SET approved_at = NULL, approved_by_subject = NULL WHERE report_id = {seeded.Id}");
	}

	[Given(@"the report has been rejected")]
	public async Task GivenTheReportHasBeenRejected()
	{
		await Violate($"UPDATE reports SET status = 'rejected' WHERE id = {seeded.Id}");
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
		}
		while (after is not null && _pages.Count < 1000);
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

	[Then(@"the response contains only the opaque report ID, ai_summary_en, ai_summary_fr, the publication timestamp, and the number of visible comments")]
	[Then(@"the public DTO contains no file counts, types, keys, or links")]
	public async Task ThenTheResponseIsExactlyTheAllowlist()
	{
		var body = await Body();
		body.EnumerateObject().Select(property => property.Name).ShouldBe(Allowlist, ignoreOrder: true);
		body.GetProperty("id").GetString().ShouldBe(seeded.Id);
	}

	[Then(@"it never contains question keys, labels, answers, consent value, report language, private flags, raw reports, attachment metadata or URLs, member or reviewer identities, model provenance, or audit records")]
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
		raw.ShouldNotContain("en-CA");
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

	[Then(@"the list is newest published first, a tie broken by report ID, and each page names the cursor that continues it")]
	public void ThenTheOrderIsTotalAndTheCursorContinuesIt()
	{
		var listed = Listed();
		var expected = listed
			.OrderByDescending(item => item.PublishedAt)
			.ThenByDescending(item => item.Id, StringComparer.Ordinal)
			.ToList();

		listed.ShouldBe(expected);
		_pages[^1].GetProperty("next").ValueKind.ShouldBe(JsonValueKind.Null);
		_pages[..^1].ShouldAllBe(page => page.GetProperty("next").ValueKind == JsonValueKind.String);
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
