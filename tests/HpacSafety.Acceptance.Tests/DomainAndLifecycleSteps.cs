using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Soft deletion and retention, against the booted host — REQ-DOM-007,
///     REQ-DOM-009, REQ-DOM-010. Detailed coverage — every owned row, the Worker's
///     own mid-flight recheck — lives in <c>HpacSafety.Api.Tests</c> and
///     <c>HpacSafety.Worker.Tests</c>; these prove the feature file's sentences are
///     true of the running system.
/// </summary>
[Binding]
public sealed class DomainAndLifecycleSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly DateTimeOffset At = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	private TinyId _reportId;
	private TinyId _answerId;
	private TinyId _outboxId;
	private TinyId _questionId;
	private TinyId _revisionId;
	private TinyId _referencedQuestionId;
	private TinyId _referencedRevisionId;
	private HttpResponseMessage? _response;
	private HttpResponseMessage? _secondResponse;

	[Given(@"a report exists in any lifecycle state")]
	public async Task GivenAReportExists()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var question = Question.Create(
			$"synthetic_{Guid.NewGuid():n}"[..24], QuestionType.ShortText, "A synthetic question", "Une question synthétique", At);
		database.Questions.Add(question);

		var report = new Report(Locale.EnCa, At);
		var answer = report.Answer(question, "A synthetic answer.", At);
		database.Reports.Add(report);

		var outboxMessage = new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, At);
		database.OutboxMessages.Add(outboxMessage);

		await database.SaveChangesAsync();

		_reportId = report.Id;
		_answerId = answer.Id;
		_outboxId = outboxMessage.Id;
	}

	[When(@"a safety officer soft-deletes it")]
	public async Task WhenASafetyOfficerSoftDeletesIt()
	{
		var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		_response = await client.DeleteAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Then(@"one application transaction stamps the same deleted timestamp on the report and all owned and dependent rows: answers, summary, files, and report outbox items")]
	public async Task ThenOneTransactionStampsEveryOwnedRow()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var report = await database.Reports.IgnoreQueryFilters().SingleAsync(r => r.Id == _reportId);
		var answer = await database.ReportAnswers.IgnoreQueryFilters().SingleAsync(a => a.Id == _answerId);
		var outboxMessage = await database.OutboxMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == _outboxId);

		report.Deleted.ShouldNotBeNull();
		answer.Deleted.ShouldBe(report.Deleted);
		outboxMessage.Deleted.ShouldBe(report.Deleted);
	}

	[Then(@"an immutable audit entry is recorded")]
	public async Task ThenAnAuditEntryIsRecorded()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var entry = await database.AuditLog
			.SingleAsync(e => e.Action == AuditAction.DeletedReport && e.TargetId == _reportId);
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
	}

	[Then(@"pending Worker work for the report stops, and the Worker rechecks deletion before committing output")]
	public async Task ThenPendingWorkStops()
	{
		// The Worker's own mid-flight recheck is proven directly in
		// HpacSafety.Worker.Tests; here, what an HTTP-level scenario can prove is
		// that the outbox row a future Worker claim would find is gone.
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		(await database.OutboxMessages.AnyAsync(m => m.Id == _outboxId)).ShouldBeFalse();
	}

	[Then(@"public and normal admin queries hide the report immediately")]
	public async Task ThenNormalQueriesHideTheReport()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		(await database.Reports.AnyAsync(r => r.Id == _reportId)).ShouldBeFalse();

		var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var secondDelete = await client.DeleteAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		secondDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"there is no restore transition")]
	public async Task ThenThereIsNoRestoreTransition()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var routes = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
			.ToArray();

		string[] forbidden = ["restore", "undelete", "un-delete"];
		routes.ShouldNotContain(pattern => forbidden.Any(p => pattern.Contains(p, StringComparison.OrdinalIgnoreCase)));
		await Task.CompletedTask;
	}

	[Given(@"a question is retired, either by an Administrator or by being replaced through an edit")]
	public async Task GivenAQuestionIsRetired()
	{
		var client = await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"retired_{Guid.NewGuid():n}"[..24];

		using var created = await client.PostAsJsonAsync(
			new Uri("/api/admin/questions", UriKind.Relative),
			new
			{
				key,
				type = "short_text",
				labelEn = "A synthetic question",
				labelFr = "Une question synthétique",
				isRequired = false,
				isPrivate = true,
				isActive = true
			});
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await created.Content.ReadFromJsonAsync<JsonElement>();
		_questionId = TinyId.Parse(body.GetProperty("id").GetString()!);
		_revisionId = TinyId.Parse(body.GetProperty("revisionId").GetString()!);

		_response = await client.DeleteAsync(new Uri($"/api/admin/questions/{_questionId}", UriKind.Relative));
	}

	[When(@"the deletion is committed")]
	public void WhenTheDeletionIsCommitted()
	{
		// The Given already made the call; this step is the sentence's grammar.
		_response.ShouldNotBeNull();
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Then(@"the question is stamped with a deleted timestamp rather than removed")]
	public async Task ThenTheQuestionIsStampedDeleted()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var question = await database.Questions.IgnoreQueryFilters().SingleAsync(q => q.Id == _questionId);
		question.Deleted.ShouldNotBeNull();
	}

	[Then(@"its revisions, choices, and every answer given to it are untouched")]
	public async Task ThenItsRevisionsAreUntouched()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var revision = await database.QuestionRevisions.IgnoreQueryFilters().SingleAsync(r => r.Id == _revisionId);
		revision.Deleted.ShouldBeNull();
	}

	[Given(@"a question revision is referenced by no answer, including answers on deleted reports")]
	public async Task GivenAQuestionRevisionIsReferencedByNoAnswer()
	{
		var client = await BootedApi.SignedInAs(MemberRole.Administrator);

		// Question A: revision 1 is superseded by an edit nobody answered, so it
		// is history with nothing pointing at it — the one the scenario deletes.
		var (questionAId, revision1Id) = await CreateSingleSelectQuestion(client, "unreferenced");
		await ReviseSingleSelectQuestion(client, questionAId);
		_questionId = questionAId;
		_revisionId = revision1Id;

		// Question B: revision 1 is answered, and that answer's report is then
		// soft-deleted — proving the reference check is not fooled by the
		// report's own deletion.
		var (questionBId, revision1BId) = await CreateSingleSelectQuestion(client, "referenced");
		await ReviseSingleSelectQuestion(client, questionBId);
		_referencedQuestionId = questionBId;
		_referencedRevisionId = revision1BId;

		var host = await BootedApi.Factory();
		using (var scope = host.Services.CreateScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var questionB = await database.Questions
				.Include(q => q.Revisions)
				.Include(q => q.AllChoices)
				.SingleAsync(q => q.Id == questionBId);
			var oldRevision = questionB.Revisions.Single(r => r.Id == revision1BId);

			var report = new Report(Locale.EnCa, At);
			report.Answer(questionB, oldRevision, "North", At);
			database.Reports.Add(report);
			await database.SaveChangesAsync();

			_reportId = report.Id;
		}

		var deletingClient = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var deleted = await deletingClient.DeleteAsync(new Uri($"/api/admin/reports/{_reportId}", UriKind.Relative));
		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[When(@"an Administrator deletes that revision")]
	public async Task WhenAnAdministratorDeletesThatRevision()
	{
		var client = await BootedApi.SignedInAs(MemberRole.Administrator);
		_response = await client.DeleteAsync(
			new Uri($"/api/admin/questions/{_questionId}/revisions/{_revisionId}", UriKind.Relative));
		_response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Then(@"the revision is stamped with a deleted timestamp")]
	public async Task ThenTheRevisionIsStamped()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var revision = await database.QuestionRevisions
			.IgnoreQueryFilters()
			.SingleAsync(r => r.Id == _revisionId);

		revision.Deleted.ShouldNotBeNull();
	}

	[Then(@"once any answer references a revision, that revision is never deletable again")]
	public async Task ThenAReferencedRevisionIsNeverDeletableAgain()
	{
		var client = await BootedApi.SignedInAs(MemberRole.Administrator);
		_secondResponse = await client.DeleteAsync(
			new Uri($"/api/admin/questions/{_referencedQuestionId}/revisions/{_referencedRevisionId}", UriKind.Relative));
		_secondResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var revision = await database.QuestionRevisions.IgnoreQueryFilters().SingleAsync(r => r.Id == _referencedRevisionId);
		revision.Deleted.ShouldBeNull();
	}

	private static async Task<(TinyId QuestionId, TinyId RevisionId)> CreateSingleSelectQuestion(
		HttpClient client, string label)
	{
		var key = $"{label}_{Guid.NewGuid():n}"[..24];

		using var created = await client.PostAsJsonAsync(
			new Uri("/api/admin/questions", UriKind.Relative),
			new
			{
				key,
				type = "single_select",
				labelEn = $"Which direction, {label}?",
				labelFr = $"Quelle direction, {label} ?",
				isRequired = false,
				isPrivate = false,
				isActive = true,
				options = new[]
				{
					new { code = "north", labelEn = "North", labelFr = "Nord" },
					new { code = "south", labelEn = "South", labelFr = "Sud" },
				}
			});
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await created.Content.ReadFromJsonAsync<JsonElement>();

		return (
			TinyId.Parse(body.GetProperty("id").GetString()!),
			TinyId.Parse(body.GetProperty("revisionId").GetString()!));
	}

	/// <summary>Edits the question while nothing has answered it, which revises it in place (ADR-0071).</summary>
	private static async Task ReviseSingleSelectQuestion(HttpClient client, TinyId questionId)
	{
		using var revised = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{questionId}", UriKind.Relative),
			new
			{
				type = "single_select",
				labelEn = "Which direction, revised?",
				labelFr = "Quelle direction, révisée ?",
				isRequired = false,
				isPrivate = false,
				isActive = true,
				options = new[]
				{
					new { code = "north", labelEn = "North", labelFr = "Nord" },
					new { code = "south", labelEn = "South", labelFr = "Sud" },
				}
			});
		revised.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Given(@"a synthetic report has been submitted")]
	public async Task GivenASyntheticReportHasBeenSubmitted()
	{
		// Its own seeding rather than ReportSubmissionEndpointSteps' shared
		// "Given a report has been submitted" — that step posts a literal,
		// globally-queried "Blue" answer several other scenarios also depend on
		// being the only one in the translation queue at a time; reusing it here
		// would race those scenarios under xUnit's cross-class parallelism.
		await GivenAReportExists();
	}

	[When(@"no safety officer has deleted it")]
	public void WhenNoSafetyOfficerHasDeletedIt()
	{
		// Nothing to do — the Given already left the report undeleted.
	}

	[Then(@"the report is retained indefinitely")]
	public async Task ThenTheReportIsRetainedIndefinitely()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		(await database.Reports.AnyAsync(r => r.Id == _reportId)).ShouldBeTrue();
	}

	[Then(@"there is no scheduled report purge and no physical-delete path in the application")]
	public async Task ThenThereIsNoPurgeOrPhysicalDeletePath()
	{
		var host = await BootedApi.Factory();
		using var scope = host.Services.CreateScope();
		var routes = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
			.ToArray();

		string[] forbidden = ["purge"];
		routes.ShouldNotContain(pattern => forbidden.Any(p => pattern.Contains(p, StringComparison.OrdinalIgnoreCase)));
		await Task.CompletedTask;
	}
}
