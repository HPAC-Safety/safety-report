using System.Buffers.Text;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HpacSafety.Api.Authentication;
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
///     The admin report list and read-only detail view, through the booted API
///     (REQ-MOD-030, REQ-MOD-031, REQ-MOD-049..051, REQ-MOD-119, REQ-MOD-124). The booted
///     database is shared by every scenario, so assertions name the reports seeded here.
/// </summary>
[Binding]
public sealed class ReportReviewSteps
{
	private const string PilotName = "Rowan Synthetic";
	private const string SummaryEn = "The pilot landed in a field.";

	private readonly Dictionary<string, string> _seeded = new(StringComparer.Ordinal);
	private List<JsonElement> _listed = [];
	private JsonElement _detail;
	private string _detailBody = string.Empty;
	private string _listBody = string.Empty;
	private string _openedId = string.Empty;
	private string _namedReportId = string.Empty;
	private string _restartedFirstId = string.Empty;
	private string _topFirstId = string.Empty;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"reports exist in every workflow state, one without publication consent, and one soft-deleted")]
	[Given(@"reports exist in every workflow state")]
	public async Task GivenReportsInEveryState()
	{
		if (_seeded.Count == 0)
		{
			await Seed();
		}
	}

	[Given(@"one report has waited in Submitted and one in Summarizing for more than 24 hours")]
	[Given(@"one report has waited in Summarizing for less than 24 hours")]
	public void GivenTheStuckAndFreshReports()
	{
		// Seeded with the rest: stuckSubmitted, stuckSummarizing, freshSummarizing.
		_seeded.ShouldContainKey("stuckSubmitted");
		_seeded.ShouldContainKey("freshSummarizing");
	}

	[Given(@"a reviewer opens a report's detail view")]
	public async Task GivenAReviewerOpensADetailView()
	{
		await Seed();
		_openedId = _seeded["pending"];
	}

	[Given(@"a report whose reporter first name is ""(.*)"", reporter last name is ""(.*)"", pilot first name is ""(.*)"", and pilot last name is ""(.*)""")]
	public async Task GivenAReportWithReporterAndPilotNames(
		string reporterFirst,
		string reporterLast,
		string pilotFirst,
		string pilotLast)
	{
		ArgumentNullException.ThrowIfNull(reporterFirst);
		ArgumentNullException.ThrowIfNull(reporterLast);
		ArgumentNullException.ThrowIfNull(pilotFirst);
		ArgumentNullException.ThrowIfNull(pilotLast);

		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow;
		var consent = await ConsentQuestion(database);
		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);

		await AnswerRoleIfGiven(database, report, QuestionRole.ReporterFirstName, reporterFirst, now);
		await AnswerRoleIfGiven(database, report, QuestionRole.ReporterLastName, reporterLast, now);
		await AnswerRoleIfGiven(database, report, QuestionRole.PilotFirstName, pilotFirst, now);
		await AnswerRoleIfGiven(database, report, QuestionRole.PilotLastName, pilotLast, now);

		database.Reports.Add(report);
		await database.SaveChangesAsync();
		_namedReportId = report.Id.Value;
	}

	[Given(@"a report has one hidden attachment and one still-processing attachment")]
	public async Task GivenAReportWithAHiddenAndAStillProcessingAttachment()
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow;
		var report = new Report(Locale.EnCa, now);
		var hidden = report.AddFile(TinyId.New(), $"{report.Id}/original/hidden", MediaType.Jpeg.ContentType, 1024, "hidden.jpg", now);
		hidden.RecordStripped($"{report.Id}/stripped/hidden", now);
		hidden.HideBy("synthetic-reviewer", now);
		report.AddFile(TinyId.New(), $"{report.Id}/original/processing", MediaType.Jpeg.ContentType, 1024, "processing.jpg", now);

		database.Reports.Add(report);
		await database.SaveChangesAsync();
		_namedReportId = report.Id.Value;
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"a reviewer lists reports")]
	public async Task WhenAReviewerListsReports()
	{
		await ListWith(null);
	}

	[When(@"a reviewer lists reports needing action")]
	public async Task WhenAReviewerListsReportsNeedingAction()
	{
		await ListWith("needs-action");
	}

	[When(@"a reviewer lists reports with the (.+) filter")]
	public async Task WhenAReviewerListsWithFilter(string filter)
	{
		await ListWith(filter);
	}

	[When(@"a reviewer lists reports after a cursor naming a report no longer in the queue")]
	public async Task WhenAReviewerListsAfterAnUnknownCursor()
	{
		// Scenarios running in parallel add newer reports to the shared booted
		// database at any moment, so the restarted page is compared with the top
		// of the list read just before and just after it, retried until the
		// three reads agree.
		const int attempts = 10;
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var cursor = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(TinyId.New().Value));

		for (var attempt = 1; attempt <= attempts; attempt++)
		{
			var before = await FirstListedId(client, string.Empty);
			_restartedFirstId = await FirstListedId(client, $"?after={cursor}");
			_topFirstId = await FirstListedId(client, string.Empty);

			if (before == _restartedFirstId && _restartedFirstId == _topFirstId)
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200));
		}
	}

	private static async Task<string> FirstListedId(HttpClient client,
													string query)
	{
		using var response = await client.GetAsync(new Uri($"/api/admin/reports{query}", UriKind.Relative));
		response.EnsureSuccessStatusCode();
		var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
		return page.GetProperty("items").EnumerateArray().First().GetProperty("id").GetString()!;
	}

	[When(@"the detail query runs")]
	public async Task WhenTheDetailQueryRuns()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await client.GetAsync(new Uri($"/api/admin/reports/{_openedId}", UriKind.Relative));
		response.EnsureSuccessStatusCode();
		_detailBody = await response.Content.ReadAsStringAsync();
		_detail = JsonDocument.Parse(_detailBody).RootElement;
	}

	// ── Then: the list ──────────────────────────────────────────────────────

	[Then(@"every live report appears, newest first, with its workflow status and whether publication consent was refused")]
	public void ThenEveryLiveReportAppearsNewestFirst()
	{
		string[] expected =
		[
			"pending", "private", "failed", "unpublished", "published", "freshSummarizing",
			"stuckSubmitted", "stuckSummarizing",
		];

		Mine().ShouldBe([.. expected.Select(name => _seeded[name])]);
		Row("private").GetProperty("consent").GetBoolean().ShouldBeFalse();
		Row("private").GetProperty("status").GetString().ShouldBe("unpublished");
		Row("pending").GetProperty("status").GetString().ShouldBe("pending");
		Row("published").GetProperty("status").GetString().ShouldBe("published");
		Row("unpublished").GetProperty("status").GetString().ShouldBe("unpublished");
	}

	[Then(@"the soft-deleted report does not appear")]
	public void ThenTheDeletedReportDoesNotAppear()
	{
		Mine().ShouldNotContain(_seeded["deleted"]);
	}

	[Then(@"no answer text or summary text appears in the list, except the reporter's and pilot's names")]
	public void ThenNoContentAppearsInTheList()
	{
		_listBody.ShouldNotContain(PilotName);
		_listBody.ShouldNotContain(SummaryEn);
	}

	[Then(@"the row's reporter name reads ""(.*)""")]
	public void ThenTheRowsReporterNameReads(string expected)
	{
		ArgumentNullException.ThrowIfNull(expected);
		ReadNullableString(RowById(_namedReportId), "reporterName").ShouldBe(expected.Length == 0 ? null : expected);
	}

	[Then(@"the row's attachment count is (\d+)")]
	public void ThenTheRowsAttachmentCountIs(int expected)
	{
		RowById(_namedReportId).GetProperty("attachmentCount").GetInt32().ShouldBe(expected);
	}

	[Then(@"the row's pilot name reads ""(.*)""")]
	public void ThenTheRowsPilotNameReads(string expected)
	{
		ArgumentNullException.ThrowIfNull(expected);
		ReadNullableString(RowById(_namedReportId), "pilotName").ShouldBe(expected.Length == 0 ? null : expected);
	}

	[Then(@"the list holds the pending, summary-failed, and two stuck reports")]
	public void ThenTheNeedsActionListHoldsTheRightReports()
	{
		Mine().ShouldBe(
			[.. new[] { "pending", "failed", "stuckSubmitted", "stuckSummarizing" }.Select(name => _seeded[name])],
			ignoreOrder: true);
	}

	[Then(@"that list starts with the same report the first page did")]
	public void ThenTheRestartedListStartsWithTheSameReport()
	{
		_restartedFirstId.ShouldBe(_topFirstId);
	}

	[Then(@"each stuck report is marked stuck")]
	public void ThenEachStuckReportIsMarked()
	{
		Row("stuckSubmitted").GetProperty("isStuck").GetBoolean().ShouldBeTrue();
		Row("stuckSummarizing").GetProperty("isStuck").GetBoolean().ShouldBeTrue();
	}

	[Then(@"the report summarizing for less than 24 hours is not listed")]
	public void ThenTheFreshReportIsNotListed()
	{
		Mine().ShouldNotContain(_seeded["freshSummarizing"]);
	}

	[Then(@"the list holds only (.+)")]
	public void ThenTheListHoldsOnly(string reports)
	{
		string[] expected = reports switch
		{
			"published reports" => ["published"],
			"live reports whose reporter refused consent" => ["private"],
			"unpublished reports" => ["private", "unpublished"],
			"reports whose summarization failed" => ["failed"],
			_ => throw new ArgumentOutOfRangeException(nameof(reports), reports, "No seeded report matches."),
		};

		Mine().ShouldBe([.. expected.Select(name => _seeded[name])]);
	}

	[Then(@"each row carries the same version the report's detail view gives")]
	public async Task ThenEachRowCarriesTheDetailVersion()
	{
		foreach (var name in new[] { "pending", "private", "published" })
		{
			using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
			var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_seeded[name]}", UriKind.Relative));

			Row(name).GetProperty("version").GetString().ShouldBe(detail.GetProperty("version").GetString());
		}
	}

	[Then(@"publishing an unpublished report with its row's version succeeds without opening the report")]
	public async Task ThenPublishingWithTheRowVersionSucceeds()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await client.PostAsJsonAsync(
			new Uri($"/api/admin/reports/{_seeded["unpublished"]}/publish", UriKind.Relative),
			new { version = Row("unpublished").GetProperty("version").GetString() });

		response.EnsureSuccessStatusCode();
		var published = await response.Content.ReadFromJsonAsync<JsonElement>();
		published.GetProperty("status").GetString().ShouldBe("published");
	}

	[Then(@"no ViewedRawReport entry is written for that report")]
	public async Task ThenNoViewedRawReportEntryIsWritten()
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_seeded["unpublished"]);

		(await database.AuditLog.CountAsync(entry => entry.Action == AuditAction.ViewedRawReport && entry.TargetId == reportId))
			.ShouldBe(0);
		(await database.AuditLog.CountAsync(entry => entry.Action == AuditAction.PublishedReport && entry.TargetId == reportId))
			.ShouldBe(1);
	}

	// ── Then: the detail view ───────────────────────────────────────────────

	[Then(@"^it supplies the reporter language, exact bilingual question labels and each question's type, answers with privacy indicated, processing state, both summary texts with their shared provenance/approval, and each attachment's kind and whether it can be opened$")]
	public void ThenTheDetailSuppliesWhatTheReviewerNeeds()
	{
		_detail.GetProperty("language").GetString().ShouldBe("en-CA");
		_detail.GetProperty("status").GetString().ShouldBe("pending");

		var pilot = _detail.GetProperty("answers").EnumerateArray()
			.Single(answer => answer.GetProperty("labelEn").GetString() == "Pilot name");
		pilot.GetProperty("labelFr").GetString().ShouldBe("Nom du pilote");
		pilot.GetProperty("type").GetString().ShouldBe("short_text");
		pilot.GetProperty("isPrivate").GetBoolean().ShouldBeTrue();
		pilot.GetProperty("values")[0].GetProperty("value").GetString().ShouldBe(PilotName);

		var summary = _detail.GetProperty("summary");
		summary.GetProperty("aiSummaryEn").GetString().ShouldBe(SummaryEn);
		summary.GetProperty("aiSummaryFr").GetString().ShouldNotBeNullOrWhiteSpace();
		summary.GetProperty("model").GetString().ShouldBe("gemini-3.7-flash");
		summary.GetProperty("promptVersion").GetString().ShouldBe("summarize-anonymize.v3");
		summary.TryGetProperty("approvedAt", out _).ShouldBeTrue();

		var attachment = _detail.GetProperty("attachments")[0];
		attachment.GetProperty("kind").GetString().ShouldBe("document");
		attachment.GetProperty("state").GetString().ShouldBe("ready");
	}

	[Then(@"it supplies no storage key and no link; an attachment is opened only through its own audited view or download request")]
	public void ThenTheDetailSuppliesNoKeyOrLink()
	{
		_detailBody.ShouldNotContain("/original/");
		_detailBody.ShouldNotContain("http", Case.Insensitive);
	}

	[Then(@"an audit entry records the reviewer's token subject, ViewedRawReport, the report, and the time")]
	public async Task ThenAViewedRawReportRowIsWritten()
	{
		var entry = await ViewedRawReportEntry();
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		entry.TargetType.ShouldBe("Report");
		entry.OccurredAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
	}

	[Then(@"the audit entry records no report content")]
	public async Task ThenTheAuditEntryRecordsNoContent()
	{
		var entry = await ViewedRawReportEntry();
		(entry.Detail ?? string.Empty).ShouldNotContain(PilotName);
		(entry.Detail ?? string.Empty).ShouldNotContain(SummaryEn);
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private async Task<AuditLogEntry> ViewedRawReportEntry()
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_openedId);

		return await database.AuditLog
			.Where(entry => entry.Action == AuditAction.ViewedRawReport && entry.TargetId == reportId)
			.SingleAsync();
	}

	/// <summary>
	///     Follows <c>next</c> until it is null, so every scenario sees the whole
	///     list regardless of page size (REQ-MOD-129), de-duplicating by ID as it
	///     goes — the same rule <c>useInfiniteReportList</c> applies on the web. A
	///     cursor naming a report no longer in the queue (deleted, or moved past by
	///     a concurrent write in this shared-collection database) restarts the
	///     underlying query from the top rather than failing (ADR-0155), which
	///     without de-duplication here would show an earlier page's rows a second
	///     time and break a lookup expecting exactly one match.
	/// </summary>
	private async Task ListWith(string? filter)
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var seen = new HashSet<string>(StringComparer.Ordinal);
		_listed = [];
		var bodies = new List<string>();
		string? after = null;

		do
		{
			var query = string.Join('&', new[]
			{
				filter is null ? null : $"filter={filter}",
				after is null ? null : $"after={after}",
			}.Where(part => part is not null));

			var path = query.Length == 0 ? "/api/admin/reports" : $"/api/admin/reports?{query}";
			using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
			response.EnsureSuccessStatusCode();
			var body = await response.Content.ReadAsStringAsync();
			bodies.Add(body);
			var page = JsonDocument.Parse(body).RootElement;

			foreach (var item in page.GetProperty("items").EnumerateArray())
			{
				if (seen.Add(item.GetProperty("id").GetString()!))
				{
					_listed.Add(item);
				}
			}

			after = page.GetProperty("next").ValueKind == JsonValueKind.String ? page.GetProperty("next").GetString() : null;
		} while (after is not null);

		_listBody = string.Join('\n', bodies);
	}

	private List<string> Mine()
	{
		var mine = _seeded.Values.ToHashSet(StringComparer.Ordinal);
		return [.. _listed.Select(item => item.GetProperty("id").GetString()!).Where(mine.Contains)];
	}

	private JsonElement Row(string name)
	{
		return _listed.Single(item => item.GetProperty("id").GetString() == _seeded[name]);
	}

	private JsonElement RowById(string id)
	{
		return _listed.Single(item => item.GetProperty("id").GetString() == id);
	}

	private static string? ReadNullableString(JsonElement element,
											  string property)
	{
		var value = element.GetProperty(property);
		return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
	}

	/// <summary>
	///     The one live question carrying a reporter- or pilot-name role — the
	///     seeded "First name"/"Last name" question under "From:" or "Pilot:"
	///     (ADR-0154). Answers to it are the one exception the admin list shows.
	/// </summary>
	private static async Task<Question> RoleQuestion(HpacSafetyDbContext database,
													  QuestionRole role)
	{
		return await database.Questions
			.Include(question => question.Revisions)
			.SingleAsync(question => question.Role == role && question.Deleted == null);
	}

	private static async Task AnswerRoleIfGiven(
		HpacSafetyDbContext database,
		Report report,
		QuestionRole role,
		string value,
		DateTimeOffset at)
	{
		if (value.Length == 0)
		{
			return;
		}

		var question = await RoleQuestion(database, role);
		report.Answer(question, value, at);
	}

	/// <summary>
	///     The one seeded consent question, read by role: a role lives on at most
	///     one live question (ix_questions_role, ADR-0154), so this never races
	///     with another scenario the way finding-or-creating one by an assumed key
	///     would.
	/// </summary>
	private static async Task<Question> ConsentQuestion(HpacSafetyDbContext database)
	{
		return await RoleQuestion(database, QuestionRole.ConsentPublish);
	}

	/// <summary>
	///     One report per workflow state, submitted a minute apart in the future so
	///     their order is known and no other scenario's reports interleave, plus two
	///     reports stuck for more than a day.
	/// </summary>
	private async Task Seed()
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow;
		var suffix = Guid.NewGuid().ToString("n")[..8];
		var consent = await ConsentQuestion(database);

		var pilot = Question.Create($"pilot_{suffix}", QuestionType.ShortText, "Pilot name", "Nom du pilote", now, isPrivate: true);
		database.Questions.Add(pilot);

		var future = now.AddYears(6);
		var offset = 0;

		Report Add(string name,
				   bool consentAnswer,
				   DateTimeOffset? submittedAt = null)
		{
			var report = new Report(Locale.EnCa, submittedAt ?? future.AddMinutes(-offset++));
			report.Answer(consent, consentAnswer, now);
			report.Answer(pilot, PilotName, now);
			database.Reports.Add(report);
			_seeded[name] = report.Id.Value;
			return report;
		}

		void Summarize(Report report)
		{
			report.BeginSummarizing();
			report.AttachSummary(Summary.Generate(report.Id, SummaryEn, "Le pilote s'est posé dans un champ.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
			report.AwaitReview();
		}

		var pending = Add("pending", true);
		Summarize(pending);
		pending.AddFile(TinyId.New(), $"{pending.Id}/original/doc", "application/pdf", 10, "synthetic.pdf", now);

		var unconsented = Add("private", false);
		unconsented.BeginSummarizing();
		unconsented.KeepUnpublished();

		var failed = Add("failed", true);
		failed.BeginSummarizing();
		failed.FailSummarization("The AI chat provider was unavailable for this summarization attempt.");

		var unpublished = Add("unpublished", true);
		Summarize(unpublished);
		unpublished.Unpublish();

		var published = Add("published", true);
		Summarize(published);
		published.Publish("synthetic-approver", now);

		Add("freshSummarizing", true).BeginSummarizing();
		Add("deleted", true).SoftDelete(now);
		Add("stuckSubmitted", true, now.AddHours(-25));
		Add("stuckSummarizing", true, now.AddHours(-26)).BeginSummarizing();

		await database.SaveChangesAsync();
	}
}
