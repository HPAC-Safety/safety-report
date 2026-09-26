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
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("unanswered"));
		var id = created.GetProperty("id").GetString()!;

		// When
		var edited = await Revise(client, id, "Reworded once");

		// Then
		edited.GetProperty("id").GetString().ShouldBe(id);
		edited.GetProperty("labelEn").GetString().ShouldBe("Reworded once");
	}

	[Fact]
	public async Task GivenAnsweredQuestion_WhenEdited_ThenRetiredAndReplaced()
	{
		// Given
		using var client = await SignedIn();
		var key = UniqueKey("answered");
		var created = await Create(client, key);
		var id = created.GetProperty("id").GetString()!;
		await Answer(id, "Wind picked up on final.");

		// When
		var edited = await Revise(client, id, "Reworded after an answer");

		// Then — a new question, carrying the same stable key
		edited.GetProperty("id").GetString().ShouldNotBe(id);
		edited.GetProperty("key").GetString().ShouldBe(key);
		edited.GetProperty("labelEn").GetString().ShouldBe("Reworded after an answer");

		// And exactly one live question answers to that key
		var live = await List(client);
		live.Count(question => question.GetProperty("key").GetString() == key).ShouldBe(1);
	}

	[Fact]
	public async Task GivenAnsweredQuestion_WhenOnlyItsChoicesAreEdited_ThenKeepsItsIdentifierAndRevision()
	{
		// Given — REQ-QB-099: choices live outside revisions (ADR-0095)
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("answered_choices"), "single_select");
		var id = created.GetProperty("id").GetString()!;
		await Answer(id, "Cooper's Hill");

		// When
		var edited = await SaveChoices(client, id, created.GetProperty("labelEn").GetString()!,
			new { code = "coopers", labelEn = "Cooper's Hill", labelFr = "Colline Cooper" },
			new { code = (string?)null, labelEn = "Mara", labelFr = "Mara" });

		// Then
		edited.GetProperty("id").GetString().ShouldBe(id);
		edited.GetProperty("revisionNumber").GetInt32().ShouldBe(1);
		edited.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("code").GetString())
			.ShouldBe(["coopers", "mara"]);
	}

	[Fact]
	public async Task GivenAnsweredQuestion_WhenReworded_ThenReplacementCarriesItsChoices()
	{
		// Given — REQ-QB-098
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("answered_fork_choices"), "single_select");
		var id = created.GetProperty("id").GetString()!;
		await Answer(id, "Cooper's Hill");

		// When
		var edited = await SaveChoices(client, id, "Reworded after an answer",
			new { code = "coopers", labelEn = "Cooper's Hill", labelFr = "Colline Cooper" });

		// Then
		edited.GetProperty("id").GetString().ShouldNotBe(id);
		edited.GetProperty("options").EnumerateArray().Single().GetProperty("code").GetString().ShouldBe("coopers");
	}

	[Fact]
	public async Task GivenAConditionOnAParentThatForked_WhenListed_ThenItStillNamesTheChoiceItWasGiven()
	{
		// Given — a condition on a single-select, whose parent is then answered and reworded (ADR-0071)
		using var client = await SignedIn();
		var parent = await Create(client, UniqueKey("answered_parent"), "single_select");
		var parentId = parent.GetProperty("id").GetString()!;
		var coopers = parent.GetProperty("options").EnumerateArray().Single().GetProperty("id").GetString()!;
		using var createdChild = await client.PostAsJsonAsync(Questions, new
		{
			key = UniqueKey("conditional_child"),
			type = "short_text",
			labelEn = "A conditional question",
			labelFr = "Une question conditionnelle",
			isRequired = false,
			isPrivate = true,
			isActive = true,
			dependsOnQuestionId = parentId,
			dependsOnChoiceId = coopers,
			options = Array.Empty<object>(),
		});
		createdChild.StatusCode.ShouldBe(HttpStatusCode.Created, await createdChild.Content.ReadAsStringAsync());
		var childId = (await createdChild.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
		await Answer(parentId, "Cooper's Hill");
		await SaveChoices(client, parentId, "Reworded after an answer",
			new { code = "coopers", labelEn = "Cooper's Hill", labelFr = "Colline Cooper" });

		// When — the parent the condition names is retired, so not among the live questions
		var admin = (await client.GetFromJsonAsync<JsonElement>(Questions)).EnumerateArray()
			.Single(question => question.GetProperty("id").GetString() == childId);
		using var reader = _factory.CreateClient();
		var shown = (await reader.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/questions/", UriKind.Relative))).EnumerateArray()
			.Single(question => question.GetProperty("id").GetString() == childId);

		// Then — the condition still names the choice it was given, unresolved
		admin.GetProperty("dependsOnChoiceId").GetString().ShouldBe(coopers);
		shown.GetProperty("dependsOnChoiceId").GetString().ShouldBe(coopers);
	}

	[Fact]
	public async Task GivenAnsweredQuestion_WhenDeleted_ThenRefusedAndKept()
	{
		// Given — REQ-QB-031: an answer records what somebody was asked
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("answered_delete"));
		var id = created.GetProperty("id").GetString()!;
		await Answer(id, "Wind gradient on short final.");

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var live = await List(client);
		live.ShouldContain(question => question.GetProperty("id").GetString() == id);
	}

	[Fact]
	public async Task GivenAnswerOnDeletedReport_WhenQuestionIsDeleted_ThenStillRefused()
	{
		// Given — REQ-QB-031: the reference check ignores the soft-delete filter,
		// so an answer hidden behind a deleted report still protects the question
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("deleted_report_delete"));
		var id = created.GetProperty("id").GetString()!;
		await Answer(id, "Thermal collapse over the ridge.", true);

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

		var live = await List(client);
		live.ShouldContain(question => question.GetProperty("id").GetString() == id);
	}

	[Fact]
	public async Task GivenUnansweredQuestion_WhenDeleted_ThenRemovedFromTheForm()
	{
		// Given — REQ-QB-030: nothing references it, so it can go
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("unanswered_delete"));
		var id = created.GetProperty("id").GetString()!;

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/questions/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		var live = await List(client);
		live.ShouldNotContain(question => question.GetProperty("id").GetString() == id);
	}

	[Fact]
	public async Task GivenAnswerOnDeletedReport_WhenQuestionIsEdited_ThenStillForks()
	{
		// Given — a deleted report is still a record of what somebody was asked
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("deleted_report"));
		var id = created.GetProperty("id").GetString()!;
		await Answer(id, "Gusting crosswind.", true);

		// When
		var edited = await Revise(client, id, "Reworded anyway");

		// Then
		edited.GetProperty("id").GetString().ShouldNotBe(id);
	}

	[Fact]
	public async Task GivenRetiredQuestion_WhenEditedAgain_ThenApiRefuses()
	{
		// Given
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("retired_by_fork"));
		var id = created.GetProperty("id").GetString()!;
		await Answer(id, "Thermal collapse.");
		await Revise(client, id, "Reworded once");

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
		using var client = await SignedIn(MemberRole.SafetyOfficer);

		// When
		using var response = await client.GetAsync(Awaiting);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenPickerAnswer_WhenRecorded_ThenReadsItsChoicesFrenchAndIsNeverQueued()
	{
		// Given — a picker's choices are written in both languages, so its
		// answer reads the other label from its choice (ADR-0128)
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("site"), "single_select");
		var id = created.GetProperty("id").GetString()!;

		// When
		var answerId = await Answer(id, "Cooper's Hill");

		// Then
		var queued = await client.GetFromJsonAsync<JsonElement>(Awaiting);
		queued.GetProperty("answers").EnumerateArray()
			.ShouldNotContain(answer => answer.GetProperty("id").GetString() == answerId);

		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.Include(a => a.Choice).SingleAsync(a => a.Id == TinyId.Parse(answerId));
		stored.Value.ShouldBeNull();
		stored.Text.ShouldBe("Cooper's Hill");
		stored.DisplayedTranslation.ShouldBe("Colline Cooper");
		stored.TranslationSource.ShouldBe(TranslationSource.Choice);
	}

	[Fact]
	public async Task GivenEmailAnswer_WhenAdministratorSuppliesTranslation_ThenRefused()
	{
		// Given — an email never has a second language (ADR-0112)
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("contact"), "email");
		var answerId = await Answer(created.GetProperty("id").GetString()!, "avery@example.test");

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{answerId}/translation", UriKind.Relative),
			new { value = "avery@example.test" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenNarrativeAnswer_WhenAdministratorSuppliesTranslation_ThenAcceptedWithHumanProvenance()
	{
		// Given — ADR-0080 widens the queue to every answer with a value, a
		// free-text one included, not just select-shaped ones
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("narrative"));
		var answerId = await Answer(created.GetProperty("id").GetString()!, "It all happened quickly.");

		var queued = await client.GetFromJsonAsync<JsonElement>(Awaiting);
		queued.GetProperty("answers").EnumerateArray()
			.ShouldContain(answer => answer.GetProperty("id").GetString() == answerId);

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{answerId}/translation", UriKind.Relative),
			new { value = "Tout s'est passé très vite." });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var stored = await database.ReportAnswers.SingleAsync(a => a.Id == TinyId.Parse(answerId));
		stored.Value.ShouldBe("It all happened quickly.");
		stored.TranslatedValue.ShouldBe("Tout s'est passé très vite.");
		stored.TranslationSource.ShouldBe(TranslationSource.Human);
	}

	[Fact]
	public async Task GivenAnswerAwaitingTranslation_WhenAdministratorReadsPendingCounts_ThenItIsCounted()
	{
		// Given — the collection runs one test at a time, so the difference is
		// exactly the answer this test recorded
		using var client = await SignedIn();
		var before = await PendingTranslationCount(client);
		var created = await Create(client, UniqueKey("counted"));
		await Answer(created.GetProperty("id").GetString()!, "It was windy.");

		// When
		var after = await PendingTranslationCount(client);

		// Then
		after.ShouldBe(before + 1);
		var queued = await client.GetFromJsonAsync<JsonElement>(Awaiting);
		after.ShouldBe(queued.GetProperty("waiting").GetInt32());
	}

	[Fact]
	public async Task GivenAnswerWithNoValue_WhenTranslationIsSupplied_ThenApiRefuses()
	{
		// Given — a skipped answer has nothing to translate
		using var client = await SignedIn();
		var created = await Create(client, UniqueKey("skipped"));
		var answerId = await Answer(created.GetProperty("id").GetString()!, value: null);

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/answers/{answerId}/translation", UriKind.Relative),
			new { value = "anything" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData("not-a-tiny-id")]
	[InlineData("aaaaaaaaaaa")]
	public async Task GivenUnknownAnswerId_WhenTranslationIsSupplied_ThenApiReturnsNotFound(string id)
	{
		// Given — a malformed id and a well-formed one that matches no answer
		using var client = await SignedIn();

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
	private static async Task<int> PendingTranslationCount(HttpClient client)
	{
		var counts = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/admin/counts", UriKind.Relative));
		return counts.GetProperty("answersAwaitingTranslation").GetInt32();
	}

	private async Task<string> Answer(string questionId,
									  string? value,
									  bool deleteReport = false)
	{
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var question = await database.Questions
			.Include(candidate => candidate.Revisions)
			.Include(candidate => candidate.AllChoices)
			.SingleAsync(candidate => candidate.Id == TinyId.Parse(questionId));

		var report = new Report(Locale.EnCa, At);
		var answer = report.Answer(question, value, At);

		database.Reports.Add(report);
		await database.SaveChangesAsync();

		if (deleteReport)
		{
			// What matters here is only that an answer behind a deleted report
			// still forces the fork (issue #82 built the real soft delete).
			report.SoftDelete(At);
			await database.SaveChangesAsync();
		}

		return answer.Id.ToString();
	}

	private Task<HttpClient> SignedIn(MemberRole role = MemberRole.Administrator)
	{
		return SignedInClient.As(_factory, role);
	}

	private static string UniqueKey(string prefix)
	{
		var key = $"{prefix}_{Guid.NewGuid():N}";
		return key[..Math.Min(key.Length, 40)];
	}

	private static async Task<JsonElement> Create(HttpClient client,
												  string key,
												  string type = "long_text")
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
				options,
			});

		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private static async Task<JsonElement> Revise(HttpClient client,
												  string id,
												  string labelEn)
	{
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative), Draft(labelEn));

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

		return await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	private static async Task<JsonElement> SaveChoices(HttpClient client,
													   string id,
													   string labelEn,
													   params object[] options)
	{
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/questions/{id}", UriKind.Relative),
			new
			{
				type = "single_select",
				labelEn,
				labelFr = "Une question synthétique",
				isRequired = false,
				isPrivate = true,
				isActive = true,
				options,
			});

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
			options = Array.Empty<object>(),
		};
	}

	private static async Task<List<JsonElement>> List(HttpClient client)
	{
		var body = await client.GetFromJsonAsync<JsonElement>(Questions);
		return [.. body.EnumerateArray()];
	}
}
