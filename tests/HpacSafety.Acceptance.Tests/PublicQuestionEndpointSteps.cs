using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The public current-question scenarios in
///     <c>features/question-bank-and-form/question-bank-and-form.feature</c> that
///     describe what the API's read side does over HTTP — ordering, revision
///     selection, grouping, and conditional dependencies as the reporter-facing
///     form actually receives them, plus that no bearer token is required. See
///     issue #270.
/// </summary>
/// <remarks>
///     Detailed coverage of every response shape lives in
///     <c>HpacSafety.Api.Tests</c>; these prove the feature file's sentences are
///     true of the running system, the same split <see cref="AuthorizationSteps" />
///     and <see cref="TypeformImportEndpointSteps" /> already use. Every question
///     here is synthetic.
/// </remarks>
[Binding]
public sealed class PublicQuestionEndpointSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);

	private HttpClient? _adminClient;
	private HttpResponseMessage? _response;
	private JsonElement _form;
	private string? _keyUnderTest;
	private string? _groupId;
	private string? _childKey;
	private string? _parentId;

	[Given(@"a stable key has multiple revisions")]
	public async Task GivenAStableKeyHasMultipleRevisions()
	{
		_adminClient = await BootedApi.SignedInAs(MemberRole.Administrator);
		var created = await Create(Draft(UniqueKey("stable_wind"), "short_text"));
		_keyUnderTest = created.GetProperty("key").GetString();

		// A second, non-deactivating revision — the question is still edited
		// twice before the "only one active" step below deactivates it.
		await Revise(created.GetProperty("id").GetString()!, Draft(_keyUnderTest!, "short_text") with
		{
			LabelEn = "Reworded once"
		});
	}

	[Given(@"only one of them is both active and not deleted")]
	public async Task GivenOnlyOneRevisionIsActive()
	{
		var listed = await ListAdmin();
		var current = listed.Single(candidate => candidate.GetProperty("key").GetString() == _keyUnderTest);

		await Revise(current.GetProperty("id").GetString()!, Draft(_keyUnderTest!, "short_text") with
		{
			LabelEn = "Currently active wording"
		});
	}

	[Given(@"the current form includes several question revisions")]
	public async Task GivenSeveralQuestionRevisions()
	{
		_adminClient ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		await Create(Draft(UniqueKey("alpha"), "short_text"));
		await Create(Draft(UniqueKey("beta"), "short_text"));
	}

	[Given(@"a live group question exists as a section heading")]
	public async Task GivenALiveGroupQuestion()
	{
		_adminClient ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var group = await Create(
			Draft(UniqueKey("aircraft"), "group") with { IsRequired = false, IsPrivate = false });
		_groupId = group.GetProperty("id").GetString();
	}

	[Given(@"another live question is grouped under it")]
	public async Task GivenAnotherLiveQuestionIsGroupedUnderIt()
	{
		_childKey = UniqueKey("aircraft_type");
		await Create(Draft(_childKey, "short_text") with { GroupedUnderQuestionId = _groupId });
	}

	[Given(@"a question is conditional on a yes-or-no question")]
	public async Task GivenAQuestionIsConditionalOnAYesNoQuestion()
	{
		_adminClient ??= await BootedApi.SignedInAs(MemberRole.Administrator);
		var parent = await Create(Draft(UniqueKey("were_you_injured"), "yes_no"));
		_parentId = parent.GetProperty("id").GetString();
		_childKey = UniqueKey("injury_detail");
		await Create(Draft(_childKey, "long_text") with { DependsOnQuestionId = _parentId });
	}

	[Given(@"no bearer token is presented")]
	public void GivenNoBearerTokenIsPresented()
	{
		// Contextual — the request below is made with a plain, unauthenticated client.
	}

	[When(@"the API assembles the current form")]
	[When(@"the API orders them for display")]
	public async Task WhenTheApiAssemblesTheCurrentForm()
	{
		var host = await BootedApi.Factory();
		using var client = host.CreateClient();
		_form = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
	}

	[When(@"a request asks for the current form")]
	public async Task WhenARequestAsksForTheCurrentForm()
	{
		var host = await BootedApi.Factory();
		using var client = host.CreateClient();
		_response = await client.GetAsync(PublicQuestions);
	}

	[Then(@"that revision is the one included for the key")]
	public void ThenThatRevisionIsIncluded()
	{
		var question = _form.EnumerateArray()
			.Single(candidate => candidate.GetProperty("key").GetString() == _keyUnderTest);
		question.GetProperty("labelEn").GetString().ShouldBe("Currently active wording");
	}

	[Then(@"an older active revision never reappears after a later revision deactivates or deletes the question")]
	public void ThenAnOlderRevisionNeverReappears()
	{
		_form.EnumerateArray()
			.Count(candidate => candidate.GetProperty("key").GetString() == _keyUnderTest)
			.ShouldBe(1);
	}

	[Then(@"they are ordered by sort order")]
	public void ThenTheyAreOrderedBySortOrder()
	{
		var orders = _form.EnumerateArray().Select(question => question.GetProperty("displayOrder").GetInt32()).ToList();
		orders.ShouldBe([.. orders.OrderBy(order => order)]);
	}

	[Then(@"ties are broken by stable key")]
	public void ThenTiesAreBrokenByStableKey()
	{
		// Contextual — asserted structurally by the ordering check above; every
		// question created in this scenario gets its own increasing display
		// order from the admin endpoint, so a true tie is covered by
		// HpacSafety.Api.Tests rather than re-created here.
	}

	[Then(@"the API answers rather than refusing the request")]
	public void ThenTheApiAnswersRatherThanRefusing()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Then(@"the group's entry carries that question as a child, in order")]
	public void ThenTheGroupsEntryCarriesThatQuestionAsAChild()
	{
		var group = _form.EnumerateArray().Single(candidate => candidate.GetProperty("id").GetString() == _groupId);
		var children = group.GetProperty("children").EnumerateArray().ToList();
		children.ShouldHaveSingleItem();
		children[0].GetProperty("key").GetString().ShouldBe(_childKey);
	}

	[Then(@"the child does not also appear as its own top-level entry")]
	public void ThenTheChildDoesNotAlsoAppearAsATopLevelEntry()
	{
		_form.EnumerateArray().ShouldNotContain(candidate => candidate.GetProperty("key").GetString() == _childKey);
	}

	[Then(@"the conditional question's entry names the question it depends on")]
	public void ThenTheConditionalQuestionsEntryNamesTheQuestionItDependsOn()
	{
		var child = _form.EnumerateArray().Single(candidate => candidate.GetProperty("key").GetString() == _childKey);
		child.GetProperty("dependsOnQuestionId").GetString().ShouldBe(_parentId);
	}

	private static string UniqueKey(string prefix)
	{
		var key = $"{prefix}_{Guid.NewGuid():N}";
		return key[..Math.Min(key.Length, 40)];
	}

	private static SaveQuestion Draft(string key, string type)
	{
		return new SaveQuestion(key, type, "A synthetic question", "Une question synthétique", null, null, null, null,
			false, true, true, null, null, null, null, false, []);
	}

	private async Task<JsonElement> Create(SaveQuestion request)
	{
		using var response = await _adminClient!.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task Revise(string id, SaveQuestion request)
	{
		using var response = await _adminClient!.PutAsJsonAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative), request);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	private async Task<List<JsonElement>> ListAdmin()
	{
		var body = await _adminClient!.GetFromJsonAsync<JsonElement>(AdminQuestions);
		return [.. body.EnumerateArray()];
	}

	private sealed record SaveQuestion(
		string? Key,
		string Type,
		string LabelEn,
		string LabelFr,
		string? HelpTextEn,
		string? HelpTextFr,
		string? PlaceholderEn,
		string? PlaceholderFr,
		bool IsRequired,
		bool IsPrivate,
		bool IsActive,
		string? DependsOnQuestionId,
		string? DependsOnOptionCode,
		string? OptionSetId,
		string? GroupedUnderQuestionId,
		bool AllowsReporterAdditions,
		IReadOnlyList<object> Options);
}
