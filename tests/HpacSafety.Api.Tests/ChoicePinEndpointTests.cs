using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Every staff view that lists a question's choices carries each choice's pin,
///     so the browser lists them as the form does (ADR-0136): the type-ahead review
///     page's merge targets and a report's multi-select answer. The form's and the
///     editor's views are REQ-QB-144. Every value here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class ChoicePinEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Awaiting = new("/api/admin/type-ahead-values/awaiting-review", UriKind.Relative);
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly DateTimeOffset At = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenPinnedValues_WhenFlaggedValueIsListed_ThenMergeTargetsCarryPinsInListOrder()
	{
		// Given
		TinyId added;
		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var question = Question.Create(
				$"site_{Guid.NewGuid():N}"[..20], QuestionType.Autocomplete, "Where?", "Où ?", At, isActive: true,
				options:
				[
					new QuestionOptionInput("woodside", "Woodside", "Woodside", Pin: ChoicePin.Last),
					new QuestionOptionInput("coopers", "Cooper's", "Cooper's"),
				]);
			database.Questions.Add(question);
			var report = new Report(Locale.EnCa, At);
			report.Answer(question, "Mount 7", At);
			database.Reports.Add(report);
			await database.SaveChangesAsync();
			added = question.AllChoices.Single(choice => choice.AddedByReporter).Id;
		}

		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var listed = await client.GetFromJsonAsync<JsonElement>(Awaiting);

		// Then
		listed.GetProperty("values").EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == added.Value)
			.GetProperty("mergeTargets").EnumerateArray()
			.Select(entry => (entry.GetProperty("labelEn").GetString(), entry.GetProperty("pin").GetString()))
			.ShouldBe([("Cooper's", "none"), ("Woodside", "last")]);
	}

	[Fact]
	public async Task GivenMultiSelectAnswer_WhenReportIsRead_ThenEachChoiceValueCarriesItsPin()
	{
		// Given
		TinyId reportId;
		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var question = Question.Create(
				$"conditions_{Guid.NewGuid():N}"[..20], QuestionType.MultiSelect, "Conditions?", "Conditions ?", At, isActive: true,
				options:
				[
					new QuestionOptionInput("other", "Other", "Autre", Pin: ChoicePin.Last),
					new QuestionOptionInput("gusty", "Gusty", "Rafales"),
				]);
			var narrative = Question.Create(
				$"notes_{Guid.NewGuid():N}"[..20], QuestionType.ShortText, "Notes?", "Notes ?", At, isActive: true);
			database.Questions.AddRange(question, narrative);
			var report = new Report(Locale.EnCa, At);
			report.AnswerChoices(question, question.CurrentRevision, [.. question.Choices.Select(choice => choice.Id)], At);
			report.Answer(narrative, "Windy launch", At);
			database.Reports.Add(report);
			await database.SaveChangesAsync();
			reportId = report.Id;
		}

		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await officer.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		// Then
		var answers = detail.GetProperty("answers").EnumerateArray().ToList();
		answers.Single(answer => answer.GetProperty("type").GetString() == "multi_select").GetProperty("values").EnumerateArray()
			.Select(value => (value.GetProperty("value").GetString(), value.GetProperty("pin").GetString()))
			.ShouldBe([("Gusty", "none"), ("Other", "last")], ignoreOrder: true);
		answers.Single(answer => answer.GetProperty("type").GetString() == "short_text").GetProperty("values")[0]
			.GetProperty("pin").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Theory]
	[InlineData("first")]
	[InlineData("last")]
	public async Task GivenChoice_WhenAdministratorPinsIt_ThenQuestionKeepsItsRevision(string pin)
	{
		// Given
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);
		var label = $"Which wing {Guid.NewGuid():N}?";
		using var created = await admin.PostAsJsonAsync(AdminQuestions, Save(label, null));
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var saved = await created.Content.ReadFromJsonAsync<JsonElement>();
		var id = saved.GetProperty("id").GetString();
		var revision = saved.GetProperty("revisionId").GetString();

		// When
		using var response = await admin.PutAsJsonAsync(new Uri($"{AdminQuestions}/{id}", UriKind.Relative), Save(label, pin));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var after = await response.Content.ReadFromJsonAsync<JsonElement>();
		after.GetProperty("revisionId").GetString().ShouldBe(revision);
		after.GetProperty("options").EnumerateArray()
			.Single(option => option.GetProperty("code").GetString() == "other").GetProperty("pin").GetString().ShouldBe(pin);
	}

	[Fact]
	public async Task GivenUnknownPin_WhenSaved_ThenRefused()
	{
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);

		using var response = await admin.PostAsJsonAsync(AdminQuestions, Save($"Which wing {Guid.NewGuid():N}?", "middle"));

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private static object Save(string label,
							   string? pin)
	{
		return new
		{
			key = (string?)null,
			type = "single_select",
			labelEn = label,
			labelFr = label,
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = new[]
			{
				new { code = "other", labelEn = "Other", labelFr = "Autre", pin },
				new { code = "paraglider", labelEn = "Paraglider", labelFr = "Parapente", pin = (string?)null },
			},
		};
	}
}
