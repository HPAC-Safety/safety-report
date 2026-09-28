using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Fuzzy public search over the published summary and visible member
///     comments, in the visitor's current site language only (#574,
///     ADR-0157, REQ-MOD-140..149). Every match, and every non-match, is
///     read straight through <c>GET /api/v1/public/reports?q=</c>, the same
///     anonymous endpoint the public feed uses.
/// </summary>
/// <remarks>
///     The fixed seeded summary — "The pilot landed in a field." /
///     "Le pilote s'est posé dans un champ." — is shared with every other
///     Reqnroll scenario in this suite (<c>BootedReports.Seed</c>), so most
///     scenarios below quote it rather than choosing their own text; the
///     quoted text in each <c>Given</c> step is documentation, not an
///     argument the step reads. Every report and comment is synthetic.
/// </remarks>
[Binding]
[Scope(Feature = "Moderation, authentication, and publication")]
public sealed class PublicSearchSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string Feed = "/api/v1/public/reports";

	private readonly Dictionary<string, string> _reportIdsByLabel = [];
	private JsonElement _results;
	private List<string> _allIds = [];
	private bool _resultsMatchThePlainFeed;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"a published report whose English summary says {string}")]
	public async Task GivenAPublishedReportWithFixedSummary(string summaryEn)
	{
		_ = summaryEn; // documentation only — see the class remarks.
		_reportIdsByLabel["report"] = await BootedReports.Seed(ReportStatus.Published, true);
	}

	[Given(@"a published report whose French summary mentions a word its English summary does not")]
	public async Task GivenAPublishedReportWithFrenchOnlyWord()
	{
		_reportIdsByLabel["report"] = await BootedReports.Seed(ReportStatus.Published, true);
	}

	[Given(@"a published report whose pilot's name is answered privately")]
	public async Task GivenAPublishedReportWithAPrivatePilotName()
	{
		_reportIdsByLabel["report"] = await BootedReports.Seed(ReportStatus.Published, true);
	}

	[Given(@"a published report carrying a member comment that says {string}")]
	public async Task GivenAPublishedReportWithAComment(string text)
	{
		var reportId = await BootedReports.Seed(ReportStatus.Published, true);
		_reportIdsByLabel["report"] = reportId;
		await PostComment(reportId, text, "en-CA");
	}

	[Given(@"a published report carrying a comment that is later hidden")]
	public async Task GivenAPublishedReportWithAHiddenComment()
	{
		var reportId = await BootedReports.Seed(ReportStatus.Published, true);
		_reportIdsByLabel["hidden-report"] = reportId;
		var commentId = await PostComment(reportId, "Synthetic soon-hidden note about the carburetor icing.", "en-CA");
		await Hide(commentId);
	}

	[Given(@"another published report carrying a comment that is later deleted")]
	public async Task GivenAnotherPublishedReportWithADeletedComment()
	{
		var reportId = await BootedReports.Seed(ReportStatus.Published, true);
		_reportIdsByLabel["deleted-report"] = reportId;
		var author = $"member:{Guid.NewGuid():n}";
		var commentId = await PostComment(reportId, "Synthetic soon-deleted note about the alternator failing.", "en-CA", author);
		await DeleteComment(reportId, commentId, author);
	}

	[Given(@"^a (pending|unpublished) report whose summary would otherwise match$")]
	public async Task GivenAPendingOrUnpublishedReport(string status)
	{
		var reportStatus = status == "pending" ? ReportStatus.Pending : ReportStatus.Unpublished;
		_reportIdsByLabel["report"] = await BootedReports.Seed(reportStatus, true);
	}

	[Given(@"a no-consent report whose summary would otherwise match")]
	public async Task GivenANoConsentReport()
	{
		// The domain never attaches a summary when consent is withheld
		// (BootedReports.Seed), so there is no summary text to search in the
		// first place — the strongest form of "never matches." Publish
		// normally, then break only the consent flag directly, the same
		// invariant-violation pattern PublicReportFeedSteps uses, so the row
		// still carries the fixed matching summary text.
		var reportId = await BootedReports.Seed(ReportStatus.Published, true);
		await Violate($"UPDATE reports SET consent_publish = NULL WHERE id = {reportId}");
		_reportIdsByLabel["report"] = reportId;
	}

	[Given(@"a deleted report whose summary would otherwise match")]
	public async Task GivenADeletedReport()
	{
		var reportId = await BootedReports.Seed(ReportStatus.Published, true);
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(reportId);
		var report = await database.Reports
			.Include(candidate => candidate.Answers)
			.Include(candidate => candidate.Files)
			.Include(candidate => candidate.Summary)
			.SingleAsync(candidate => candidate.Id == id);
		report.SoftDelete(DateTimeOffset.UtcNow);
		await database.SaveChangesAsync();
		_reportIdsByLabel["report"] = reportId;
	}

	[Given(@"a published report whose summary contains the exact phrase ""hydraulic leak""")]
	public async Task GivenAPublishedReportWithAnExactPhraseMatch()
	{
		_reportIdsByLabel["exact-match"] = await SeedWithSummary(
			"A hydraulic leak was found during the pre-flight inspection.",
			"Une fuite hydraulique a été trouvée lors de l'inspection avant vol.");
	}

	[Given(@"another published report whose summary only misspells ""hydraulic leak""")]
	public async Task GivenAnotherPublishedReportThatOnlyMisspellsTheTerm()
	{
		// Neither word is spelled correctly, so this can only ever be a
		// trigram typo match — never full-text or an exact substring — and
		// its score is bounded well below the exact match's.
		_reportIdsByLabel["typo-match"] = await SeedWithSummary(
			"A hydrolic leek was noticed on the ramp.",
			"Une fuite hydrolique a été notée sur le tarmac.");
	}

	// "some reports are publishable and others are not", and the pagination
	// and ordering Then steps this scenario shares with REQ-MOD-037, are
	// PublicReportFeedSteps' own bindings — this feature scope must not
	// redefine them, or Reqnroll sees an ambiguous match.

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"^a visitor searches ""(.+)"" in (English|French)$")]
	public async Task WhenAVisitorSearches(string query,
										   string language)
	{
		await Search(query, language == "French" ? "fr-CA" : "en-CA");
	}

	[When(@"a visitor searches for the pilot's name")]
	public async Task WhenAVisitorSearchesForThePilotsName()
	{
		await Search(BootedReports.PilotName, "en-CA");
	}

	[When(@"a visitor searches its summary's distinctive word")]
	public async Task WhenAVisitorSearchesItsSummarysDistinctiveWord()
	{
		await Search("landed", "en-CA");
	}

	[When(@"a visitor searches the hidden comment's distinctive word")]
	public async Task WhenAVisitorSearchesTheHiddenCommentsDistinctiveWord()
	{
		await Search("carburetor icing", "en-CA");
	}

	[When(@"a visitor searches the deleted comment's distinctive word")]
	public async Task WhenAVisitorSearchesTheDeletedCommentsDistinctiveWord()
	{
		await Search("alternator failing", "en-CA");
	}

	[When(@"^a visitor searches that French-only word in (English|French)$")]
	public async Task WhenAVisitorSearchesTheFrenchOnlyWord(string language)
	{
		await Search("champ", language == "French" ? "fr-CA" : "en-CA");
	}

	[When(@"a visitor searches the misspelling {string}")]
	public async Task WhenAVisitorSearchesTheMisspelling(string typo)
	{
		await Search(typo, "en-CA");
	}

	[When(@"the public feed is queried with a blank search box")]
	public async Task WhenThePublicFeedIsQueriedWithABlankSearchBox()
	{
		// A blank q= must take the exact same path as no q= at all — proven by
		// comparing them directly, not by re-deriving the ordering rule
		// REQ-MOD-037 already covers.
		using var client = (await BootedApi.Factory()).CreateClient();
		var withBlankQuery = await client.GetFromJsonAsync<JsonElement>(new Uri($"{Feed}?q=", UriKind.Relative));
		var withNoQuery = await client.GetFromJsonAsync<JsonElement>(new Uri(Feed, UriKind.Relative));
		_results = withBlankQuery.GetProperty("items");
		_resultsMatchThePlainFeed = withBlankQuery.GetRawText() == withNoQuery.GetRawText();
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"the report is listed among the results")]
	public void ThenTheReportIsListedAmongTheResults()
	{
		Ids().ShouldContain(_reportIdsByLabel["report"]);
	}

	[Then(@"the report is not listed among the results")]
	public void ThenTheReportIsNotListedAmongTheResults()
	{
		Ids().ShouldNotContain(_reportIdsByLabel["report"]);
	}

	[Then(@"the report with the hidden comment is not listed among the results")]
	public void ThenTheReportWithTheHiddenCommentIsNotListed()
	{
		Ids().ShouldNotContain(_reportIdsByLabel["hidden-report"]);
	}

	[Then(@"the report with the deleted comment is not listed among the results")]
	public void ThenTheReportWithTheDeletedCommentIsNotListed()
	{
		Ids().ShouldNotContain(_reportIdsByLabel["deleted-report"]);
	}

	[Then(@"the exact match is ranked above the misspelled match")]
	public void ThenTheExactMatchIsRankedAboveTheMisspelledMatch()
	{
		var ids = Ids();
		var exactIndex = ids.IndexOf(_reportIdsByLabel["exact-match"]);
		var typoIndex = ids.IndexOf(_reportIdsByLabel["typo-match"]);

		exactIndex.ShouldBeGreaterThanOrEqualTo(0);
		typoIndex.ShouldBeGreaterThanOrEqualTo(0);
		exactIndex.ShouldBeLessThan(typoIndex);
	}

	[Then(@"a blank search box's first page is identical to the plain feed's first page")]
	public void ThenABlankSearchBoxsFirstPageIsIdenticalToThePlainFeedsFirstPage()
	{
		_results.GetArrayLength().ShouldBeGreaterThan(0);
		_resultsMatchThePlainFeed.ShouldBeTrue();
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	/// <summary>
	///     Walks every page of a search, not just the first: the acceptance
	///     database is shared and cumulative across every scenario in the run,
	///     and many of them seed the very same fixed summary text this class
	///     also searches for, so a report this scenario cares about can rank
	///     past the first page. Matching REQ-MOD-149's own scope, this stops
	///     well short of any real pathological case.
	/// </summary>
	private async Task Search(string query,
							  string locale)
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var ids = new List<string>();
		string? after = null;
		var pages = 0;

		do
		{
			var uri = $"{Feed}?q={Uri.EscapeDataString(query)}&locale={Uri.EscapeDataString(locale)}"
					  + (after is null ? "" : $"&after={Uri.EscapeDataString(after)}");
			var page = await client.GetFromJsonAsync<JsonElement>(new Uri(uri, UriKind.Relative));
			var items = page.GetProperty("items");

			if (pages == 0)
			{
				_results = items;
			}

			ids.AddRange(items.EnumerateArray().Select(item => item.GetProperty("id").GetString()!));
			after = page.GetProperty("next").GetString();
			pages++;
		}
		while (after is not null && pages < 1000);

		_allIds = ids;
	}

	private List<string> Ids()
	{
		return _allIds;
	}

	private static async Task<string> PostComment(string reportId,
												  string text,
												  string locale,
												  string? author = null)
	{
		using var client = BootedApi.SignedInAsMember(await BootedApi.Factory(), author ?? $"member:{Guid.NewGuid():n}");
		var uri = new Uri($"{Feed}/{reportId}/comments/", UriKind.Relative);
		using var response = await client.PostAsJsonAsync(uri, new { text, locale });
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("id").GetString()!;
	}

	private static async Task DeleteComment(string reportId,
											string commentId,
											string author)
	{
		using var client = BootedApi.SignedInAsMember(await BootedApi.Factory(), author);
		using var response = await client.DeleteAsync(new Uri($"{Feed}/{reportId}/comments/{commentId}", UriKind.Relative));
		response.EnsureSuccessStatusCode();
	}

	private static async Task Hide(string commentId)
	{
		using var officer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await officer.PostAsync(new Uri($"/api/admin/comments/{commentId}/hide", UriKind.Relative), null);
		response.EnsureSuccessStatusCode();
	}

	private static async Task Violate(FormattableString sql)
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.Database.ExecuteSqlInterpolatedAsync(sql)).ShouldBe(1);
	}

	/// <summary>
	///     Seeds a published report with its own summary text, for the one
	///     scenario (best-match ranking) that needs text the shared fixed seed
	///     does not carry. Otherwise identical to <c>BootedReports.Seed</c>.
	/// </summary>
	private static async Task<string> SeedWithSummary(string summaryEn,
													   string summaryFr)
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var now = DateTimeOffset.UtcNow;

		var consentQuestion = await database.Questions
			.Include(question => question.Revisions)
			.SingleAsync(question => question.Role == QuestionRole.ConsentPublish);

		var report = new Report(Locale.EnCa, now);
		report.Answer(consentQuestion, true, now);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, summaryEn, summaryFr, "gemini-3.7-flash", "summarize-anonymize.v3", now));
		report.AwaitReview();
		report.Publish("synthetic-approver", now);

		database.Reports.Add(report);
		await database.SaveChangesAsync();
		return report.Id.Value;
	}
}
