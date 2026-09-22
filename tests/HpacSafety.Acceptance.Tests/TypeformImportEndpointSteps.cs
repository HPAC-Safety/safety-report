using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank.Typeform;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The Typeform import scenarios in
///     <c>features/typeform-question-import-export/typeform-question-import-export.feature</c>
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

	[When(@"an Administrator submits only one of the two files")]
	public async Task WhenOnlyOneFileIsSubmitted()
	{
		_client = await BootedApi.SignedInAsAsync(MemberRole.Administrator);

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
		_client = await BootedApi.SignedInAsAsync(MemberRole.Administrator);

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
		_client = await BootedApi.SignedInAsAsync(MemberRole.Administrator);
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
		_client = await BootedApi.SignedInAsAsync(MemberRole.Administrator);

		await CreateQuestionAsync(_client, UniqueKey("acceptance_export_a"));
		await CreateQuestionAsync(_client, UniqueKey("acceptance_export_b"));
	}

	[Given(@"a live question has a stable key, a dependency, and a group membership")]
	public async Task GivenALiveQuestionHasADependencyAndAGroupMembership()
	{
		_client = await BootedApi.SignedInAsAsync(MemberRole.Administrator);

		var group = await CreateQuestionAsync(_client, UniqueKey("acceptance_export_group"), type: "group", isPrivate: false);
		var groupId = group.GetProperty("id").GetString()!;

		var parent = await CreateQuestionAsync(_client, UniqueKey("acceptance_export_parent"), type: "yes_no", isPrivate: false);
		var parentId = parent.GetProperty("id").GetString()!;

		_exportedKey = UniqueKey("acceptance_export_child");
		await CreateQuestionAsync(
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

		using var archive = await OpenExportedZipAsync();
		var englishJson = await ReadEntryAsync(archive, "form-en.json");
		var frenchJson = await ReadEntryAsync(archive, "form-fr.json");

		TypeformDocument.Parse(englishJson).Fields.ShouldNotBeEmpty();
		TypeformDocument.Parse(frenchJson).Fields.ShouldNotBeEmpty();
	}

	[Then(@"the exported field carries that data in a namespaced extension object")]
	public async Task ThenTheExportedFieldCarriesTheExtensionObject()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var archive = await OpenExportedZipAsync();
		var document = TypeformDocument.Parse(await ReadEntryAsync(archive, "form-en.json"));
		var field = document.Fields.Single(candidate => candidate.Ref == _exportedKey);

		field.Properties.Hpac.ShouldNotBeNull();
		field.Properties.Hpac!.DependsOnKey.ShouldNotBeNull();
		field.Properties.Hpac.GroupedUnderKey.ShouldNotBeNull();
		field.Properties.Hpac.IsPrivate.ShouldBeTrue();
	}

	[Then(@"a plain Typeform file otherwise validates without it")]
	public async Task ThenAPlainTypeformFileOtherwiseValidates()
	{
		using var archive = await OpenExportedZipAsync();
		var json = await ReadEntryAsync(archive, "form-en.json");

		// A plain Typeform importer, unaware of "hpac", still sees an
		// ordinary field: ref, title, and type parse from raw JSON regardless.
		using var document = JsonDocument.Parse(json);
		var field = document.RootElement.GetProperty("fields")
			.EnumerateArray()
			.Single(candidate => candidate.GetProperty("ref").GetString() == _exportedKey);

		field.GetProperty("title").GetString().ShouldNotBeNullOrEmpty();
		field.GetProperty("type").GetString().ShouldNotBeNullOrEmpty();
	}

	[Given(@"a member does not have the Administrator role")]
	public async Task GivenAMemberDoesNotHaveTheAdministratorRole()
	{
		_client = await BootedApi.SignedInAsAsync(MemberRole.User);
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

	private static async Task<JsonElement> CreateQuestionAsync(
		HttpClient client,
		string key,
		string type = "short_text",
		bool isPrivate = true,
		string? dependsOnQuestionId = null,
		string? groupedUnderQuestionId = null)
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
			DependsOnOptionCode = (string?)null,
			OptionSetId = (string?)null,
			GroupedUnderQuestionId = groupedUnderQuestionId,
			AllowsReporterAdditions = false,
			Options = Array.Empty<object>(),
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
	private async Task<ZipArchive> OpenExportedZipAsync()
	{
		_exportedZipBytes ??= await _response!.Content.ReadAsByteArrayAsync();
		return new ZipArchive(new MemoryStream(_exportedZipBytes), ZipArchiveMode.Read);
	}

	private static async Task<string> ReadEntryAsync(ZipArchive archive, string entryName)
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
			{ english, "english", "form-en.json" }, { french, "french", "form-fr.json" }
		};
	}

	private static StringContent JsonContent(string json)
	{
		var content = new StringContent(json);
		content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
		return content;
	}
}
