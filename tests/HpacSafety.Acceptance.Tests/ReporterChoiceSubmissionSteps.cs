using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The scenario that follows a reporter's type-ahead value from the
///     submission all the way into the question's own choices the next reporter
///     reads — over HTTP, against the booted host, because it is the submission
///     path that calls <c>Question.AddChoiceFromReporter</c> (ADR-0063, ADR-0095). What that domain
///     operation decides on its own is covered by <see cref="ReporterAddedChoiceSteps" />.
/// </summary>
[Binding]
public sealed class ReporterChoiceSubmissionSteps
{
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private HttpClient? _admin;
	private TypeAhead? _typeAhead;
	private HttpResponseMessage? _response;
	private string? _typed;

	[Given(@"a published form has a type-ahead question")]
	public async Task GivenAPublishedTypeAhead()
	{
		_admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		_typeAhead = await CreateTypeAheadQuestion(_admin);
	}

	[When(@"a reporter answering in French submits a report naming ""(.*)"" in it")]
	public async Task WhenAFrenchReporterSubmits(string typed)
	{
		_typed = typed;
		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		var dto = new
		{
			language = "fr-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (bool?)true },
				new { questionRevisionId = _typeAhead!.RevisionId, value = (string?)typed },
			},
		};

		using var content = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), System.Text.Encoding.UTF8, "application/json");
		_response = await reporter.PostAsync(Submit, content);
	}

	[Then(@"the report is accepted")]
	public async Task ThenTheReportIsAccepted()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
	}

	[Then(@"the answer is stored as ""(.*)"", in French")]
	public async Task ThenTheAnswerIsStoredAsTyped(string typed)
	{
		var queue = await _admin!.GetFromJsonAsync<JsonElement>(
			new Uri("/api/admin/answers/awaiting-translation", UriKind.Relative));

		queue.GetProperty("answers").EnumerateArray().ShouldContain(entry =>
			entry.GetProperty("value").GetString() == typed
			&& entry.GetProperty("locale").GetString() == "fr-CA");
	}

	[Then(@"the question now offers ""(.*)"" as a reporter-added choice coded ""(.*)""")]
	public async Task ThenTheQuestionOffersIt(string typed,
											  string code)
	{
		var questions = await _admin!.GetFromJsonAsync<JsonElement>(AdminQuestions);
		var question = questions.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetString() == _typeAhead!.Id);
		var choice = question.GetProperty("options").EnumerateArray()
			.Single(candidate => candidate.GetProperty("code").GetString() == code);

		choice.GetProperty("labelFr").GetString().ShouldBe(typed);
		choice.GetProperty("labelEn").ValueKind.ShouldBe(JsonValueKind.Null);
		choice.GetProperty("addedByReporter").GetBoolean().ShouldBeTrue();
		question.GetProperty("reporterChoicesAwaitingReview").GetInt32().ShouldBe(1);
	}

	[Then(@"the next reporter is offered ""(.*)""")]
	public async Task ThenTheNextReporterIsOfferedIt(string typed)
	{
		using var client = (await BootedApi.Factory()).CreateClient();
		var questions = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
		var question = questions.EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == _typeAhead!.Key);

		question.GetProperty("options").EnumerateArray()
			.ShouldContain(option => option.GetProperty("labelFr").GetString() == typed);
	}

	/// <summary>Creates a type-ahead offering one written choice, returning its current revision.</summary>
	internal static async Task<string> CreateTypeAhead(HttpClient admin)
	{
		return (await CreateTypeAheadQuestion(admin)).RevisionId;
	}

	private static async Task<TypeAhead> CreateTypeAheadQuestion(HttpClient admin)
	{
		var key = $"launch_site_{Guid.NewGuid().ToString("N")[..12]}";
		using var createdQuestion = await admin.PostAsJsonAsync(AdminQuestions, new
		{
			key,
			type = "autocomplete",
			labelEn = "Where did you launch?",
			labelFr = "D'où avez-vous décollé?",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = new[] { new { code = (string?)null, labelEn = "Mount 7", labelFr = "Mont 7" } },
		});
		createdQuestion.StatusCode.ShouldBe(HttpStatusCode.Created, await createdQuestion.Content.ReadAsStringAsync());
		var created = await createdQuestion.Content.ReadFromJsonAsync<JsonElement>();

		return new TypeAhead(key, created.GetProperty("id").GetString()!, created.GetProperty("revisionId").GetString()!);
	}

	private sealed record TypeAhead(string Key, string Id, string RevisionId);
}
