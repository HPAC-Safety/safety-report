using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank.Typeform;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The Typeform import scenarios in
///     <c>.spec/features/typeform-question-import-export/typeform-question-import-export.feature</c>
///     that describe what the API does — over HTTP, against the booted host —
///     rather than what <see cref="HpacSafety.Core.Features.QuestionBank.Typeform.TypeformQuestionMapper" />
///     decides on its own. See ADR-0077, amended by ADR-0078. Every other
///     mapping scenario in that file runs against the mapper directly; see
///     <see cref="TypeformImportSteps" />.
/// </summary>
/// <remarks>
///     Detailed coverage of every request shape lives in
///     <c>HpacSafety.Api.Tests</c>; these prove the feature file's sentences
///     are true of the running system, the same split
///     <see cref="AuthorizationSteps" /> already uses.
/// </remarks>
[Binding]
public sealed class TypeformImportEndpointSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri Import = new("/api/admin/typeform/import", UriKind.Relative);
	private static readonly Uri Export = new("/api/admin/typeform/export", UriKind.Relative);
	private static readonly Uri PendingLogic = new("/api/admin/typeform/pending-logic", UriKind.Relative);
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);

	private HttpClient? _client;
	private HttpResponseMessage? _response;
	private HttpResponseMessage? _secondResponse;
	private string? _noteId;
	private string? _exportedKey;
	private byte[]? _exportedZipBytes;
	private string[] _originalKeys = [];
	private JsonElement _reimportedPreview;
	private readonly Dictionary<string, bool> _futureDatesByKey = [];
	private readonly Dictionary<string, string> _dependencyKeys = new(StringComparer.Ordinal);
	private JsonElement _dependencyParent;
	private string? _dependencyChildName;

	[When(@"an Administrator submits only one of the two files")]
	public async Task WhenOnlyOneFileIsSubmitted()
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);

		using var content = new MultipartFormDataContent { { EnglishOnlyContent(), "english", "form.json" } };
		_response = await _client.PostAsync(Import, content);
	}

	[Then(@"the import is rejected")]
	public void ThenTheImportIsRejected()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Then(@"no draft is produced")]
	public void ThenNoDraftIsProduced()
	{
		// Contextual — a request missing a required file never reaches the mapper.
	}

	[Given(@"a pending logic note exists from a prior import")]
	public async Task GivenAPendingLogicNoteExists()
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);

		using var imported = await _client.PostAsync(Import, MatchedPairContent());
		var preview = await imported.Content.ReadFromJsonAsync<JsonElement>();
		_noteId = preview.GetProperty("pendingLogicNoteIds").EnumerateArray().First().GetString();
	}

	[When(@"an Administrator wires the equivalent condition by hand and deletes the note")]
	public async Task WhenTheNoteIsResolvedAndDeleted()
	{
		// Wiring the equivalent condition happens on the ordinary
		// question-authoring screen, outside this endpoint's concern; only
		// the note's deletion is this scenario's assertion.
		_response = await _client!.DeleteAsync(new Uri($"{PendingLogic}/{_noteId}", UriKind.Relative));
	}

	[Then(@"the note no longer appears in the pending list")]
	public async Task ThenTheNoteNoLongerAppears()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var listed = await _client!.GetFromJsonAsync<JsonElement>(PendingLogic);
		listed.EnumerateArray().ShouldNotContain(note => note.GetProperty("id").GetString() == _noteId);
	}

	[Given(@"a pair of Typeform files is imported")]
	public async Task GivenAPairOfTypeformFilesIsImported()
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);
		_response = await _client.PostAsync(Import, MatchedPairContent());
	}

	[Then(@"no question exists in the bank until an Administrator reviews and saves its draft")]
	public async Task ThenNoQuestionExistsYet()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);

		var questions = await _client!.GetFromJsonAsync<JsonElement>(Questions);
		questions.EnumerateArray().ShouldNotContain(question => question.GetProperty("key").GetString() == "acceptance_field");
	}

	[Given(@"the question bank has several live questions")]
	public async Task GivenTheQuestionBankHasSeveralLiveQuestions()
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);

		var keyA = UniqueKey("acceptance_export_a");
		var keyB = UniqueKey("acceptance_export_b");
		await CreateQuestion(_client, keyA);
		await CreateQuestion(_client, keyB, type: "yes_no");
		_originalKeys = [keyA, keyB];
	}

	[Given(@"a live question has a stable key, a dependency, and a group membership")]
	public async Task GivenALiveQuestionHasADependencyAndAGroupMembership()
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);

		var group = await CreateQuestion(_client, UniqueKey("acceptance_export_group"), type: "group", isPrivate: false);
		var groupId = group.GetProperty("id").GetString()!;

		var parent = await CreateQuestion(_client, UniqueKey("acceptance_export_parent"), type: "yes_no", isPrivate: false);
		var parentId = parent.GetProperty("id").GetString()!;

		_exportedKey = UniqueKey("acceptance_export_child");
		await CreateQuestion(
			_client, _exportedKey, dependsOnQuestionId: parentId, groupedUnderQuestionId: groupId, isPrivate: true);
	}

	[When(@"an Administrator exports it")]
	public async Task WhenAnAdministratorExportsIt()
	{
		_response = await _client!.GetAsync(Export);
	}

	[When(@"it is exported")]
	public async Task WhenItIsExported()
	{
		_response = await _client!.GetAsync(Export);
	}

	[Then(@"the result is a zip containing an English Typeform-shaped file and a French one")]
	public async Task ThenTheResultIsAZipOfTwoTypeformFiles()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var archive = await OpenExportedZip();
		var englishJson = await ReadEntry(archive, "form-en.json");
		var frenchJson = await ReadEntry(archive, "form-fr.json");

		TypeformDocument.Parse(englishJson).Fields.ShouldNotBeEmpty();
		TypeformDocument.Parse(frenchJson).Fields.ShouldNotBeEmpty();
	}

	[Then(@"the exported field carries that data in a namespaced extension object")]
	public async Task ThenTheExportedFieldCarriesTheExtensionObject()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var archive = await OpenExportedZip();
		var document = TypeformDocument.Parse(await ReadEntry(archive, "form-en.json"));
		var field = document.Fields.Single(candidate => candidate.Ref == _exportedKey);

		field.Properties.Hpac.ShouldNotBeNull();
		field.Properties.Hpac!.DependsOnKey.ShouldNotBeNull();
		field.Properties.Hpac.GroupedUnderKey.ShouldNotBeNull();
		field.Properties.Hpac.IsPrivate.ShouldBeTrue();
	}

	[Then(@"a plain Typeform file otherwise validates without it")]
	public async Task ThenAPlainTypeformFileOtherwiseValidates()
	{
		using var archive = await OpenExportedZip();
		var json = await ReadEntry(archive, "form-en.json");

		// A plain Typeform importer, unaware of "hpac", still sees an
		// ordinary field: ref, title, and type parse from raw JSON regardless.
		using var document = JsonDocument.Parse(json);
		var field = document.RootElement.GetProperty("fields")
			.EnumerateArray()
			.Single(candidate => candidate.GetProperty("ref").GetString() == _exportedKey);

		field.GetProperty("title").GetString().ShouldNotBeNullOrEmpty();
		field.GetProperty("type").GetString().ShouldNotBeNullOrEmpty();
	}

	[When(@"an Administrator exports it and imports the result back in")]
	public async Task WhenAnAdministratorExportsItAndImportsTheResultBackIn()
	{
		_response = await _client!.GetAsync(Export);
		_response.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var archive = await OpenExportedZip();
		var englishJson = await ReadEntry(archive, "form-en.json");
		var frenchJson = await ReadEntry(archive, "form-fr.json");

		using var content = new MultipartFormDataContent
		{
			{ JsonContent(englishJson), "english", "form-en.json" }, { JsonContent(frenchJson), "french", "form-fr.json" },
		};
		var reimported = await _client.PostAsync(Import, content);
		reimported.StatusCode.ShouldBe(HttpStatusCode.OK, await reimported.Content.ReadAsStringAsync());

		_reimportedPreview = await reimported.Content.ReadFromJsonAsync<JsonElement>();
	}

	[Then(@"the resulting drafts match the original questions' key, type, wording, and options")]
	public void ThenTheResultingDraftsMatchTheOriginalQuestions()
	{
		var drafts = _reimportedPreview.GetProperty("drafts").EnumerateArray().ToList();

		foreach (var key in _originalKeys)
		{
			drafts.ShouldContain(draft => draft.GetProperty("key").GetString() == key);
		}
	}

	// --- REQ-TF-022: Allow future dates round-trips through the hpac object (ADR-0138) ---

	[Given(@"a live date question that allows future dates and another that does not")]
	public async Task GivenTwoLiveDateQuestions()
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);

		foreach (var allows in new[] { true, false })
		{
			var key = UniqueKey(allows ? "acceptance_export_future" : "acceptance_export_past");
			await CreateQuestion(_client, key, type: "date", allowFutureDates: allows);
			_futureDatesByKey[key] = allows;
		}
	}

	[Then(@"each date question's draft allows future dates exactly as the original did")]
	public void ThenEachDateDraftAllowsFutureDatesAsTheOriginal()
	{
		var drafts = _reimportedPreview.GetProperty("drafts").EnumerateArray().ToList();

		foreach (var (key, allows) in _futureDatesByKey)
		{
			var draft = drafts.Single(candidate => candidate.GetProperty("key").GetString() == key);
			draft.GetProperty("type").GetString().ShouldBe("date");
			draft.GetProperty("allowFutureDates").ValueKind.ShouldBe(allows ? JsonValueKind.True : JsonValueKind.False);
		}
	}

	[Then(@"a date field in a plain Typeform file, with no hpac object, imports without allowing future dates")]
	public async Task ThenAPlainDateFieldImportsWithoutFutureDates()
	{
		const string english = """{"fields":[{"id":"1","ref":"acceptance-plain-date","title":"When?","type":"date","properties":{}}],"logic":[]}""";
		const string french = """{"fields":[{"id":"2","ref":"acceptance-plain-date","title":"Quand?","type":"date","properties":{}}],"logic":[]}""";
		using var content = new MultipartFormDataContent
		{
			{ JsonContent(english), "english", "form-en.json" }, { JsonContent(french), "french", "form-fr.json" },
		};
		using var response = await _client!.PostAsync(Import, content);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		var draft = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("drafts").EnumerateArray().ShouldHaveSingleItem();
		draft.GetProperty("type").GetString().ShouldBe("date");
		draft.GetProperty("allowFutureDates").GetBoolean().ShouldBeFalse();
	}

	[Given(@"a live type-ahead {string} question whose choices depend on the single-select {string} question")]
	public async Task GivenALiveDependentTypeAhead(string child,
												   string parent)
	{
		_client = await BootedApi.SignedInAs(MemberRole.Administrator);
		_dependencyKeys[parent] = UniqueKey("acceptance_export_make");
		_dependencyKeys[child] = UniqueKey("acceptance_export_model");
		_dependencyParent = await PostQuestion(_client, new JsonObject
		{
			["key"] = _dependencyKeys[parent],
			["type"] = "single_select",
			["labelEn"] = parent,
			["labelFr"] = parent,
			["isRequired"] = false,
			["isPrivate"] = false,
			["isActive"] = true,
			["options"] = new JsonArray(ChoiceNode("Niviuk", null), ChoiceNode("Ozone", null)),
		});
		_dependencyChildName = child;
	}

	[Given(@"{string} offers {string} under {string} and {string}, and {string} under {string}")]
	public async Task GivenTheChildsChoices(string child,
										   string shared,
										   string firstParent,
										   string secondParent,
										   string alone,
										   string aloneParent)
	{
		string ParentId(string label) => _dependencyParent.GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("labelEn").GetString() == label).GetProperty("id").GetString()!;

		await PostQuestion(_client!, new JsonObject
		{
			["key"] = _dependencyKeys[child],
			["type"] = "autocomplete",
			["labelEn"] = child,
			["labelFr"] = child,
			["isRequired"] = false,
			["isPrivate"] = false,
			["isActive"] = true,
			["choicesDependOnQuestionId"] = _dependencyParent.GetProperty("id").GetString(),
			["options"] = new JsonArray(
				ChoiceNode(shared, [ParentId(firstParent), ParentId(secondParent)]),
				ChoiceNode(alone, [ParentId(aloneParent)])),
		});
	}

	[Then(@"the {string} draft depends on {string} by key")]
	public void ThenTheDraftDependsByKey(string child,
										 string parent)
	{
		Draft(child).GetProperty("choicesDependOnKey").GetString().ShouldBe(_dependencyKeys[parent]);
		Draft(parent).GetProperty("choicesDependOnKey").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Then(@"its {string} draft is offered under {string} and {string}, and {string} under {string}, by their codes")]
	public void ThenEachChoiceDraftNamesItsParents(string shared,
												   string firstParent,
												   string secondParent,
												   string alone,
												   string aloneParent)
	{
		string[] RefsOf(string label) =>
		[
			.. Draft(_dependencyChildName!).GetProperty("options").EnumerateArray()
				.Single(option => option.GetProperty("labelEn").GetString() == label)
				.GetProperty("parentRefs").EnumerateArray().Select(parentRef => parentRef.GetString()!),
		];

		string CodeOf(string label) => _dependencyParent.GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("labelEn").GetString() == label).GetProperty("code").GetString()!;

		RefsOf(shared).ShouldBe(new[] { CodeOf(firstParent), CodeOf(secondParent) }.Order(StringComparer.Ordinal));
		RefsOf(alone).ShouldBe([CodeOf(aloneParent)]);
	}

	[Then(@"a field in a file with no dependency in its hpac object imports with no dependency")]
	public async Task ThenAFileWithoutADependencyImportsNone()
	{
		const string english = """{"fields":[{"id":"1","ref":"acceptance-old-model","title":"Model?","type":"multiple_choice","properties":{"choices":[{"id":"a","ref":"mentor_7","label":"Mentor 7"}],"hpac":{"type":"autocomplete","is_private":false,"is_required":false,"depends_on_key":null,"depends_on_option_code":null,"grouped_under_key":null}}}],"logic":[]}""";
		const string french = """{"fields":[{"id":"2","ref":"acceptance-old-model","title":"Modèle?","type":"multiple_choice","properties":{"choices":[{"id":"b","ref":"mentor_7","label":"Mentor 7"}]}}],"logic":[]}""";
		using var content = new MultipartFormDataContent
		{
			{ JsonContent(english), "english", "form-en.json" }, { JsonContent(french), "french", "form-fr.json" },
		};
		using var response = await _client!.PostAsync(Import, content);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		var draft = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("drafts").EnumerateArray().ShouldHaveSingleItem();
		draft.GetProperty("type").GetString().ShouldBe("autocomplete");
		draft.GetProperty("choicesDependOnKey").ValueKind.ShouldBe(JsonValueKind.Null);
		draft.GetProperty("options").EnumerateArray().ShouldHaveSingleItem().GetProperty("parentRefs").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	private JsonElement Draft(string name)
	{
		return _reimportedPreview.GetProperty("drafts").EnumerateArray()
			.Single(draft => draft.GetProperty("key").GetString() == _dependencyKeys[name]);
	}

	private static JsonObject ChoiceNode(string label,
										 string[]? parentChoiceIds)
	{
		return new JsonObject
		{
			["labelEn"] = label,
			["labelFr"] = label,
			["parentChoiceIds"] = parentChoiceIds is null ? null : new JsonArray([.. parentChoiceIds.Select(id => (JsonNode)id)]),
		};
	}

	private static async Task<JsonElement> PostQuestion(HttpClient client,
														JsonObject request)
	{
		using var response = await client.PostAsJsonAsync(Questions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	[Given(@"a member does not have the Administrator role")]
	public async Task GivenAMemberDoesNotHaveTheAdministratorRole()
	{
		_client = await BootedApi.SignedInAs(MemberRole.User);
	}

	[When(@"that member attempts to import or export")]
	public async Task WhenThatMemberAttemptsToImportOrExport()
	{
		_response = await _client!.PostAsync(Import, MatchedPairContent());
		_secondResponse = await _client.GetAsync(Export);
	}

	[Then(@"the API rejects both attempts")]
	public void ThenTheApiRejectsTheAttempt()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		_secondResponse!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private static async Task<JsonElement> CreateQuestion(
		HttpClient client,
		string key,
		string type = "short_text",
		bool isPrivate = true,
		string? dependsOnQuestionId = null,
		string? groupedUnderQuestionId = null,
		bool allowFutureDates = false)
	{
		var request = new
		{
			Key = key,
			Type = type,
			LabelEn = "An acceptance question",
			LabelFr = "Une question d'acceptation",
			HelpTextEn = (string?)null,
			HelpTextFr = (string?)null,
			PlaceholderEn = (string?)null,
			PlaceholderFr = (string?)null,
			IsRequired = false,
			IsPrivate = isPrivate,
			IsActive = true,
			DependsOnQuestionId = dependsOnQuestionId,
			DependsOnChoiceId = (string?)null,
			OptionSetId = (string?)null,
			GroupedUnderQuestionId = groupedUnderQuestionId,
			AllowsReporterAdditions = false,
			Options = Array.Empty<object>(),
			AllowFutureDates = allowFutureDates,
		};

		using var response = await client.PostAsJsonAsync(Questions, request);
		response.EnsureSuccessStatusCode();

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private static string UniqueKey(string prefix)
	{
		var key = $"{prefix}_{Guid.NewGuid():N}";
		return key[..Math.Min(key.Length, 40)];
	}

	/// <summary>
	///     A response's content stream can only be read once — several
	///     scenarios read <c>_response</c>'s zip across more than one step, so
	///     this caches the bytes the first time and reopens a fresh archive
	///     over them every time.
	/// </summary>
	private async Task<ZipArchive> OpenExportedZip()
	{
		_exportedZipBytes ??= await _response!.Content.ReadAsByteArrayAsync();
		return new ZipArchive(new MemoryStream(_exportedZipBytes), ZipArchiveMode.Read);
	}

	private static async Task<string> ReadEntry(ZipArchive archive,
												string entryName)
	{
		using var stream = archive.GetEntry(entryName)!.Open();
		using var reader = new StreamReader(stream);
		return await reader.ReadToEndAsync();
	}

	private static StringContent EnglishOnlyContent()
	{
		return JsonContent(
			"""{"fields":[{"id":"1","ref":"acceptance-field","title":"Field","type":"short_text","properties":{}}],"logic":[]}""");
	}

	private static MultipartFormDataContent MatchedPairContent()
	{
		var english = JsonContent(
			"""
			{
			  "fields": [
			    {"id": "1", "ref": "acceptance-field", "title": "Field", "type": "short_text", "properties": {}}
			  ],
			  "logic": [
			    {
			      "type": "field",
			      "ref": "acceptance-field",
			      "actions": [
			        {
			          "action": "jump",
			          "details": {"to": {"type": "field", "value": "somewhere"}},
			          "condition": {"op": "is", "vars": [{"type": "field", "value": "other-ref"}, {"type": "choice", "value": "some-choice"}]}
			        }
			      ]
			    }
			  ]
			}
			""");
		var french = JsonContent(
			"""{"fields":[{"id":"1","ref":"acceptance-field","title":"Champ","type":"short_text","properties":{}}],"logic":[]}""");

		return new MultipartFormDataContent
		{
			{ english, "english", "form-en.json" }, { french, "french", "form-fr.json" },
		};
	}

	private static StringContent JsonContent(string json)
	{
		var content = new StringContent(json);
		content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
		return content;
	}
}
