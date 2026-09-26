using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core.Features.Moderation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     A condition follows its parent question through a fork (<c>REQ-QB-140</c>,
///     ADR-0132).
/// </summary>
/// <remarks>
///     Whether the parent forks depends on it having been answered, a fact the admin
///     endpoint reads from reports, and resolving the condition needs the retired
///     parent the endpoints load beside the live bank. So this runs through the
///     booted API. Every question and answer here is synthetic.
/// </remarks>
[Binding]
public sealed class ForkedParentConditionSteps
{
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions/", UriKind.Relative);
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);

	private readonly string _run = Guid.NewGuid().ToString("N")[..10];
	private HttpClient? _admin;
	private JsonElement _parent;
	private JsonElement _dependent;
	private string? _replacementId;

	[Given(@"a question depends on the ""(.*)"" choice of an answered single-select question")]
	public async Task GivenADependentOnAnAnsweredParent(string label)
	{
		_admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		_parent = await Created(ParentRequest($"Which wing do you fly? {_run}"));

		var choiceId = ChoiceId(_parent, label);
		_dependent = await Created(new
		{
			type = "short_text",
			labelEn = $"Which rating do you hold? {_run}",
			labelFr = $"Quelle qualification détenez-vous? {_run}",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			dependsOnQuestionId = _parent.GetProperty("id").GetString(),
			dependsOnChoiceId = choiceId,
		});

		using var reporter = await BootedApi.SignedInAs(MemberRole.User);
		using var submitted = await reporter.PostAsJsonAsync(Submit, new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (bool?)true, choices = (string[]?)null },
				new { questionRevisionId = _parent.GetProperty("revisionId").GetString(), value = (string?)null, choices = new[] { choiceId } },
			},
		});
		submitted.StatusCode.ShouldBe(HttpStatusCode.Accepted, await submitted.Content.ReadAsStringAsync());
	}

	[When(@"an Administrator changes the single-select question's wording")]
	public async Task WhenTheParentIsReworded()
	{
		using var response = await _admin!.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{_parent.GetProperty("id").GetString()}", UriKind.Relative),
			ParentRequest($"Which wing were you flying? {_run}"));
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		_replacementId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
		_replacementId.ShouldNotBe(_parent.GetProperty("id").GetString(), "an answered question forks (ADR-0071)");
	}

	[Then(@"the report form and the editor show the condition on the replacement question and its ""(.*)"" choice")]
	public async Task ThenBothViewsShowTheReplacement(string label)
	{
		var replacement = await AdminView(_replacementId!);
		var choiceId = ChoiceId(replacement, label);

		var editor = await AdminView(_dependent.GetProperty("id").GetString()!);
		editor.GetProperty("dependsOnQuestionId").GetString().ShouldBe(_replacementId);
		editor.GetProperty("dependsOnChoiceId").GetString().ShouldBe(choiceId);

		using var client = (await BootedApi.Factory()).CreateClient();
		var form = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
		var shown = form.EnumerateArray().Single(question => question.GetProperty("id").GetString() == _dependent.GetProperty("id").GetString());
		shown.GetProperty("dependsOnQuestionId").GetString().ShouldBe(_replacementId);
		shown.GetProperty("dependsOnChoiceId").GetString().ShouldBe(choiceId);
	}

	[Then(@"the dependent question is not revised by its parent's fork")]
	public async Task ThenTheDependentKeepsItsRevision()
	{
		(await AdminView(_dependent.GetProperty("id").GetString()!)).GetProperty("revisionId").GetString()
			.ShouldBe(_dependent.GetProperty("revisionId").GetString());
	}

	[Then(@"saving the dependent question unchanged gives it no new revision")]
	public async Task ThenSavingItUnchangedKeepsItsRevision()
	{
		var editor = await AdminView(_dependent.GetProperty("id").GetString()!);

		using var response = await _admin!.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{_dependent.GetProperty("id").GetString()}", UriKind.Relative),
			new
			{
				type = "short_text",
				labelEn = editor.GetProperty("labelEn").GetString(),
				labelFr = editor.GetProperty("labelFr").GetString(),
				isRequired = false,
				isPrivate = false,
				isActive = true,
				dependsOnQuestionId = editor.GetProperty("dependsOnQuestionId").GetString(),
				dependsOnChoiceId = editor.GetProperty("dependsOnChoiceId").GetString(),
			});
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionId").GetString()
			.ShouldBe(_dependent.GetProperty("revisionId").GetString());
	}

	private static object ParentRequest(string labelEn)
	{
		return new
		{
			type = "single_select",
			labelEn,
			labelFr = $"{labelEn} (fr)",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = new[]
			{
				new { code = "hang_glider", labelEn = "Hang glider", labelFr = "Deltaplane" },
				new { code = "paraglider", labelEn = "Paraglider", labelFr = "Parapente" },
			},
		};
	}

	private async Task<JsonElement> Created(object request)
	{
		using var response = await _admin!.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private async Task<JsonElement> AdminView(string questionId)
	{
		var questions = await _admin!.GetFromJsonAsync<JsonElement>(AdminQuestions);
		return questions.EnumerateArray().Single(question => question.GetProperty("id").GetString() == questionId);
	}

	private static string ChoiceId(JsonElement question,
								   string labelEn)
	{
		return question.GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("labelEn").GetString() == labelEn)
			.GetProperty("id").GetString()!;
	}
}
