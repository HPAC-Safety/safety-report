using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The question-bank scenario that says an Administrator never invents an
///     option code — over HTTP, against the booted host, because the code is
///     derived where the authoring request meets the domain. Detailed request
///     shapes, including shared choice lists, live in
///     <c>HpacSafety.Api.Tests</c>.
/// </summary>
[Binding]
public sealed class ChoiceCodeEndpointSteps
{
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);

	private HttpClient? _client;
	private string? _questionId;
	private JsonElement _saved;
	private HttpResponseMessage? _refused;

	[Given(@"an Administrator saves a single-select question with the choices ""(.*)"" and ""(.*)""")]
	public async Task GivenASingleSelectQuestionIsSaved(string first, string second)
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);

		using var response = await _client.PostAsJsonAsync(Questions, Request($"launch_site_{Guid.NewGuid():N}"[..30], [
			new Choice(null, first),
			new Choice(null, second)
		]));
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		_saved = await response.Content.ReadFromJsonAsync<JsonElement>();
		_questionId = _saved.GetProperty("id").GetString();
	}

	[Then(@"the choices are recorded under the codes ""(.*)"" and ""(.*)""")]
	public void ThenTheChoicesAreRecordedUnder(string first, string second)
	{
		Codes().ShouldBe([first, second]);
	}

	[When(@"they reword ""(.*)"" to ""(.*)"" and save again")]
	public async Task WhenTheyReword(string before, string after)
	{
		var choices = _saved.GetProperty("options").EnumerateArray()
			.Select(option => option.GetProperty("labelEn").GetString() == before
				? new Choice(option.GetProperty("code").GetString(), after)
				: new Choice(option.GetProperty("code").GetString(), option.GetProperty("labelEn").GetString()!))
			.ToList();

		using var response = await _client!.PutAsJsonAsync(
			new Uri($"{Questions}/{_questionId}", UriKind.Relative), Request(null, choices));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		_saved = await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	[Then(@"that choice is still recorded under the code ""(.*)""")]
	public void ThenThatChoiceKeepsItsCode(string code)
	{
		Codes().ShouldContain(code);
	}

	[When(@"they save choices whose English wording reads ""(.*)"" and ""(.*)""")]
	public async Task WhenTheySaveAlikeChoices(string first, string second)
	{
		_refused = await _client!.PutAsJsonAsync(
			new Uri($"{Questions}/{_questionId}", UriKind.Relative),
			Request(null, [new Choice(null, first), new Choice(null, second)]));
	}

	[Then(@"the save is refused naming both wordings")]
	public async Task ThenTheSaveIsRefused()
	{
		_refused!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var detail = (await _refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString() ?? "";
		detail.ShouldContain("'Site A-1'");
		detail.ShouldContain("'Site A 1'");
		_refused.Dispose();
	}

	private List<string?> Codes()
	{
		return [.. _saved.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("code").GetString())];
	}

	private static object Request(string? key, IReadOnlyList<Choice> choices)
	{
		return new
		{
			key,
			type = "single_select",
			labelEn = "Where did you launch?",
			labelFr = "D'où avez-vous décollé?",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			allowsReporterAdditions = false,
			options = choices.Select(choice => new { code = choice.Code, labelEn = choice.Wording, labelFr = choice.Wording }),
		};
	}

	private sealed record Choice(string? Code, string Wording);
}
