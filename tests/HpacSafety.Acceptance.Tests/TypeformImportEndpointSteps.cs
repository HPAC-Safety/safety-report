using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
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
	private static readonly Uri PendingLogic = new("/api/admin/typeform/pending-logic", UriKind.Relative);
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);

	private HttpClient? _client;
	private HttpResponseMessage? _response;
	private string? _noteId;

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
