using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The audit scenarios — ADR-0092: REQ-MOD-029, REQ-MOD-044, REQ-MOD-045, and
///     REQ-MOD-091, that signing out has nothing to audit. Detailed
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

	[When(@"the attempt is rejected")]
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
		_actions.ShouldBe([HttpStatusCode.OK, HttpStatusCode.Created]);
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

	[Then(@"the subject is stored as an opaque string that joins to no user record")]
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
