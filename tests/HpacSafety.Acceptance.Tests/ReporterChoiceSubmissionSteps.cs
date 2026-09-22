using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The scenario that follows a reporter's type-ahead value from the
///     submission all the way into the shared list the next reporter reads —
///     over HTTP, against the booted host, because it is the submission path
///     that calls <c>OptionSet.AddFromReporter</c> (ADR-0063). What that domain
///     operation decides on its own is covered by <see cref="ReporterAddedChoiceSteps" />.
/// </summary>
[Binding]
public sealed class ReporterChoiceSubmissionSteps
{
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri OptionSets = new("/api/admin/option-sets", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private HttpClient? _admin;
	private TypeAhead? _typeAhead;
	private HttpResponseMessage? _response;
	private string? _typed;

	[Given(@"a published form has a type-ahead question backed by a shared choice list")]
	public async Task GivenAPublishedTypeAhead()
	{
		_admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		_typeAhead = await CreateTypeAheadWithList(_admin);
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
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (string?)"yes" },
				new { questionRevisionId = _typeAhead!.RevisionId, value = (string?)typed }
			}
		};

		using var content = new MultipartFormDataContent
		{
			{ new StringContent(JsonSerializer.Serialize(dto, JsonOptions)), "report" }
		};
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

	[Then(@"the shared list now offers ""(.*)"" as a reporter-added choice coded ""(.*)""")]
	public async Task ThenTheListOffersIt(string typed, string code)
	{
		var sets = await _admin!.GetFromJsonAsync<JsonElement>(OptionSets);
		var set = sets.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetString() == _typeAhead!.SetId);
		var item = set.GetProperty("items").EnumerateArray().Single(candidate => candidate.GetProperty("code").GetString() == code);

		item.GetProperty("labelFr").GetString().ShouldBe(typed);
		item.GetProperty("addedByReporter").GetBoolean().ShouldBeTrue();
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

	/// <summary>Creates a type-ahead backed by a fresh shared list, returning its current revision.</summary>
	internal static async Task<string> CreateTypeAhead(HttpClient admin)
	{
		return (await CreateTypeAheadWithList(admin)).RevisionId;
	}

	private static async Task<TypeAhead> CreateTypeAheadWithList(HttpClient admin)
	{
		var suffix = Guid.NewGuid().ToString("N")[..12];

		using var createdSet = await admin.PostAsJsonAsync(OptionSets, new
		{
			key = $"sites_{suffix}",
			nameEn = "Flying sites",
			nameFr = "Sites de vol",
			items = new[] { new { code = (string?)null, labelEn = "Mount 7", labelFr = "Mont 7" } }
		});
		createdSet.StatusCode.ShouldBe(HttpStatusCode.Created, await createdSet.Content.ReadAsStringAsync());
		var setId = (await createdSet.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

		var key = $"launch_site_{suffix}";
		using var createdQuestion = await admin.PostAsJsonAsync(AdminQuestions, new
		{
			key,
			type = "autocomplete",
			labelEn = "Where did you launch?",
			labelFr = "D'où avez-vous décollé?",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			optionSetId = setId,
			allowsReporterAdditions = true,
			options = Array.Empty<object>()
		});
		createdQuestion.StatusCode.ShouldBe(HttpStatusCode.Created, await createdQuestion.Content.ReadAsStringAsync());
		var revisionId = (await createdQuestion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionId").GetString()!;

		return new TypeAhead(key, setId, revisionId);
	}

	private sealed record TypeAhead(string Key, string SetId, string RevisionId);
}
