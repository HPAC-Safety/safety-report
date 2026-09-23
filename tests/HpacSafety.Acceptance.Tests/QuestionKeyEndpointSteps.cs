using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The question-bank scenario that says an Administrator never sees or
///     chooses a question key — over HTTP, against the booted host, because the
///     key is derived where the authoring request meets the domain. The wording
///     carries a unique marker so a repeated run never meets its own keys.
/// </summary>
[Binding]
public sealed class QuestionKeyEndpointSteps
{
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);

	private readonly List<JsonElement> _saved = [];
	private HttpClient? _client;
	private string _marker = "";

	[Given(@"an Administrator saves a new question without a key")]
	public async Task GivenAQuestionIsSavedWithoutAKey()
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);
		_marker = Guid.NewGuid().ToString("N")[..8];

		await Save();
	}

	[Then(@"its key is derived from its English wording")]
	public void ThenItsKeyIsDerived()
	{
		Key(0).ShouldBe($"where_did_you_land_{_marker}");
	}

	[When(@"they save another question with the same English wording")]
	public async Task WhenTheySaveAnother()
	{
		await Save();
	}

	[Then(@"it receives a different key")]
	public void ThenItReceivesADifferentKey()
	{
		Key(1).ShouldBe($"{Key(0)}_2");
	}

	[When(@"they delete the first question and save a third with the same wording")]
	public async Task WhenTheyDeleteTheFirstAndSaveAThird()
	{
		using var deleted = await _client!.DeleteAsync(
			new Uri($"{Questions}/{_saved[0].GetProperty("id").GetString()}", UriKind.Relative));
		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());

		await Save();
	}

	[Then(@"the third question does not take the retired question's key")]
	public void ThenTheRetiredKeyIsNotReused()
	{
		Key(2).ShouldNotBe(Key(0));
		Key(2).ShouldBe($"{Key(0)}_3");
	}

	private string? Key(int index)
	{
		return _saved[index].GetProperty("key").GetString();
	}

	private async Task Save()
	{
		using var response = await _client!.PostAsJsonAsync(Questions, new
		{
			type = "short_text",
			labelEn = $"Where did you land {_marker}?",
			labelFr = $"Où avez-vous atterri {_marker}?",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			allowsReporterAdditions = false,
			options = Array.Empty<object>(),
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		_saved.Add(await response.Content.ReadFromJsonAsync<JsonElement>());
	}
}
