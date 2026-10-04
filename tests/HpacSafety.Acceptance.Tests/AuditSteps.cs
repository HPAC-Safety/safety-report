using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The audit scenarios — ADR-0092: REQ-MOD-029, REQ-MOD-044, REQ-MOD-045, and
///     REQ-MOD-091, that signing out has nothing to audit; REQ-DOM-013, that every
///     audited action is recorded; and REQ-MOD-047, that a failed audit write
///     blocks the action. Detailed
///     coverage of every audited action lives in <c>HpacSafety.Api.Tests</c>; this
///     proves the feature file's sentences are true against the booted host.
/// </summary>
[Binding]
public sealed class AuditSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly string[] SignOutWords = ["logout", "logoff", "signout", "signedout", "revoke"];

	private readonly string _subject = $"opaque-{Guid.NewGuid():N}";
	private readonly string _marker = $"synthetic-narrative-{Guid.NewGuid():N}";
	private readonly List<HttpStatusCode> _actions = [];
	private HttpResponseMessage? _response;
	private string? _attemptedUsername;
	private string? _reportId;
	private string? _questionId;
	private DateTimeOffset _started;
	private DateTimeOffset _finished;
	private List<string> _routes = [];
	private readonly List<string> _secrets = [];
	private readonly List<bool> _blocked = [];
	private AuditAction _expectedAction;
	private string _targetType = string.Empty;
	private string _targetId = string.Empty;
	private string _version = string.Empty;
	private string _failedQuestionLabel = string.Empty;
	private bool _triggerInstalled;

	[Given(@"a member signs in with valid credentials")]
	public async Task GivenValidCredentials()
	{
		var host = await BootedApi.Factory();
		using var client = host.CreateClient();
		_response = await client.PostAsJsonAsync("/api/auth/token", new { username = "admin", password = "admin" });
	}

	[When(@"the sign-in succeeds")]
	public void WhenSignInSucceeds()
	{
		_response!.IsSuccessStatusCode.ShouldBeTrue();
	}

	[Then(@"an audit entry records the token subject, a sign-in-succeeded action, and the time")]
	public async Task ThenAuditRecordsSuccess()
	{
		var entry = await Latest(AuditAction.SignedInSucceeded);
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		entry.OccurredAt.ShouldNotBe(default);
	}

	[Then(@"it never records the credentials")]
	public async Task ThenNoCredentialsRecorded()
	{
		var entry = await Latest(AuditAction.SignedInSucceeded);
		entry.Detail.ShouldBeNull();
	}

	[Given(@"a sign-in attempt uses credentials that are not valid")]
	public async Task GivenInvalidCredentials()
	{
		var host = await BootedApi.Factory();
		using var client = host.CreateClient();
		_attemptedUsername = $"nobody_{Guid.NewGuid():n}"[..24];
		_response = await client.PostAsJsonAsync(
			"/api/auth/token", new { username = _attemptedUsername, password = "not-the-real-password" });
	}

	[When(@"the attempt is refused")]
	public void WhenAttemptIsRejected()
	{
		_response!.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Then(@"an audit entry records a sign-in-failed action and the time")]
	public async Task ThenAuditRecordsFailure()
	{
		var entry = await Latest(AuditAction.SignedInFailed);
		entry.OccurredAt.ShouldNotBe(default);
	}

	[Then(@"it never records the attempted credentials")]
	public async Task ThenNoAttemptedCredentialsRecorded()
	{
		var entry = await Latest(AuditAction.SignedInFailed);
		entry.Detail.ShouldBeNull();
	}

	[Then(@"the actor is recorded as the attempted identity rather than left blank")]
	public async Task ThenActorIsAttemptedIdentity()
	{
		var entry = await Latest(AuditAction.SignedInFailed);
		entry.ActorSubject.ShouldBe(_attemptedUsername);
	}

	// --- REQ-MOD-029: sensitive admin actions are audited without report content ---

	[Given(@"a sensitive read or material mutation occurs in the admin application")]
	public async Task GivenASensitiveReadAndAMutation()
	{
		var host = await BootedApi.Factory();
		_reportId = await SubmitReportCarrying(_marker);

		// A subject of its own, so this scenario's rows are the only ones it reads.
		using var admin = BootedApi.SignedInAsMember(host, _subject, "administrator");
		_started = DateTimeOffset.UtcNow;

		using var read = await admin.GetAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		(await read.Content.ReadAsStringAsync()).ShouldContain(_marker, customMessage: "the read returned the report's content");
		_actions.Add(read.StatusCode);

		using var mutation = await admin.PostAsJsonAsync("/api/admin/questions", new
		{
			key = (string?)null,
			type = "short_text",
			labelEn = $"An audited synthetic question {Guid.NewGuid():N}",
			labelFr = "Une question synthétique auditée",
			isRequired = false,
			isPrivate = false,
			isActive = true,
		});
		_actions.Add(mutation.StatusCode);
		_questionId = (await mutation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
	}

	[When(@"the action completes")]
	public void WhenTheActionCompletes()
	{
		_actions.ShouldNotBeEmpty();
		_actions.ShouldAllBe(status => (int)status >= 200 && (int)status < 300);
		_finished = DateTimeOffset.UtcNow;
	}

	[Then(@"an audit entry records the acting token subject, action, target, and time")]
	public async Task ThenAnEntryRecordsSubjectActionTargetAndTime()
	{
		var entries = await EntriesBy(_subject);

		entries.ShouldContain(entry => entry.Action == AuditAction.ViewedRawReport
									   && entry.TargetType == "Report"
									   && entry.TargetId.Value == _reportId);
		entries.ShouldContain(entry => entry.Action == AuditAction.CreatedQuestion
									   && entry.TargetType == "Question"
									   && entry.TargetId.Value == _questionId);
		entries.ShouldAllBe(entry => entry.OccurredAt >= _started.AddSeconds(-1) && entry.OccurredAt <= _finished.AddSeconds(1));
	}

	[Then(@"the token subject is stored as an opaque string that joins to no user record")]
	[Then(@"the token subject is an opaque string that joins to no user record")]
	public async Task ThenTheSubjectJoinsToNothing()
	{
		(await EntriesBy(_subject)).ShouldNotBeEmpty();

		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var model = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>().Model;

		var audit = model.FindEntityType(typeof(AuditLogEntry))!;
		audit.FindProperty(nameof(AuditLogEntry.ActorSubject))!.ClrType.ShouldBe(typeof(string));
		audit.GetForeignKeys().ShouldBeEmpty();

		// Nothing for it to join to: no user, member, or account table (ADR-0065).
		model.GetEntityTypes()
			.Select(entity => entity.GetTableName() ?? string.Empty)
			.ShouldNotContain(table => table.Contains("user", StringComparison.OrdinalIgnoreCase)
									   || table.Contains("member", StringComparison.OrdinalIgnoreCase)
									   || table.Contains("account", StringComparison.OrdinalIgnoreCase));
	}

	[Then(@"it never records report content")]
	public async Task ThenItNeverRecordsReportContent()
	{
		foreach (var entry in await EntriesBy(_subject))
		{
			(entry.Detail ?? string.Empty).ShouldNotContain(_marker);
			entry.TargetType.ShouldNotContain(_marker);
		}
	}

	// --- REQ-DOM-013: every audited action is recorded ---

	[Given(@"^(a question is created|a question is revised|a question is deleted|a question revision is deleted|a report is deleted|a summary is edited|a summary is rolled back|a report is published|a report is unpublished) occurs$")]
	public async Task GivenAnAuditedActionOccurs(string action)
	{
		ArgumentNullException.ThrowIfNull(action);

		var host = await BootedApi.Factory();
		_started = DateTimeOffset.UtcNow;

		if (action.StartsWith("a question", StringComparison.Ordinal))
		{
			using var admin = BootedApi.SignedInAsMember(host, _subject, "administrator");
			RememberSecrets(admin);
			await PerformQuestionAction(admin, action);
		}
		else
		{
			using var officer = BootedApi.SignedInAsMember(host, _subject, "safety_officer");
			RememberSecrets(officer);
			await PerformReportAction(officer, action);
		}
	}

	[Then(@"an audit log entry records the acting token subject and action metadata")]
	public async Task ThenAnEntryRecordsSubjectAndMetadata()
	{
		var matching = (await EntriesBy(_subject))
			.Where(entry => entry.Action == _expectedAction && entry.TargetId.Value == _targetId)
			.ToList();

		var entry = matching.ShouldHaveSingleItem();
		entry.ActorSubject.ShouldBe(_subject);
		entry.TargetType.ShouldBe(_targetType);
		entry.OccurredAt.ShouldBeInRange(_started.AddSeconds(-1), _finished.AddSeconds(1));
	}

	[Then(@"it never contains raw answers, names, credentials, tokens, or client filenames")]
	public async Task ThenNoRawContentIsRecorded()
	{
		var entries = await EntriesBy(_subject);
		entries.ShouldNotBeEmpty();

		_secrets.ShouldNotBeEmpty();
		foreach (var entry in entries)
		{
			var recorded = $"{entry.TargetType} {entry.Detail}";
			foreach (var secret in _secrets)
			{
				recorded.ShouldNotContain(secret);
			}
		}
	}

	// --- REQ-MOD-047: a failed audit write blocks the action ---

	[Given(@"an Administrator or reviewer performs an action that must be audited")]
	public async Task GivenAnActionThatMustBeAudited()
	{
		var host = await BootedApi.Factory();
		_reportId = await BootedReports.Seed(ReportStatus.Pending, true);

		using var officer = BootedApi.SignedInAsMember(host, _subject, "safety_officer");
		var detail = await officer.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		_version = detail.GetProperty("version").GetString()!;
	}

	[When(@"the audit row fails to write")]
	public async Task WhenTheAuditRowFailsToWrite()
	{
		var host = await BootedApi.Factory();

		// A database-side refusal of this actor's audit rows only, so the audit
		// insert fails for real inside the same SaveChanges as the action.
		await Sql(host, $"""
			CREATE FUNCTION {TriggerName}() RETURNS trigger AS $body$
			BEGIN
				RAISE EXCEPTION 'synthetic audit write failure';
			END;
			$body$ LANGUAGE plpgsql;
			""");
		await Sql(host, $"""
			CREATE TRIGGER {TriggerName} BEFORE INSERT ON audit_log
				FOR EACH ROW WHEN (NEW.actor_subject = '{_subject}')
				EXECUTE FUNCTION {TriggerName}();
			""");
		_triggerInstalled = true;

		using var admin = BootedApi.SignedInAsMember(host, _subject, "administrator");
		_failedQuestionLabel = $"A question whose audit failed {Guid.NewGuid():N}";
		_blocked.Add(await Attempt(() => admin.PostAsJsonAsync("/api/admin/questions", new
		{
			key = (string?)null,
			type = "short_text",
			labelEn = _failedQuestionLabel,
			labelFr = "Une question dont la trace a échoué",
			isRequired = false,
			isPrivate = false,
			isActive = true,
		})));

		using var officer = BootedApi.SignedInAsMember(host, _subject, "safety_officer");
		_blocked.Add(await Attempt(() => officer.PostAsJsonAsync($"/api/admin/reports/{_reportId}/publish", new { version = _version })));
	}

	[Then(@"the action itself does not commit")]
	public async Task ThenTheActionDoesNotCommit()
	{
		var host = await BootedApi.Factory();
		using var admin = BootedApi.SignedInAsMember(host, _subject, "administrator");
		var questions = await admin.GetFromJsonAsync<JsonElement>(new Uri("/api/admin/questions", UriKind.Relative));
		questions.EnumerateArray()
			.ShouldNotContain(question => question.GetProperty("labelEn").GetString() == _failedQuestionLabel);

		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = await database.Reports.AsNoTracking().SingleAsync(candidate => candidate.Id == TinyId.Parse(_reportId!));
		report.Status.ShouldBe(ReportStatus.Pending);

		(await EntriesBy(_subject)).ShouldNotContain(entry => entry.Action == AuditAction.CreatedQuestion || entry.Action == AuditAction.PublishedReport);
	}

	[Then(@"the member sees the action as failed, not succeeded")]
	public void ThenTheCallerSeesFailure()
	{
		_blocked.Count.ShouldBe(2);
		_blocked.ShouldAllBe(failed => failed);
	}

	[AfterScenario]
	public async Task RemoveTheFailingTrigger()
	{
		if (!_triggerInstalled)
		{
			return;
		}

		var host = await BootedApi.Factory();
		await Sql(host, $"DROP TRIGGER IF EXISTS {TriggerName} ON audit_log");
		await Sql(host, $"DROP FUNCTION IF EXISTS {TriggerName}()");
	}

	private string TriggerName => $"refuse_audit_{_subject.Replace("-", "_", StringComparison.Ordinal)}"[..40];

	/// <summary>True when the call failed: a server error answered, or the host surfaced the exception.</summary>
	private static async Task<bool> Attempt(Func<Task<HttpResponseMessage>> call)
	{
		try
		{
			using var response = await call();
			return !response.IsSuccessStatusCode;
		}
		catch (Exception)
		{
			// The test server hands an unhandled host exception to the caller.
			return true;
		}
	}

	private static async Task Sql(WebApplicationFactory<Program> host,
								  string statement)
	{
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		await database.Database.ExecuteSqlRawAsync(statement);
	}

	private void RememberSecrets(HttpClient client)
	{
		_secrets.Add(client.DefaultRequestHeaders.Authorization!.Parameter!);
		_secrets.Add(BootedReports.PilotName);
		_secrets.Add("launch-site.jpg");
	}

	private async Task PerformQuestionAction(HttpClient admin,
											 string action)
	{
		var label = $"An audited synthetic question {Guid.NewGuid():N}";
		_secrets.Add(label);

		var created = await Send(admin.PostAsJsonAsync("/api/admin/questions", QuestionBody(label)), HttpStatusCode.Created);
		var questionId = created.GetProperty("id").GetString()!;
		var firstRevisionId = created.GetProperty("revisionId").GetString()!;
		var question = new Uri($"/api/admin/questions/{questionId}", UriKind.Relative);

		(_expectedAction, _targetType, _targetId) = (AuditAction.CreatedQuestion, "Question", questionId);

		if (action == "a question is created")
		{
			_actions.Add(HttpStatusCode.Created);
			return;
		}

		if (action == "a question is deleted")
		{
			using var deleted = await admin.DeleteAsync(question);
			_actions.Add(deleted.StatusCode);
			_expectedAction = AuditAction.DeletedQuestion;
			return;
		}

		var revisedLabel = $"{label} reworded";
		_secrets.Add(revisedLabel);
		using var revised = await admin.PutAsJsonAsync(question, QuestionBody(revisedLabel));
		_actions.Add(revised.StatusCode);
		_expectedAction = AuditAction.RevisedQuestion;

		if (action == "a question revision is deleted")
		{
			using var deleted = await admin.DeleteAsync(new Uri($"/api/admin/questions/{questionId}/revisions/{firstRevisionId}", UriKind.Relative));
			_actions.Add(deleted.StatusCode);
			(_expectedAction, _targetType, _targetId) = (AuditAction.DeletedQuestionRevision, "QuestionRevision", firstRevisionId);
		}
	}

	private async Task PerformReportAction(HttpClient officer,
										   string action)
	{
		var status = action is "a report is unpublished" ? ReportStatus.Published : ReportStatus.Pending;
		_reportId = await BootedReports.Seed(status, true);
		_secrets.Add("The pilot landed in a field.");
		_targetType = "Report";
		_targetId = _reportId;

		var detail = await officer.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		var version = detail.GetProperty("version").GetString();
		var report = $"/api/admin/reports/{_reportId}";

		switch (action)
		{
			case "a report is deleted":
				using (var deleted = await officer.DeleteAsync(new Uri(report, UriKind.Relative)))
				{
					_actions.Add(deleted.StatusCode);
				}

				_expectedAction = AuditAction.DeletedReport;
				break;
			case "a report is published":
				_actions.Add(await Post(officer, $"{report}/publish", new { version }));
				_expectedAction = AuditAction.PublishedReport;
				break;
			case "a report is unpublished":
				_actions.Add(await Post(officer, $"{report}/unpublish", new { version }));
				_expectedAction = AuditAction.UnpublishedReport;
				break;
			default:
				_secrets.Add(EditedEn);
				_secrets.Add(EditedFr);
				using (var edited = await officer.PutAsJsonAsync(new Uri($"{report}/summary", UriKind.Relative), new { version, aiSummaryEn = EditedEn, aiSummaryFr = EditedFr }))
				{
					_actions.Add(edited.StatusCode);
					var after = await edited.Content.ReadFromJsonAsync<JsonElement>();
					_expectedAction = AuditAction.EditedSummary;

					if (action == "a summary is rolled back")
					{
						var earlier = after.GetProperty("summaryRevisions")[1].GetProperty("id").GetString()!;
						_actions.Add(await Post(officer, $"{report}/summary/revisions/{earlier}/rollback", new { version = after.GetProperty("version").GetString() }));
						_expectedAction = AuditAction.RolledBackSummary;
					}
				}

				break;
		}
	}

	private const string EditedEn = "Synthetic edited summary for the audit scenario.";
	private const string EditedFr = "Résumé synthétique modifié pour le scénario d'audit.";

	private static object QuestionBody(string label)
	{
		return new
		{
			key = (string?)null,
			type = "short_text",
			labelEn = label,
			labelFr = "Une question synthétique auditée",
			isRequired = false,
			isPrivate = false,
			isActive = true,
		};
	}

	private static async Task<JsonElement> Send(Task<HttpResponseMessage> call,
												HttpStatusCode expected)
	{
		using var response = await call;
		response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private static async Task<HttpStatusCode> Post(HttpClient client,
												   string uri,
												   object body)
	{
		using var response = await client.PostAsJsonAsync(new Uri(uri, UriKind.Relative), body);
		return response.StatusCode;
	}

	// --- REQ-MOD-091: sign-out is not an audited event ---

	[Given(@"the API's mapped routes")]
	public async Task GivenTheMappedRoutes()
	{
		var host = await BootedApi.Factory();

		_routes =
		[
			.. host.Services.GetServices<EndpointDataSource>()
				.SelectMany(source => source.Endpoints)
				.OfType<RouteEndpoint>()
				.Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty),
		];

		_routes.ShouldContain("/api/auth/token", customMessage: "the routes were read from the running host");
	}

	[Then(@"none of them signs a member out")]
	public void ThenNoneSignsAMemberOut()
	{
		_routes.ShouldNotContain(route => SignOutWords.Any(word => route.Replace("-", string.Empty, StringComparison.Ordinal)
			.Contains(word, StringComparison.OrdinalIgnoreCase)));
	}

	[Then(@"no audit action records a sign-out")]
	public void ThenNoAuditActionRecordsASignOut()
	{
		Enum.GetNames<AuditAction>()
			.ShouldNotContain(name => SignOutWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));
	}

	private static async Task<List<AuditLogEntry>> EntriesBy(string subject)
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		return await database.AuditLog.Where(entry => entry.ActorSubject == subject).ToListAsync();
	}

	/// <summary>Files a report whose one ordinary answer is <paramref name="marker" />, and returns its id.</summary>
	private static async Task<string> SubmitReportCarrying(string marker)
	{
		var host = await BootedApi.Factory();
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		using var created = await admin.PostAsJsonAsync("/api/admin/questions", new
		{
			key = (string?)null,
			type = "short_text",
			labelEn = $"A synthetic narrative question {Guid.NewGuid():N}",
			labelFr = "Une question narrative synthétique",
			isRequired = false,
			isPrivate = false,
			isActive = true,
		});
		created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
		var revisionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionId").GetString();

		using var reporter = BootedApi.SignedInAsMember(host, $"reporter-{Guid.NewGuid():N}");
		using var submitted = await reporter.PostAsJsonAsync("/api/v1/reports", new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (bool?)false },
				new { questionRevisionId = revisionId, value = (string?)marker },
			},
		});
		submitted.StatusCode.ShouldBe(HttpStatusCode.Accepted, await submitted.Content.ReadAsStringAsync());

		return (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
	}

	private static async Task<AuditLogEntry> Latest(AuditAction action)
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		return await database.AuditLog
			.Where(entry => entry.Action == action)
			.OrderByDescending(entry => entry.OccurredAt)
			.FirstAsync();
	}
}
