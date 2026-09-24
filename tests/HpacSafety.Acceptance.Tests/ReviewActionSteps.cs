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
///     The review commands through the booted API: edit, approve-and-publish,
///     reject with a note, reopen, unpublish, a manual pair, stale-view refusal, and
///     their audit rows (REQ-MOD-032..035, REQ-MOD-055..061, ADR-0105).
/// </summary>
[Binding]
[Scope(Feature = "Moderation, authentication, and publication")]
public sealed class ReviewActionSteps : IDisposable
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

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"a reviewer edits either summary language")]
	[Given(@"a report is published")]
	public async Task GivenAPublishedReport()
	{
		await SeedAndLoad(ReportStatus.Published, "yes");
	}

	[Given(@"^a reviewer approves the current English/French summary pair$")]
	[Given(@"a report is non-deleted, has explicit positive consent, has two nonblank summary texts, and a reviewer approves the pair")]
	public async Task GivenAConsentedPendingReport()
	{
		await SeedAndLoad(ReportStatus.PendingReview, "yes");
	}

	[Given(@"^a pending-review report whose reporter answered (yes|no) to publication$")]
	public async Task GivenAPendingReportWithConsent(string consent)
	{
		await SeedAndLoad(ReportStatus.PendingReview, consent);
	}

	[Given(@"a reviewer rejects a report")]
	[Given(@"a reviewer rejects a report with a note")]
	public async Task GivenAReviewerRejectsAReport()
	{
		await SeedAndLoad(ReportStatus.PendingReview, "yes");
	}

	[Given(@"a reviewer rejected a report")]
	public async Task GivenARejectedReport()
	{
		await SeedAndLoad(ReportStatus.Rejected, "yes");
	}

	[Given(@"a report is SummaryFailed")]
	public async Task GivenAFailedReport()
	{
		await SeedAndLoad(ReportStatus.SummaryFailed, "yes");
	}

	[Given(@"two reviewers opened the same report")]
	public async Task GivenTwoReviewersOpenedTheReport()
	{
		await SeedAndLoad(ReportStatus.PendingReview, "yes");
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
			"reopen the report" => ReportStatus.Rejected,
			"unpublish the report" => ReportStatus.Published,
			"write a manual pair" => ReportStatus.SummaryFailed,
			_ => ReportStatus.PendingReview,
		};

		await SeedAndLoad(status, "yes");
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"the edit is saved")]
	public async Task WhenTheEditIsSaved()
	{
		await Send("summary", new { version = _version, aiSummaryEn = "The pilot landed firmly.", aiSummaryFr = "Le pilote s'est posé fermement." });
	}

	[When(@"the approval is recorded")]
	[When(@"a reviewer approves the pair")]
	public async Task WhenApproved()
	{
		await Send("approve", new { version = _version });
	}

	[When(@"the rejection is recorded")]
	public async Task WhenRejected()
	{
		_logs = new CapturingLoggerProvider();
		_host = (await BootedApi.Factory()).WithWebHostBuilder(builder =>
			builder.ConfigureLogging(logging => logging.AddProvider(_logs)));

		await Send("reject", new { version = _version, note = Note }, _host);
	}

	[When(@"a reviewer reopens it")]
	public async Task WhenReopened()
	{
		await Send("reopen", new { version = _version });
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
			case "approve the pair":
				await Send("approve", new { version = _version });
				break;
			case "reject the report":
				await Send("reject", new { version = _version, note = Note });
				break;
			case "reopen the report":
				await Send("reopen", new { version = _version });
				break;
			case "unpublish the report":
				await Send("unpublish", new { version = _version });
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

	[Then(@"a previously published report is unpublished")]
	public void ThenUnpublished()
	{
		_result.GetProperty("status").GetString().ShouldBe("pending_review");
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
		(await AuditEntries(AuditAction.ApprovedReport)).Single().ActorSubject.ShouldBe(approver);
	}

	[Then(@"the report can never satisfy the publication invariant")]
	public async Task ThenARejectedReportCannotBePublished()
	{
		_result.GetProperty("status").GetString().ShouldBe("rejected");
		await Send("approve", new { version = _result.GetProperty("version").GetString() });
		_response!.StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}

	[Then(@"the report remains available for internal learning")]
	public async Task ThenTheReportRemainsAvailable()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await client.GetAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Then(@"the report is published in the same action")]
	public async Task ThenPublishedInTheSameAction()
	{
		_result.GetProperty("status").GetString().ShouldBe("published");
		_result.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.String);
		(await AuditEntries(AuditAction.ApprovedReport)).Count.ShouldBe(1);
	}

	[Then(@"no Administrator, migration, background worker, or direct API caller can bypass any of these guards")]
	public async Task ThenNoCallerCanBypassTheGuards()
	{
		// An administrator approving an unconsented report gets Approved, never Published.
		var unconsented = await BootedReports.Seed(ReportStatus.PendingReview, "no");
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		var loaded = await admin.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{unconsented}", UriKind.Relative));
		using var approved = await admin.PostAsJsonAsync($"/api/admin/reports/{unconsented}/approve", new { version = loaded.GetProperty("version").GetString() });
		(await approved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().ShouldBe("approved");

		// No endpoint publishes directly, and nothing outside the review commands changes a status.
		var factory = await BootedApi.Factory();
		var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
			.ToList();
		routes.ShouldNotContain(route => route.Contains("publish", StringComparison.OrdinalIgnoreCase) && !route.EndsWith("/unpublish", StringComparison.Ordinal));
	}

	[Then(@"^the report becomes (Published|Approved)$")]
	public void ThenTheReportBecomes(string status)
	{
		ArgumentNullException.ThrowIfNull(status);
		_result.GetProperty("status").GetString().ShouldBe(status.ToLowerInvariant());
	}

	[Then(@"the approval and any publication are recorded in one audited action")]
	public async Task ThenOneAuditedAction()
	{
		(await AuditEntries(AuditAction.ApprovedReport)).Count.ShouldBe(1);
		(await AuditEntries(AuditAction.PublishedReport)).ShouldBeEmpty();
	}

	[Then(@"the report returns to Pending review")]
	public void ThenPendingReview()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
		_result.GetProperty("status").GetString().ShouldBe("pending_review");
	}

	[Then(@"the reopening is audited")]
	public async Task ThenReopeningIsAudited()
	{
		(await AuditEntries(AuditAction.ReopenedReport)).Count.ShouldBe(1);
	}

	[Then(@"it is no longer publishable")]
	public void ThenNoLongerPublishable()
	{
		_result.GetProperty("status").GetString().ShouldNotBe("published");
		_result.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"the pair's approval is cleared and the report returns to Pending review")]
	public void ThenApprovalClearedAndPending()
	{
		ThenApprovalIsCleared();
		ThenPendingReview();
	}

	[Then(@"the unpublishing is audited")]
	public async Task ThenUnpublishingIsAudited()
	{
		(await AuditEntries(AuditAction.UnpublishedReport)).Count.ShouldBe(1);
	}

	[Then(@"the detail view shows the note to reviewers")]
	public async Task ThenTheDetailShowsTheNote()
	{
		_result.GetProperty("rejectionNote").GetString().ShouldBe(Note);

		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		detail.GetProperty("rejectionNote").GetString().ShouldBe(Note);
	}

	[Then(@"the note never reaches the public API, the audit log, or the application logs")]
	public async Task ThenTheNoteStaysReviewerOnly()
	{
		foreach (var entry in await AuditEntries(AuditAction.RejectedReport))
		{
			(entry.Detail ?? string.Empty).ShouldNotContain(Note);
		}

		_logs!.Messages.ShouldNotContain(message => message.Contains(Note, StringComparison.Ordinal));

		// Outside /api/admin, a report route only accepts a submission: nothing public
		// reads a report back yet (#28), so nothing public can carry the note.
		var publicReportMethods = (await BootedApi.Factory()).Services.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Where(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty).Contains("reports", StringComparison.Ordinal)
				&& !(endpoint.RoutePattern.RawText ?? string.Empty).StartsWith("/api/admin/", StringComparison.Ordinal))
			.SelectMany(endpoint => endpoint.Metadata.OfType<HttpMethodMetadata>())
			.SelectMany(metadata => metadata.HttpMethods)
			.Distinct();
		publicReportMethods.ShouldBe(["POST"]);
	}

	[Then(@"rejecting without a note also succeeds")]
	public async Task ThenRejectingWithoutANoteSucceeds()
	{
		var other = await BootedReports.Seed(ReportStatus.PendingReview, "yes");
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var loaded = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{other}", UriKind.Relative));
		using var rejected = await client.PostAsJsonAsync($"/api/admin/reports/{other}/reject", new { version = loaded.GetProperty("version").GetString() });
		rejected.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await rejected.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("status").GetString().ShouldBe("rejected");
		body.GetProperty("rejectionNote").ValueKind.ShouldBe(JsonValueKind.Null);
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

	[Then(@"the entry records no summary text, answer, or rejection note")]
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
			? ReviewLifecycleSteps.In(ReportStatus.SummaryFailed, "yes")
			: ReviewLifecycleSteps.In(ReportStatus.PendingReview, "yes");

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
								   string consent)
	{
		_reportId = await BootedReports.Seed(status, consent);
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
///     Seeds one synthetic report in a given review state into the booted database,
///     through the domain's own transitions.
/// </summary>
internal static class BootedReports
{
	public const string PilotName = "Morgan Synthetic";

	public static async Task<string> Seed(ReportStatus status,
										  string consent)
	{
		var factory = await BootedApi.Factory();
		await ReportSubmissionEndpointSteps.ConsentRevisionId();

		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var now = DateTimeOffset.UtcNow;

		var consentQuestion = await database.Questions
			.Include(question => question.Revisions)
			.SingleAsync(question => question.Key == QuestionKey.ConsentPublish);
		var pilot = Question.Create($"pilot_{Guid.NewGuid():n}"[..24], QuestionType.ShortText, "Pilot name", "Nom du pilote", now, isPrivate: true);
		database.Questions.Add(pilot);

		var report = new Report(Locale.EnCa, now);
		report.Answer(consentQuestion, consent, now);
		report.Answer(pilot, PilotName, now);
		report.BeginSummarizing();

		if (status == ReportStatus.SummaryFailed)
		{
			report.FailSummarization("The AI chat provider was unavailable for this summarization attempt.");
		}
		else
		{
			report.AttachSummary(Summary.Generate(report.Id, "The pilot landed in a field.", "Le pilote s'est posé dans un champ.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
			report.AwaitReview();

			switch (status)
			{
				case ReportStatus.PendingReview:
					break;
				case ReportStatus.Published:
				case ReportStatus.Approved:
					report.ApprovePair("synthetic-approver", now);
					break;
				case ReportStatus.Rejected:
					report.RejectReview(null);
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
