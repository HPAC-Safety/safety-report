using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The review commands through the booted API: edit, publish, unpublish with a
///     note, a manual pair, stale-view refusal, their audit rows, and a report
///     without consent that never needs action (REQ-MOD-032..035, REQ-MOD-055..061,
///     REQ-MOD-090, ADR-0105, ADR-0125).
/// </summary>
[Binding]
[Scope(Feature = "Moderation, authentication, and publication")]
public sealed class ReviewActionSteps(SeededReport seeded) : IDisposable
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string SummaryEn = "The pilot landed in a field.";
	private const string Note = "Synthetic note: duplicate of an earlier report.";

	private string _reportId = string.Empty;
	private string _version = string.Empty;
	private HttpResponseMessage? _response;
	private JsonElement _result;
	private string _requestedAction = string.Empty;
	private CapturingLoggerProvider? _logs;
	private WebApplicationFactory<Program>? _host;
	private MemberRole _role;
	private string _situation = string.Empty;
	private Report? _sourcesReport;
	private List<string> _needsAction = [];
	private int _needsActionCount;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"a reviewer edits either summary language")]
	[Given(@"a report is published")]
	public async Task GivenAPublishedReport()
	{
		// It shows a public image, so there is media metadata the DTO could leak.
		await SeedAndLoad(ReportStatus.Published, true, mediaConsent: true, arrange: report => BootedReports.AddProcessedImage(report));
	}

	[Given(@"^a reviewer publishes the current English/French summary pair$")]
	[Given(@"a report is non-deleted, has explicit positive consent, has two nonblank summary texts, and a reviewer publishes it")]
	[Given(@"a reviewer unpublishes a report with a note")]
	public async Task GivenAConsentedPendingReport()
	{
		await SeedAndLoad(ReportStatus.Pending, true);
	}

	[Given(@"^an? (Pending|Published|Unpublished) report whose reporter consented to publication$")]
	public async Task GivenAConsentedReportIn(string status)
	{
		await SeedAndLoad(Enum.Parse<ReportStatus>(status), true);
	}

	[Given(@"^a report whose reporter did not consent to publication is Unpublished$")]
	public async Task GivenAnUnconsentedReport()
	{
		await SeedAndLoad(ReportStatus.Unpublished, false);
	}

	[Given(@"a report is SummaryFailed")]
	public async Task GivenAFailedReport()
	{
		await SeedAndLoad(ReportStatus.SummaryFailed, true);
	}

	[Given(@"two reviewers opened the same report")]
	public async Task GivenTwoReviewersOpenedTheReport()
	{
		await SeedAndLoad(ReportStatus.Pending, true);
	}

	[Given(@"the first reviewer has saved a change to it")]
	public async Task GivenTheFirstReviewerSaved()
	{
		using var first = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var saved = await first.PutAsJsonAsync(
			$"/api/admin/reports/{_reportId}/summary",
			new { version = _version, aiSummaryEn = "First reviewer's text.", aiSummaryFr = "Texte du premier réviseur." });
		saved.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Given(@"^a report on which a reviewer can (.+)$")]
	public async Task GivenAReportOnWhichAReviewerCan(string action)
	{
		_requestedAction = action;
		var status = action switch
		{
			"unpublish the report" => ReportStatus.Published,
			"write a manual pair" => ReportStatus.SummaryFailed,
			_ => ReportStatus.Pending,
		};

		await SeedAndLoad(status, true);
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"the edit is saved")]
	public async Task WhenTheEditIsSaved()
	{
		await Send("summary", new { version = _version, aiSummaryEn = "The pilot landed firmly.", aiSummaryFr = "Le pilote s'est posé fermement." });
	}

	[When(@"the publication is recorded")]
	[When(@"a reviewer publishes the pair")]
	public async Task WhenPublished()
	{
		await Send("publish", new { version = _version });
	}

	[When(@"^a SafetyOfficer lists reports needing action and reads the pending counts$")]
	public async Task WhenNeedsActionAndCountsAreRead()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_needsAction = [.. (await client.GetFromJsonAsync<JsonElement[]>(new Uri("/api/admin/reports?filter=needs-action", UriKind.Relative)))!
			.Select(item => item.GetProperty("id").GetString()!)];
		_needsActionCount = (await client.GetFromJsonAsync<JsonElement>(new Uri("/api/admin/counts", UriKind.Relative)))
			.GetProperty("reportsNeedingAction").GetInt32();
	}

	[Then(@"that report is not listed")]
	public void ThenThatReportIsNotListed()
	{
		_needsAction.ShouldNotContain(_reportId);
	}

	[Then(@"the reports count does not include it")]
	public void ThenTheCountDoesNotIncludeIt()
	{
		// The count is the Needs action list's length, and that list leaves it out.
		_needsActionCount.ShouldBe(_needsAction.Count);
	}

	[When(@"the unpublishing is recorded")]
	public async Task WhenUnpublishedWithNote()
	{
		_logs = new CapturingLoggerProvider();
		_host = (await BootedApi.Factory()).WithWebHostBuilder(builder =>
			builder.ConfigureLogging(logging => logging.AddProvider(_logs)));

		await Send("unpublish", new { version = _version, note = Note }, _host);
	}

	[When(@"a reviewer unpublishes it")]
	public async Task WhenUnpublished()
	{
		await Send("unpublish", new { version = _version });
	}

	[When(@"a reviewer saves an English and a French summary text")]
	public async Task WhenAManualPairIsSaved()
	{
		await Send("summary", new { version = _version, aiSummaryEn = "The pilot landed.", aiSummaryFr = "Le pilote s'est posé." });
	}

	[When(@"the second reviewer sends a change based on the view they loaded")]
	public async Task WhenTheSecondReviewerSendsAStaleChange()
	{
		await Send("summary", new { version = _version, aiSummaryEn = "Second reviewer's text.", aiSummaryFr = "Texte du second réviseur." });
	}

	[When(@"the reviewer does so")]
	public async Task WhenTheReviewerDoesSo()
	{
		switch (_requestedAction)
		{
			case "edit the summary pair":
			case "write a manual pair":
				await Send("summary", new { version = _version, aiSummaryEn = "The pilot landed.", aiSummaryFr = "Le pilote s'est posé." });
				break;
			case "publish the pair":
				await Send("publish", new { version = _version });
				break;
			case "unpublish the report":
				await Send("unpublish", new { version = _version, note = Note });
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(_requestedAction), _requestedAction, "No such review action.");
		}

		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	// ── Then ────────────────────────────────────────────────────────────────

	[Then(@"the pair's approval is cleared")]
	public void ThenApprovalIsCleared()
	{
		_result.GetProperty("summary").GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		_result.GetProperty("summary").GetProperty("approvedBySubject").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"a previously published report returns to Pending and leaves the public feed")]
	public void ThenBackToPending()
	{
		_result.GetProperty("status").GetString().ShouldBe("pending");
		_result.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"it applies to that pair as a whole, not to one language")]
	public void ThenApprovalCoversThePair()
	{
		var summary = _result.GetProperty("summary");
		summary.GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.String);
		summary.GetProperty("aiSummaryEn").GetString().ShouldBe(SummaryEn);
		summary.GetProperty("aiSummaryFr").GetString().ShouldNotBeNullOrWhiteSpace();
		summary.EnumerateObject().Count(property => property.Name.StartsWith("approved", StringComparison.Ordinal)).ShouldBe(2);
	}

	[Then(@"the approving token subject is recorded as an opaque string")]
	public async Task ThenApproverIsTheTokenSubject()
	{
		var approver = _result.GetProperty("summary").GetProperty("approvedBySubject").GetString();
		approver.ShouldNotBeNullOrWhiteSpace();
		(await AuditEntries(AuditAction.PublishedReport)).Single().ActorSubject.ShouldBe(approver);
	}

	[Then(@"the report remains available for internal learning")]
	public async Task ThenTheReportRemainsAvailable()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await client.GetAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Then(@"the pair is approved and the report is Published in the same action")]
	public async Task ThenPublishedInTheSameAction()
	{
		_result.GetProperty("status").GetString().ShouldBe("published");
		_result.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.String);
		_result.GetProperty("summary").GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.String);
		(await AuditEntries(AuditAction.PublishedReport)).Count.ShouldBe(1);
	}

	[Then(@"no Administrator, migration, background worker, or direct API caller can bypass any of these guards")]
	public async Task ThenNoCallerCanBypassTheGuards()
	{
		// An administrator publishing a report without consent is refused.
		var unconsented = await BootedReports.Seed(ReportStatus.Unpublished, false);
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		var loaded = await admin.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{unconsented}", UriKind.Relative));
		using var refused = await admin.PostAsJsonAsync($"/api/admin/reports/{unconsented}/publish", new { version = loaded.GetProperty("version").GetString() });
		refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);

		// The review command is the only endpoint that publishes; nothing else changes a status.
		var factory = await BootedApi.Factory();
		var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
			.ToList();
		routes.Where(route => route.EndsWith("/publish", StringComparison.OrdinalIgnoreCase))
			.ShouldBe(["/api/admin/reports/{id}/publish"]);
	}

	[Then(@"^the report becomes Published$")]
	public void ThenTheReportBecomesPublished()
	{
		_result.GetProperty("status").GetString().ShouldBe("published");
	}

	[Then(@"the approval and the publication are recorded in one audited action")]
	public async Task ThenOneAuditedAction()
	{
		(await AuditEntries(AuditAction.PublishedReport)).Count.ShouldBe(1);
		(await AuditEntries(AuditAction.ApprovedReport)).ShouldBeEmpty();
	}

	[Then(@"the report goes to Pending")]
	public void ThenPending()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
		_result.GetProperty("status").GetString().ShouldBe("pending");
	}

	[Then(@"it is no longer publishable")]
	public void ThenNoLongerPublishable()
	{
		_result.GetProperty("status").GetString().ShouldNotBe("published");
		_result.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the pair's approval is cleared and the report is Unpublished")]
	public void ThenApprovalClearedAndUnpublished()
	{
		ThenApprovalIsCleared();
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
		_result.GetProperty("status").GetString().ShouldBe("unpublished");
	}

	[Then(@"the unpublishing is audited")]
	public async Task ThenUnpublishingIsAudited()
	{
		(await AuditEntries(AuditAction.UnpublishedReport)).Count.ShouldBe(1);
	}

	[Then(@"the detail view shows the note to reviewers")]
	public async Task ThenTheDetailShowsTheNote()
	{
		_result.GetProperty("unpublishNote").GetString().ShouldBe(Note);

		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		detail.GetProperty("unpublishNote").GetString().ShouldBe(Note);
	}

	[Then(@"the note never reaches the public API, the audit log, or the application logs")]
	public async Task ThenTheNoteStaysReviewerOnly()
	{
		foreach (var entry in await AuditEntries(AuditAction.UnpublishedReport))
		{
			(entry.Detail ?? string.Empty).ShouldNotContain(Note);
		}

		_logs!.Messages.ShouldNotContain(message => message.Contains(Note, StringComparison.Ordinal));

		// The public API reads only the public_reports view, which carries no note
		// column and no unpublished report (#28): the report is not found, and the
		// note is in no page of the feed.
		using var visitor = (await BootedApi.Factory()).CreateClient();
		using var detail = await visitor.GetAsync(new Uri($"/api/v1/public/reports/{_reportId}", UriKind.Relative));
		detail.StatusCode.ShouldBe(HttpStatusCode.NotFound);

		string? after = null;
		do
		{
			var page = await visitor.GetFromJsonAsync<JsonElement>(new Uri(
				after is null ? "/api/v1/public/reports" : $"/api/v1/public/reports?after={Uri.EscapeDataString(after)}",
				UriKind.Relative));
			page.GetRawText().ShouldNotContain(Note);
			after = page.GetProperty("next").GetString();
		}
		while (after is not null);
	}

	[Then(@"unpublishing without a note also succeeds")]
	public async Task ThenUnpublishingWithoutANoteSucceeds()
	{
		var other = await BootedReports.Seed(ReportStatus.Pending, true);
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var loaded = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{other}", UriKind.Relative));
		using var unpublished = await client.PostAsJsonAsync($"/api/admin/reports/{other}/unpublish", new { version = loaded.GetProperty("version").GetString() });
		unpublished.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await unpublished.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("status").GetString().ShouldBe("unpublished");
		body.GetProperty("unpublishNote").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the report has one summary pair with ""manual"" as its model and prompt version")]
	public void ThenTheManualPairIsRecorded()
	{
		var summary = _result.GetProperty("summary");
		summary.GetProperty("model").GetString().ShouldBe("manual");
		summary.GetProperty("promptVersion").GetString().ShouldBe("manual");
	}

	[Then(@"writing the pair is audited")]
	public async Task ThenWritingThePairIsAudited()
	{
		(await AuditEntries(AuditAction.EditedSummary)).Count.ShouldBe(1);
	}

	[Then(@"the API answers 409 with a problem that asks them to reload")]
	public async Task ThenTheApiAnswersStale()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var problem = await _response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("type").GetString().ShouldBe("https://hpac.ca/problems/stale-report");
		problem.GetProperty("detail").GetString()!.ShouldContain("Reload");
	}

	[Then(@"nothing the second reviewer sent is saved")]
	public async Task ThenNothingStaleIsSaved()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		detail.GetProperty("summary").GetProperty("aiSummaryEn").GetString().ShouldBe("First reviewer's text.");
		(await AuditEntries(AuditAction.EditedSummary)).Count.ShouldBe(1);
	}

	[Then(@"^one audit entry records the reviewer's token subject, (\w+), the report, and the time$")]
	public async Task ThenOneAuditEntryRecords(string auditAction)
	{
		var entries = await AuditEntries(Enum.Parse<AuditAction>(auditAction));
		var entry = entries.ShouldHaveSingleItem();
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		entry.TargetType.ShouldBe("Report");
		entry.OccurredAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
	}

	[Then(@"the entry records no summary text, answer, or unpublishing note")]
	public async Task ThenTheEntryRecordsNoContent()
	{
		foreach (var entry in await AuditEntries(null))
		{
			var detail = entry.Detail ?? string.Empty;
			detail.ShouldNotContain("pilot", Case.Insensitive);
			detail.ShouldNotContain(Note);
			detail.ShouldNotContain(BootedReports.PilotName);
		}
	}

	// ── REQ-MOD-069: who may translate ──────────────────────────────────────

	[Given(@"^a member signed in as (User|SafetyOfficer|Administrator)$")]
	public void GivenAMemberSignedInAs(string role)
	{
		_role = Enum.Parse<MemberRole>(role);
	}

	[When(@"that member requests a translation")]
	public async Task WhenThatMemberRequestsATranslation()
	{
		// The booted host has no DeepL key, and there is no stand-in
		// (ADR-0109), so a stub answers in the provider's place. This scenario
		// is about who may ask, not about the provider.
		await using var host = (await BootedApi.Factory()).WithWebHostBuilder(builder =>
			builder.ConfigureTestServices(services =>
				services.AddScoped<ITranslator, PrefixingTranslator>()));

		using var client = await BootedApi.SignedInAs(_role, host);
		_response = await client.PostAsJsonAsync(
			"/api/admin/translate",
			new { texts = new[] { "The pilot landed." }, from = "en-CA", to = "fr-CA" });
	}

	[Then(@"^the API answers (forbidden|a translation)$")]
	public async Task ThenTheApiAnswers(string outcome)
	{
		if (outcome == "forbidden")
		{
			_response!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			return;
		}

		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await _response.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("texts").GetArrayLength().ShouldBe(1);
	}

	// ── REQ-MOD-070: how each language was produced ─────────────────────────

	[Given(@"^(the Worker produced the pair|a reviewer edited only the English text of a generated pair|a reviewer edited the English text and accepted its French translation|a reviewer wrote both texts by hand after summarization failed|a reviewer wrote the French text by hand and accepted its English translation)$")]
	public void GivenASituation(string situation)
	{
		_situation = situation;
	}

	[When(@"the pair is saved")]
	public void WhenThePairIsSaved()
	{
		var at = DateTimeOffset.UtcNow;
		_sourcesReport = _situation.Contains("failed", StringComparison.Ordinal) || _situation.Contains("French text by hand", StringComparison.Ordinal)
			? ReviewLifecycleSteps.In(ReportStatus.SummaryFailed, true)
			: ReviewLifecycleSteps.In(ReportStatus.Pending, true);

		switch (_situation)
		{
			case "the Worker produced the pair":
				break;
			case "a reviewer edited only the English text of a generated pair":
				_sourcesReport.EditSummary("The pilot landed firmly.", _sourcesReport.Summary!.AiSummaryFr, at);
				break;
			case "a reviewer edited the English text and accepted its French translation":
				_sourcesReport.EditSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", at, SummaryTextSource.Human, SummaryTextSource.Machine);
				break;
			case "a reviewer wrote both texts by hand after summarization failed":
				_sourcesReport.WriteManualSummary("The pilot landed.", "Le pilote s'est posé.", at);
				break;
			case "a reviewer wrote the French text by hand and accepted its English translation":
				_sourcesReport.WriteManualSummary("The pilot landed.", "Le pilote s'est posé.", at, SummaryTextSource.Machine, SummaryTextSource.Human);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(_situation), _situation, "No such situation.");
		}
	}

	[Then(@"^the English text is recorded as (\w+) and the French text as (\w+)$")]
	public void ThenTheSourcesAre(string english,
								  string french)
	{
		EnumCode.Of(_sourcesReport!.Summary!.SourceEn).ShouldBe(english);
		EnumCode.Of(_sourcesReport.Summary.SourceFr).ShouldBe(french);
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	public void Dispose()
	{
		_response?.Dispose();
		_host?.Dispose();
		_logs?.Dispose();
	}

	private async Task SeedAndLoad(ReportStatus status,
								   bool consent,
								   bool? mediaConsent = null,
								   Action<Report>? arrange = null)
	{
		_reportId = await BootedReports.Seed(status, consent, arrange, mediaConsent: mediaConsent);
		seeded.Id = _reportId;
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		_version = detail.GetProperty("version").GetString()!;
	}

	private async Task Send(string command,
							object body,
							WebApplicationFactory<Program>? host = null)
	{
		using var client = host is null
			? await BootedApi.SignedInAs(MemberRole.SafetyOfficer)
			: await BootedApi.SignedInAs(MemberRole.SafetyOfficer, host);

		_response = command == "summary"
			? await client.PutAsJsonAsync($"/api/admin/reports/{_reportId}/summary", body)
			: await client.PostAsJsonAsync($"/api/admin/reports/{_reportId}/{command}", body);

		if (_response.StatusCode == HttpStatusCode.OK)
		{
			_result = await _response.Content.ReadFromJsonAsync<JsonElement>();
		}
	}

	private async Task<List<AuditLogEntry>> AuditEntries(AuditAction? action)
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_reportId);

		return await database.AuditLog
			.Where(entry => entry.TargetId == reportId && entry.Action != AuditAction.ViewedRawReport)
			.Where(entry => action == null || entry.Action == action)
			.ToListAsync();
	}

	/// <summary>Keeps every message the host logs, so a scenario can prove what it never logged.</summary>
	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		public ConcurrentQueue<string> Messages { get; } = new();

		public ILogger CreateLogger(string categoryName)
		{
			return new CapturingLogger(Messages);
		}

		public void Dispose()
		{
		}

		private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state)
				where TState : notnull
			{
				return null;
			}

			public bool IsEnabled(LogLevel logLevel)
			{
				return true;
			}

			public void Log<TState>(LogLevel logLevel,
									EventId eventId,
									TState state,
									Exception? exception,
									Func<TState, Exception?, string> formatter)
			{
				messages.Enqueue(formatter(state, exception) + " " + exception);
			}
		}
	}

	/// <summary>A translator that answers without a provider.</summary>
	private sealed class PrefixingTranslator : ITranslator
	{
		public bool IsConfigured => true;

		public Task<IReadOnlyList<string>> Translate(
			IReadOnlyList<string> texts,
			Locale source,
			Locale target,
			CancellationToken cancellationToken)
		{
			return Task.FromResult<IReadOnlyList<string>>([.. texts.Select(text => $"[{target.Code}] {text}")]);
		}
	}
}

/// <summary>
///     The report a scenario's Given step seeded, shared with the other step
///     classes the same scenario binds to. Reqnroll gives each scenario its own.
/// </summary>
public sealed class SeededReport
{
	/// <summary>The seeded report's ID, once a step has seeded one.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>
///     Seeds one synthetic report in a given review state into the booted database,
///     through the domain's own transitions.
/// </summary>
internal static class BootedReports
{
	public const string PilotName = "Morgan Synthetic";

	/// <summary>
	///     Adds an image whose derivative the Worker has already verified, under a
	///     reporter's filename that must never reach a public read.
	/// </summary>
	public static ReportFile AddProcessedImage(Report report,
											   DateTimeOffset? at = null)
	{
		ArgumentNullException.ThrowIfNull(report);

		var fileId = TinyId.New();
		var file = report.AddFile(
			fileId, $"{report.Id}/original/{fileId}", MediaType.Jpeg.ContentType, 1024, "launch-site.jpg", at ?? DateTimeOffset.UtcNow);
		file.RecordStripped($"{report.Id}/stripped/{fileId}", at ?? DateTimeOffset.UtcNow);
		return file;
	}

	public static async Task<string> Seed(ReportStatus status,
										  bool consent,
										  Action<Report>? arrange = null,
										  DateTimeOffset? at = null,
										  bool? mediaConsent = null,
										  bool mediaConsentToEarlierWording = false)
	{
		var factory = await BootedApi.Factory();
		await ReportSubmissionEndpointSteps.ConsentRevisionId();

		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var now = at ?? DateTimeOffset.UtcNow;

		var consentQuestion = await database.Questions
			.Include(question => question.Revisions)
			.SingleAsync(question => question.Key == QuestionKey.ConsentPublish);
		var pilot = Question.Create($"pilot_{Guid.NewGuid():n}"[..24], QuestionType.ShortText, "Pilot name", "Nom du pilote", now, isPrivate: true);
		database.Questions.Add(pilot);

		var report = new Report(Locale.EnCa, now);
		report.Answer(consentQuestion, consent, now);
		report.Answer(pilot, PilotName, now);

		if (mediaConsent is not null)
		{
			var mediaQuestion = await database.Questions
				.Include(question => question.Revisions)
				.SingleAsync(question => question.Key == QuestionKey.ConsentMedia);
			if (mediaConsentToEarlierWording)
			{
				// A stale draft's answer to the wording before it named documents
				// (ADR-0119).
				var earlier = mediaQuestion.Revisions
					.Where(revision => revision.RevisionNumber < mediaQuestion.CurrentRevision.RevisionNumber)
					.MaxBy(revision => revision.RevisionNumber)!;
				report.Answer(mediaQuestion, earlier, mediaConsent.Value, now);
			}
			else
			{
				report.Answer(mediaQuestion, mediaConsent.Value, now);
			}
		}

		arrange?.Invoke(report);
		report.BeginSummarizing();

		if (!consent)
		{
			report.KeepUnpublished();
		}
		else if (status == ReportStatus.SummaryFailed)
		{
			report.FailSummarization("The AI chat provider was unavailable for this summarization attempt.");
		}
		else
		{
			report.AttachSummary(Summary.Generate(report.Id, "The pilot landed in a field.", "Le pilote s'est posé dans un champ.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
			report.AwaitReview();

			switch (status)
			{
				case ReportStatus.Pending:
					break;
				case ReportStatus.Published:
					report.Publish("synthetic-approver", now);
					break;
				case ReportStatus.Unpublished:
					report.Unpublish();
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(status), status, "Not a review state this helper seeds.");
			}
		}

		report.Status.ShouldBe(status);
		database.Reports.Add(report);
		await database.SaveChangesAsync();
		return report.Id.Value;
	}
}
