using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The question-bank scenarios that only the booted API can make true —
///     REQ-QB-003, REQ-QB-005, and REQ-QB-008.
/// </summary>
/// <remarks>
///     <para>
///         Whether a question has been answered is a fact the admin endpoint reads
///         from reports, deleted ones included, and the aggregate only acts on what
///         it is told (ADR-0071). One live question per key is a partial unique
///         index. The edit DTO is what the admin list returns. None of that is in
///         the domain, so these go through the API and its database.
///     </para>
///     <para>
///         Scenarios share one database, so each question here gets its own key and
///         every assertion reads back the rows its own scenario wrote. Every
///         question and answer is synthetic.
///     </para>
/// </remarks>
[Binding]
public sealed class QuestionForkEndpointSteps(QuestionEditOutcome outcome)
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);

	private HttpClient? _admin;
	private JsonElement _created;
	private JsonElement _edited;
	private JsonElement _dto;
	private string? _liveId;
	private IReadOnlyList<JsonElement> _resolved = [];
	private Question? _resolvedFromDatabase;
	private HttpResponseMessage? _blankLanguage;
	private HttpResponseMessage? _saved;

	// --- REQ-QB-003: an answer on a deleted report still forces a fork ---

	[Given(@"the only answer to a question is on a report that has been deleted")]
	public async Task GivenTheOnlyAnswerIsOnADeletedReport()
	{
		_created = await CreateQuestion();
		var reportId = await SubmitAnswering(_created.GetProperty("revisionId").GetString()!);

		using var deleted = await (await Admin()).DeleteAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));
		deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		await using var scope = await Scope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var questionId = TinyId.Parse(_created.GetProperty("id").GetString()!);

		// Deleted, and the only answer there is — so only an answer on a deleted
		// report can be what forces the fork.
		(await database.Reports.AnyAsync(report => report.Id == TinyId.Parse(reportId))).ShouldBeFalse();
		(await database.ReportAnswers.IgnoreQueryFilters().CountAsync(answer => answer.QuestionId == questionId)).ShouldBe(1);
	}

	[When(@"an Administrator changes that question's wording")]
	public async Task WhenAnAdministratorRewordsThatQuestion()
	{
		_edited = await Reword(_created, "A reworded synthetic question");
		await LoadOutcome(_created.GetProperty("id").GetString()!, _edited.GetProperty("id").GetString()!);
	}

	// --- REQ-QB-005: only one question per key is live ---

	[Given(@"a stable key has a retired question and a live one")]
	public async Task GivenARetiredAndALiveQuestion()
	{
		_created = await CreateQuestion();
		await SubmitAnswering(_created.GetProperty("revisionId").GetString()!);
		_edited = await Reword(_created, "A reworded synthetic question");
		_liveId = _edited.GetProperty("id").GetString();

		_liveId.ShouldNotBe(_created.GetProperty("id").GetString(), "the answered question forked");
	}

	[When(@"anything resolves that key")]
	public async Task WhenAnythingResolvesTheKey()
	{
		var key = Key();

		using var anonymous = (await BootedApi.Factory()).CreateClient();
		var form = await anonymous.GetFromJsonAsync<JsonElement>(PublicQuestions);
		_resolved = [.. form.EnumerateArray().Where(entry => entry.GetProperty("key").GetString() == key)];

		await using var scope = await Scope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		_resolvedFromDatabase = await database.Questions.SingleAsync(question => question.Key == key);
	}

	[Then(@"it resolves to the live question")]
	public void ThenItResolvesToTheLiveQuestion()
	{
		_resolved.ShouldHaveSingleItem().GetProperty("id").GetString().ShouldBe(_liveId);
		_resolvedFromDatabase!.Id.Value.ShouldBe(_liveId);
	}

	[Then(@"a second live question for the same key is rejected")]
	public async Task ThenASecondLiveQuestionIsRejected()
	{
		var key = Key();

		using var viaApi = await (await Admin()).PostAsJsonAsync(AdminQuestions, Request(key, "A duplicate synthetic question"));
		viaApi.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		// The API refuses first; the partial unique index refuses whatever
		// gets past it (ADR-0071).
		await using var scope = await Scope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		database.Questions.Add(Question.Create(
			key, QuestionType.ShortText, "A duplicate synthetic question", "Une question synthétique en double",
			DateTimeOffset.UtcNow, isActive: true));

		await Should.ThrowAsync<DbUpdateException>(() => database.SaveChangesAsync());
	}

	// --- REQ-QB-008: an edit copies the latest revision into a new one ---

	[Given(@"an Administrator requests to edit a question with an existing revision")]
	public async Task GivenAQuestionWithAnExistingRevision()
	{
		_created = await CreateQuestion();

		// A second revision, so "the latest" is not also "the first".
		_edited = await Reword(_created, "A once-reworded synthetic question");
		_edited.GetProperty("revisionNumber").GetInt32().ShouldBe(2);
	}

	[When(@"the API prepares the edit DTO")]
	public async Task WhenTheApiPreparesTheEditDto()
	{
		var list = await (await Admin()).GetFromJsonAsync<JsonElement>(AdminQuestions);
		var id = _created.GetProperty("id").GetString();
		_dto = list.EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == id);
	}

	[Then(@"it loads the latest revision and copies all fields into that DTO")]
	public async Task ThenTheDtoCopiesTheLatestRevision()
	{
		var latest = (await LoadQuestion(_created.GetProperty("id").GetString()!)).CurrentRevision;

		latest.RevisionNumber.ShouldBe(2);
		_dto.GetProperty("revisionId").GetString().ShouldBe(latest.Id.Value);
		_dto.GetProperty("revisionNumber").GetInt32().ShouldBe(latest.RevisionNumber);
		Fields(_dto).ShouldBe(Fields(latest));
	}

	[When(@"the Administrator saves the edit")]
	public async Task WhenTheAdministratorSavesTheEdit()
	{
		var id = _dto.GetProperty("id").GetString();
		var uri = new Uri($"/api/admin/questions/{id}", UriKind.Relative);
		var fields = Fields(_dto);

		_blankLanguage = await (await Admin()).PutAsJsonAsync(uri, Request(null, fields with { LabelFr = " " }));
		_saved = await (await Admin()).PutAsJsonAsync(uri, Request(null, fields with { LabelEn = "A twice-reworded synthetic question" }));
	}

	[Then(@"the API validates both languages, then saves a new complete row rather than patching the existing revision")]
	public async Task ThenTheEditIsANewCompleteRow()
	{
		_blankLanguage!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		_saved!.StatusCode.ShouldBe(HttpStatusCode.OK, await _saved.Content.ReadAsStringAsync());

		var question = await LoadQuestion(_dto.GetProperty("id").GetString()!);
		var revisions = question.Revisions.OrderBy(revision => revision.RevisionNumber).ToList();
		revisions.Select(revision => revision.RevisionNumber).ShouldBe([1, 2, 3]);

		// The revision the DTO was copied from is exactly as it was.
		var copied = revisions[1];
		copied.Id.Value.ShouldBe(_dto.GetProperty("revisionId").GetString());
		Fields(copied).ShouldBe(Fields(_dto));

		// The new row is complete: every field carried, only the wording changed.
		Fields(revisions[2]).ShouldBe(Fields(_dto) with { LabelEn = "A twice-reworded synthetic question" });
	}

	// --- helpers ---

	private async Task<HttpClient> Admin()
	{
		return _admin ??= await BootedApi.SignedInAs(MemberRole.Administrator);
	}

	private static async Task<AsyncServiceScope> Scope()
	{
		return (await BootedApi.Factory()).Services.CreateAsyncScope();
	}

	private string Key()
	{
		return _created.GetProperty("key").GetString()!;
	}

	/// <summary>A new, active, public short-text question with every optional field filled.</summary>
	private async Task<JsonElement> CreateQuestion()
	{
		var fields = new EditableFields(
			"short_text", $"A synthetic question {Guid.NewGuid():N}", "Une question synthétique",
			"Synthetic help.", "Aide synthétique.", "Synthetic placeholder", "Exemple synthétique",
			true, false, true);

		using var response = await (await Admin()).PostAsJsonAsync(AdminQuestions, Request(null, fields));
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<JsonElement> Reword(JsonElement view,
										   string labelEn)
	{
		var id = view.GetProperty("id").GetString();
		using var response = await (await Admin()).PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative),
			Request(null, Fields(view) with { LabelEn = labelEn }));

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	/// <summary>Files a report answering this revision, and returns its id.</summary>
	private static async Task<string> SubmitAnswering(string revisionId)
	{
		var consent = await ReportSubmissionEndpointSteps.ConsentRevisionId();
		using var reporter = await BootedApi.SignedInAs(MemberRole.User);

		using var response = await reporter.PostAsJsonAsync(Submit, new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consent, value = (bool?)false },
				new { questionRevisionId = revisionId, value = (string?)"A synthetic answer" },
			},
		});

		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
	}

	private async Task LoadOutcome(string originalId,
								   string liveId)
	{
		outcome.OriginalLabelEn = _created.GetProperty("labelEn").GetString();
		outcome.Original = await LoadQuestion(originalId);
		outcome.Live = await LoadQuestion(liveId);

		await using var scope = await Scope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		outcome.Answer = await database.ReportAnswers
			.IgnoreQueryFilters()
			.SingleAsync(answer => answer.QuestionId == outcome.Original.Id);
	}

	private static async Task<Question> LoadQuestion(string id)
	{
		await using var scope = await Scope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var questionId = TinyId.Parse(id);

		return await database.Questions
			.IgnoreQueryFilters()
			.Include(question => question.Revisions)
			.AsNoTracking()
			.SingleAsync(question => question.Id == questionId);
	}

	private static object Request(string? key,
								  string labelEn)
	{
		return Request(key, new EditableFields(
			"short_text", labelEn, "Une question synthétique", null, null, null, null, false, false, true));
	}

	private static object Request(string? key,
								  EditableFields fields)
	{
		return new
		{
			key,
			type = fields.Type,
			labelEn = fields.LabelEn,
			labelFr = fields.LabelFr,
			helpTextEn = fields.HelpTextEn,
			helpTextFr = fields.HelpTextFr,
			placeholderEn = fields.PlaceholderEn,
			placeholderFr = fields.PlaceholderFr,
			isRequired = fields.IsRequired,
			isPrivate = fields.IsPrivate,
			isActive = fields.IsActive,
			dependsOnQuestionId = (string?)null,
			dependsOnOptionCode = (string?)null,
			groupedUnderQuestionId = (string?)null,
			options = Array.Empty<object>(),
		};
	}

	private static EditableFields Fields(JsonElement view)
	{
		return new EditableFields(
			view.GetProperty("type").GetString()!,
			view.GetProperty("labelEn").GetString()!,
			view.GetProperty("labelFr").GetString()!,
			view.GetProperty("helpTextEn").GetString(),
			view.GetProperty("helpTextFr").GetString(),
			view.GetProperty("placeholderEn").GetString(),
			view.GetProperty("placeholderFr").GetString(),
			view.GetProperty("isRequired").GetBoolean(),
			view.GetProperty("isPrivate").GetBoolean(),
			view.GetProperty("isActive").GetBoolean());
	}

	private static EditableFields Fields(QuestionRevision revision)
	{
		return new EditableFields(
			EnumCode.Of(revision.Type), revision.LabelEn, revision.LabelFr, revision.HelpTextEn, revision.HelpTextFr,
			revision.PlaceholderEn, revision.PlaceholderFr, revision.IsRequired, revision.IsPrivate, revision.IsActive);
	}

	/// <summary>What an Administrator edits, as one comparable value.</summary>
	private sealed record EditableFields(
		string Type,
		string LabelEn,
		string LabelFr,
		string? HelpTextEn,
		string? HelpTextFr,
		string? PlaceholderEn,
		string? PlaceholderFr,
		bool IsRequired,
		bool IsPrivate,
		bool IsActive);
}
