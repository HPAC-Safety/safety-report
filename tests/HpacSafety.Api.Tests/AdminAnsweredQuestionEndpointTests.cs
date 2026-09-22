using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Editing a question that has been answered, and the queue of answers waiting
///     for a second official language. See ADR-0071 and ADR-0072.
/// </summary>
/// <remarks>
///     These tests write a report answer directly, because no submission endpoint
///     exists yet. Every value here is synthetic.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class AdminAnsweredQuestionEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri Awaiting = new("/api/admin/answers/awaiting-translation", UriKind.Relative);
	private static readonly DateTimeOffset At = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenUnansweredQuestion_WhenEdited_ThenKeepsItsIdentifier()
	{
		// Given
		using var client = await SignedInAsync();
		var created = await CreateAsync(client, UniqueKey("unanswered"));
		var id = created.GetProperty("id").GetString()!;

		// When
		var edited = await ReviseAsync(client, id, "Reworded once");

		// Then
		edited.GetProperty("id").GetString().ShouldBe(id);
		edited.GetProperty("labelEn").GetString().ShouldBe("Reworded once");
	}

	[Fact]
	public async Task GivenAnsweredQuestion_WhenEdited_ThenRetiredAndReplaced()
	{
		// Given
		using var client = await SignedInAsync();
		var key = UniqueKey("answered");
		var created = await CreateAsync(client, key);
		var id = created.GetProperty("id").GetString()!;
		await AnswerAsync(id, "Wind picked up on final.");

		// When
		var edited = await ReviseAsync(client, id, "Reworded after an answer");

		// Then — a new question, carrying the same stable key
		edited.GetProperty("id").GetString().ShouldNotBe(id);
		edited.GetProperty("key").GetString().ShouldBe(key);
		edited.GetProperty("labelEn").GetString().ShouldBe("Reworded after an answer");

		// And exactly one live question answers to that key
		var live = await ListAsync(client);
		live.Count(question => question.GetProperty("key").GetString() == key).ShouldBe(1);
	}

	[Fact]
	public async Task GivenAnswerOnDeletedReport_WhenQuestionIsEdited_ThenStillForks()
	{
		// Given — a deleted report is still a record of what somebody was asked
		using var client = await SignedInAsync();
		var created = await CreateAsync(client, UniqueKey("deleted_report"));
		var id = created.GetProperty("id").GetString()!;
		await AnswerAsync(id, "Gusting crosswind.", true);

		// When
		var edited = await ReviseAsync(client, id, "Reworded anyway");

		// Then
		edited.GetProperty("id").GetString().ShouldNotBe(id);
	}

	[Fact]
	public async Task GivenRetiredQuestion_WhenEditedAgain_ThenApiRefuses()
	{
		// Given
		using var client = await SignedInAsync();
		var created = await CreateAsync(client, UniqueKey("retired_by_fork"));
		var id = created.GetProperty("id").GetString()!;
		await AnswerAsync(id, "Thermal collapse.");
		await ReviseAsync(client, id, "Reworded once");

		// When — the original is retired, and there is no undelete
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative), Draft("Reworded twice"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenNoMemberSession_WhenQueueIsRead_ThenApiRefuses()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(Awaiting);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenSafetyOfficerSession_WhenQueueIsRead_ThenApiRefuses()
	{
		// Given — the queue spans reports, so it is an Administrator's screen
		using var client = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await client.GetAsync(Awaiting);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenFlaggedAnswer_WhenAdministratorSuppliesTranslation_ThenItLeavesTheQueue()
	{
		// Given
		using var client = await SignedInAsync();
		var created = await CreateAsync(client, UniqueKey("site"), "single_select");
		var id = created.GetProperty("id").GetString()!;
		var answerId = await AnswerAsync(id, "Cooper's Hill");

		var queued = await client.GetFromJsonAsync<JsonElement>(Awaiting);
		queued.GetProperty("answers").EnumerateArray()
			.ShouldContain(answer => answer.GetProperty("id").GetString() == answerId);

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{answerId}/translation", UriKind.Relative),
			new { value = "Colline Cooper" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var after = await client.GetFromJsonAsync<JsonElement>(Awaiting);
		after.GetProperty("answers").EnumerateArray()
			.ShouldNotContain(answer => answer.GetProperty("id").GetString() == answerId);

		// And the reporter's own value is untouched
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.SingleAsync(a => a.Id == TinyId.Parse(answerId));
		stored.Value.ShouldBe("Cooper's Hill");
		stored.TranslatedValue.ShouldBe("Colline Cooper");
	}

	[Fact]
	public async Task GivenAnswerNotAwaitingTranslation_WhenOneIsSupplied_ThenApiRefuses()
	{
		// Given — a free-text answer is never translated
		using var client = await SignedInAsync();
		var created = await CreateAsync(client, UniqueKey("narrative"));
		var answerId = await AnswerAsync(created.GetProperty("id").GetString()!, "It all happened quickly.");

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{answerId}/translation", UriKind.Relative),
			new { value = "Tout s'est passé très vite." });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData("not-a-tiny-id")]
	[InlineData("aaaaaaaaaaa")]
	public async Task GivenUnknownAnswerId_WhenTranslationIsSupplied_ThenApiReturnsNotFound(string id)
	{
		// Given — a malformed id and a well-formed one that matches no answer
		using var client = await SignedInAsync();

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{id}/translation", UriKind.Relative),
			new { value = "Colline Cooper" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	/// <summary>
	///     Writes one report answer straight to the database, because there is no
	///     submission endpoint to post one through yet.
	/// </summary>
	private async Task<string> AnswerAsync(string questionId, string value, bool deleteReport = false)
	{
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var question = await database.Questions
			.Include(candidate => candidate.Revisions)
			.ThenInclude(revision => revision.Options)
			.SingleAsync(candidate => candidate.Id == TinyId.Parse(questionId));

		var report = new Report(Locale.EnCa, At);
		var answer = report.Answer(question, value, At);

		database.Reports.Add(report);
		await database.SaveChangesAsync();

		if (deleteReport)
		{
			// Report deletion is specified but not built yet, so the soft-delete
			// stamp is written directly. What matters here is only that an
			// answer behind a deleted report still forces the fork.
			await database.Reports
				.IgnoreQueryFilters()
				.Where(candidate => candidate.Id == report.Id)
				.ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.Deleted, At));

			await database.ReportAnswers
				.IgnoreQueryFilters()
				.Where(candidate => candidate.ReportId == report.Id)
				.ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.Deleted, At));
		}

		return answer.Id.ToString();
	}

	private Task<HttpClient> SignedInAsync(MemberRole role = MemberRole.Administrator)
	{
		return SignedInClient.AsAsync(_factory, role);
	}

	private static string UniqueKey(string prefix)
	{
		var key = $"{prefix}_{Guid.NewGuid():N}";
		return key[..Math.Min(key.Length, 40)];
	}

	private static async Task<JsonElement> CreateAsync(HttpClient client, string key, string type = "long_text")
	{
		var options = type is "single_select"
			? new[] { new { code = "coopers", labelEn = "Cooper's Hill", labelFr = "Colline Cooper" } }
			: [];

		using var response = await client.PostAsJsonAsync(
			Questions,
			new
			{
				key,
				type,
				labelEn = "A synthetic question",
				labelFr = "Une question synthétique",
				isRequired = false,
				isPrivate = true,
				isActive = true,
				options
			});

		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private static async Task<JsonElement> ReviseAsync(HttpClient client, string id, string labelEn)
	{
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative), Draft(labelEn));

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private static object Draft(string labelEn)
	{
		return new
		{
			type = "long_text",
			labelEn,
			labelFr = "Une question synthétique",
			isRequired = false,
			isPrivate = true,
			isActive = true,
			options = Array.Empty<object>()
		};
	}

	private static async Task<List<JsonElement>> ListAsync(HttpClient client)
	{
		var body = await client.GetFromJsonAsync<JsonElement>(Questions);
		return [.. body.EnumerateArray()];
	}
}
