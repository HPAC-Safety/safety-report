using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Whether a date question allows future dates (ADR-0138): REQ-QB-154, the
///     setting defaults to false and reads as a JSON boolean; REQ-QB-155, only a
///     date question may set it; REQ-QB-156, it is a revision field, revised
///     while unanswered and forked once answered (ADR-0071); REQ-QB-157, the
///     migration leaves the seeded occurrence date refusing future dates; and
///     REQ-SUB-110, the public form view carries it.
/// </summary>
/// <remarks>
///     Every claim here is about what an endpoint stores or returns, so each goes
///     through the booted API. Every question is synthetic except the seeded
///     occurrence date, which REQ-QB-157 reads as the migrations left it.
/// </remarks>
[Binding]
public sealed class FutureDateSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions/", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);

	// The seeded "Tell us the date of the occurrence." question (docs/form-spec.md).
	private const string OccurrenceDateKey = "c923604b_0aab_4223_92bb_795d77535f57";

	// Parallel scenarios derive keys from wording; this keeps each one's own.
	private readonly string _run = Guid.NewGuid().ToString("N");
	private int _created;
	private HttpClient? _admin;
	private JsonElement _question;
	private JsonElement _revised;
	private JsonElement _replacement;
	private string? _answeredRevisionId;
	private readonly List<HttpResponseMessage> _refusals = [];
	private readonly Dictionary<bool, JsonElement> _bySetting = [];
	private JsonElement _publicQuestions;

	// --- REQ-QB-154: false unless an Administrator says so ---

	[Given(@"^an Administrator creates a date question through the API (without saying whether it allows future dates|allowing future dates)$")]
	public async Task GivenADateQuestionCreatedThroughTheApi(string saying)
	{
		_question = await Create("date", saying == "allowing future dates" ? true : null);
	}

	[Then(@"^the saved question reads allowFutureDates as the JSON boolean (true|false)$")]
	public async Task ThenTheSavedQuestionReadsTheSetting(string stored)
	{
		var expected = stored == "true" ? JsonValueKind.True : JsonValueKind.False;
		_question.GetProperty("allowFutureDates").ValueKind.ShouldBe(expected);

		// And the same, read back from the authoring list rather than the save's echo.
		var listed = (await _admin!.GetFromJsonAsync<JsonElement>(AdminQuestions)).EnumerateArray()
			.Single(question => question.GetProperty("id").GetString() == _question.GetProperty("id").GetString());
		listed.GetProperty("allowFutureDates").ValueKind.ShouldBe(expected);
	}

	// --- REQ-QB-155: only a date question ---

	[Given(@"an Administrator creates a short-text, time, or number question through the API")]
	public async Task GivenNonDateQuestions()
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
	}

	[When(@"they mark it as allowing future dates")]
	public async Task WhenTheyMarkItAsAllowingFutureDates()
	{
		foreach (var type in new[] { "short_text", "time", "number" })
		{
			_refusals.Add(await _admin!.PostAsJsonAsync(AdminQuestions, Request(type, true)));
		}
	}

	[Then(@"the API refuses to save each one")]
	public void ThenTheApiRefusesEach()
	{
		_refusals.Count.ShouldBe(3);
		_refusals.ShouldAllBe(response => response.StatusCode == HttpStatusCode.BadRequest);
	}

	// --- REQ-QB-156: a revision field ---

	[Given(@"a date question that does not allow future dates")]
	public async Task GivenADateQuestionThatDoesNotAllowFutureDates()
	{
		_question = await Create("date", false);
		_question.GetProperty("allowFutureDates").GetBoolean().ShouldBeFalse();
	}

	[When(@"an Administrator allows future dates while nobody has answered it")]
	public async Task WhenAllowedWhileUnanswered()
	{
		_revised = await Edit(_question, true);
	}

	[Then(@"a new revision of the same question allows future dates, and the earlier revision still does not")]
	public async Task ThenANewRevisionAllowsFutureDates()
	{
		_revised.GetProperty("id").GetString().ShouldBe(_question.GetProperty("id").GetString());
		_revised.GetProperty("revisionNumber").GetInt32().ShouldBe(2);
		_revised.GetProperty("allowFutureDates").GetBoolean().ShouldBeTrue();

		(await StoredRevision(_question.GetProperty("revisionId").GetString()!)).AllowFutureDates.ShouldBeFalse();
		(await StoredRevision(_revised.GetProperty("revisionId").GetString()!)).AllowFutureDates.ShouldBeTrue();
	}

	[When(@"a reporter answers it and an Administrator then disallows future dates")]
	public async Task WhenAnsweredThenDisallowed()
	{
		_answeredRevisionId = _revised.GetProperty("revisionId").GetString();

		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		using var response = await reporter.PostAsJsonAsync(Submit, new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (object?)false },
				// A date a year ahead, which only this revision accepts.
				new { questionRevisionId = _answeredRevisionId, value = (object?)$"{DateTimeOffset.UtcNow.Year + 1}-01-15" },
			},
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());

		_replacement = await Edit(_revised, false);
	}

	[Then(@"the question is retired and replaced under the same key, and the answer keeps the revision that allowed future dates")]
	public async Task ThenRetiredAndReplaced()
	{
		_replacement.GetProperty("id").GetString().ShouldNotBe(_question.GetProperty("id").GetString());
		_replacement.GetProperty("key").GetString().ShouldBe(_question.GetProperty("key").GetString());
		_replacement.GetProperty("allowFutureDates").GetBoolean().ShouldBeFalse();

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var retiredId = TinyId.Parse(_question.GetProperty("id").GetString());
		var answeredRevisionId = TinyId.Parse(_answeredRevisionId);

		(await database.Questions.IgnoreQueryFilters().AsNoTracking().SingleAsync(question => question.Id == retiredId))
			.Deleted.ShouldNotBeNull();

		var answer = await database.ReportAnswers.IgnoreQueryFilters().AsNoTracking()
			.SingleAsync(candidate => candidate.QuestionId == retiredId);
		answer.QuestionRevisionId.ShouldBe(answeredRevisionId);
		(await StoredRevision(_answeredRevisionId!)).AllowFutureDates.ShouldBeTrue();
	}

	// --- REQ-QB-157: the migration leaves the occurrence date refusing future dates ---

	[Given(@"the migrations have been applied")]
	public async Task GivenTheMigrationsHaveBeenApplied()
	{
		// Booting the API applies every migration (EnsureMigrated).
		await BootedApi.Factory();
	}

	[Then(@"the seeded occurrence-date question {string} does not allow future dates")]
	public async Task ThenTheOccurrenceDateDoesNotAllowFutureDates(string help)
	{
		var revision = await OccurrenceDateRevision();
		revision.HelpTextEn.ShouldBe(help);
		revision.AllowFutureDates.ShouldBeFalse();
	}

	[Then(@"it is still the revision it was seeded as")]
	public async Task ThenItIsStillTheSeededRevision()
	{
		var revision = await OccurrenceDateRevision();
		revision.Id.ShouldBe(SeedIds.For($"question_version:{OccurrenceDateKey}:1"));
		revision.RevisionNumber.ShouldBe(1);
		revision.CreatedAt.ShouldBe(QuestionBankSeed.SeededAt);
	}

	// --- REQ-SUB-110: the public form view carries the setting ---

	[Given(@"a live date question that allows future dates and one that does not")]
	public async Task GivenTwoLiveDateQuestions()
	{
		_bySetting[true] = await Create("date", true);
		_bySetting[false] = await Create("date", false);
	}

	[When(@"the reporter's form reads the current questions")]
	public async Task WhenTheFormReadsTheCurrentQuestions()
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		_publicQuestions = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
	}

	[Then(@"each carries allowFutureDates as the JSON boolean matching its setting")]
	public void ThenEachCarriesTheSetting()
	{
		foreach (var (allows, created) in _bySetting)
		{
			var shown = _publicQuestions.EnumerateArray()
				.Single(question => question.GetProperty("id").GetString() == created.GetProperty("id").GetString());
			shown.GetProperty("allowFutureDates").ValueKind.ShouldBe(allows ? JsonValueKind.True : JsonValueKind.False);
		}
	}

	// --- helpers ---

	private async Task<JsonElement> Create(string type,
										   bool? allowFutureDates)
	{
		_admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		using var response = await _admin.PostAsJsonAsync(AdminQuestions, Request(type, allowFutureDates));
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<JsonElement> Edit(JsonElement question,
										 bool allowFutureDates)
	{
		var body = Request(question.GetProperty("type").GetString()!, allowFutureDates);
		body["labelEn"] = question.GetProperty("labelEn").GetString();
		body["labelFr"] = question.GetProperty("labelFr").GetString();
		using var response = await _admin!.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{question.GetProperty("id").GetString()}", UriKind.Relative), body);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private Dictionary<string, object?> Request(string type,
												bool? allowFutureDates)
	{
		// Each new question needs wording of its own, since its key derives from it;
		// an edit keeps the wording it was given.
		var run = $"{_run} {_created++}";
		var request = new Dictionary<string, object?>
		{
			["key"] = null,
			["type"] = type,
			["labelEn"] = $"A synthetic {type} question {run}",
			["labelFr"] = $"Une question synthétique {type} {run}",
			["isRequired"] = false,
			["isPrivate"] = false,
			["isActive"] = true,
			["options"] = Array.Empty<object>(),
		};

		// Left out entirely, not sent as null, for "without saying".
		if (allowFutureDates is { } allows)
		{
			request["allowFutureDates"] = allows;
		}

		return request;
	}

	private static async Task<Core.Features.QuestionBank.QuestionRevision> StoredRevision(string revisionId)
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(revisionId);
		return await database.QuestionRevisions.IgnoreQueryFilters().AsNoTracking().SingleAsync(revision => revision.Id == id);
	}

	private static async Task<Core.Features.QuestionBank.QuestionRevision> OccurrenceDateRevision()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var questionId = SeedIds.For($"question:{OccurrenceDateKey}");
		return await database.QuestionRevisions.AsNoTracking()
			.Where(revision => revision.QuestionId == questionId)
			.OrderByDescending(revision => revision.RevisionNumber)
			.FirstAsync();
	}
}
