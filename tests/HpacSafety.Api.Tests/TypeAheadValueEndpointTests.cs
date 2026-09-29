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
///     The type-ahead review endpoints: who may use them, what each review does to
///     the value, and that it is audited (ADR-0129). Every value here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class TypeAheadValueEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Awaiting = new("/api/admin/type-ahead-values/awaiting-review", UriKind.Relative);
	private static readonly Uri Counts = new("/api/admin/counts", UriKind.Relative);
	private static readonly DateTimeOffset At = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Theory]
	[InlineData(MemberRole.User, HttpStatusCode.Forbidden)]
	[InlineData(MemberRole.SafetyOfficer, HttpStatusCode.OK)]
	[InlineData(MemberRole.Administrator, HttpStatusCode.OK)]
	public async Task GivenRole_WhenListingValuesAwaitingReview_ThenOnlyReviewersAreAllowed(MemberRole role,
		HttpStatusCode expected)
	{
		// Given
		using var client = await SignedInClient.As(_factory, role);

		// When
		using var response = await client.GetAsync(Awaiting);

		// Then
		response.StatusCode.ShouldBe(expected);
	}

	[Fact]
	public async Task GivenAReporterAddedValue_WhenListed_ThenItCarriesItsQuestionLanguageAndAnswerCount()
	{
		// Given
		var (question, value) = await TypeAheadWithReporterValue("Mount 7", answers: 2);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var body = await client.GetFromJsonAsync<JsonElement>(Awaiting);

		// Then
		var listed = body.GetProperty("values").EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == value.Value);
		listed.GetProperty("questionId").GetString().ShouldBe(question.Value);
		listed.GetProperty("labelEn").GetString().ShouldBe("Mount 7");
		listed.GetProperty("labelFr").ValueKind.ShouldBe(JsonValueKind.Null);
		listed.GetProperty("typedIn").GetString().ShouldBe("en-CA");
		listed.GetProperty("answerCount").GetInt32().ShouldBe(2);
		listed.GetProperty("isRemoved").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task GivenAFlaggedValue_WhenASafetyOfficerApprovesIt_ThenItIsReviewedAndAudited()
	{
		// Given
		var (_, value) = await TypeAheadWithReporterValue("Hidden Valley");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.PostAsync(new Uri($"/api/admin/type-ahead-values/{value}/approval", UriKind.Relative), null);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var stored = await Stored(value);
		stored.NeedsReview.ShouldBeFalse();
		stored.ReviewedBy.ShouldNotBeNullOrWhiteSpace();
		(await Audited(value, AuditAction.ApprovedTypeAheadValue)).ShouldBeTrue();
	}

	[Fact]
	public async Task GivenAFlaggedValue_WhenCorrected_ThenEveryAnswerReadsTheCorrection()
	{
		// Given
		var (_, value) = await TypeAheadWithReporterValue("coopers", answers: 2);
		using var client = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		using var response = await client.PutAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{value}", UriKind.Relative), new { labelEn = "Cooper's", labelFr = "" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var answers = await database.ReportAnswers.AsNoTracking().Include(answer => answer.Choice)
			.Where(answer => answer.ChoiceId == value).ToListAsync();
		answers.Count.ShouldBe(2);
		answers.ShouldAllBe(answer => answer.Text == "Cooper's");
		(await Audited(value, AuditAction.CorrectedTypeAheadValue)).ShouldBeTrue();
	}

	[Fact]
	public async Task GivenAFlaggedValue_WhenRemoved_ThenItIsNoLongerOfferedButStillNamed()
	{
		// Given
		var (question, value) = await TypeAheadWithReporterValue("Test site", answers: 1);
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/type-ahead-values/{value}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await Stored(value)).Deleted.ShouldNotBeNull();
		using var anonymous = _factory.CreateClient();
		var form = await anonymous.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/questions/", UriKind.Relative));
		form.EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == question.Value)
			.GetProperty("options").EnumerateArray()
			.ShouldNotContain(option => option.GetProperty("id").GetString() == value.Value);
		(await Audited(value, AuditAction.RemovedTypeAheadValue)).ShouldBeTrue();
	}

	[Fact]
	public async Task GivenTwoValues_WhenOneIsMergedIntoTheOther_ThenItsAnswersReadTheTargetAndItIsAudited()
	{
		// Given
		var (question, coopers) = await TypeAheadWithReporterValue("Coopers", answers: 2);
		TinyId target;
		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var loaded = await database.Questions.Include(q => q.Revisions).Include(q => q.AllChoices).SingleAsync(q => q.Id == question);
			var report = new Report(Locale.EnCa, At);
			report.Answer(loaded, "Cooper's", At);
			database.Reports.Add(report);
			await database.SaveChangesAsync();
			target = loaded.AllChoices.Single(choice => choice.LabelEn == "Cooper's").Id;
		}

		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var listed = await client.GetFromJsonAsync<JsonElement>(Awaiting);
		listed.GetProperty("values").EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == coopers.Value)
			.GetProperty("mergeTargets").EnumerateArray().Select(entry => entry.GetProperty("id").GetString())
			.ShouldBe([target.Value]);

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{coopers}/merge", UriKind.Relative), new { intoId = target.Value });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await Stored(coopers)).MergedIntoChoiceId.ShouldBe(target);
		(await Audited(coopers, AuditAction.MergedTypeAheadValue)).ShouldBeTrue();

		await using var reader = _factory.Services.CreateAsyncScope();
		var answers = await reader.ServiceProvider.GetRequiredService<HpacSafetyDbContext>().ReportAnswers.AsNoTracking()
			.Include(answer => answer.Choice).ThenInclude(choice => choice!.MergedInto)
			.Where(answer => answer.ChoiceId == coopers).ToListAsync();
		answers.Count.ShouldBe(2);
		answers.ShouldAllBe(answer => answer.Text == "Cooper's");
	}

	[Fact]
	public async Task GivenAMergedDependentValue_WhenTypedAgainUnderAnotherParentChoice_ThenItIsListedForReviewWithItsAlias()
	{
		// Given — "Mentr 7" was merged into "Mentor 7", offered under "Niviuk"
		// only (ADR-0151). A merge reviews both sides at once, so the survivor
		// needs a later, independent reason to be flagged again for review.
		TinyId question;
		TinyId mentor7;
		TinyId ozone;
		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var make = Question.Create(
				$"make_{Guid.NewGuid():N}"[..20], QuestionType.SingleSelect, "Make", "Marque", At, isActive: true,
				options: [new QuestionOptionInput("niviuk", "Niviuk", "Niviuk"), new QuestionOptionInput("ozone", "Ozone", "Ozone")]);
			database.Questions.Add(make);
			var niviuk = make.Choice("niviuk")!.Id;
			ozone = make.Choice("ozone")!.Id;

			var model = Question.Create(
				$"model_{Guid.NewGuid():N}"[..20], QuestionType.Autocomplete, "Model", "Modèle", At, isActive: true,
				choicesDependOnQuestionId: make.Id);
			var mentr7 = model.AddChoiceFromReporter("Mentr 7", Locale.EnCa, At, niviuk);
			var mentorChoice = model.AddChoiceFromReporter("Mentor 7", Locale.EnCa, At.AddMinutes(1), niviuk);
			model.MergeValue(mentr7.Id, mentorChoice.Id, "synthetic-safety-officer", At.AddMinutes(2));
			database.Questions.Add(model);
			await database.SaveChangesAsync();
			question = model.Id;
			mentor7 = mentorChoice.Id;
		}

		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var model = await database.Questions
				.Include(q => q.Revisions).Include(q => q.AllChoices).ThenInclude(choice => choice.ParentLinks)
				.SingleAsync(q => q.Id == question);
			var report = new Report(Locale.EnCa, At.AddMinutes(3));
			report.Answer(model, model.CurrentRevision, "Mentr 7", At.AddMinutes(3), ozone);
			database.Reports.Add(report);
			await database.SaveChangesAsync();
		}

		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var listed = await client.GetFromJsonAsync<JsonElement>(Awaiting);

		// Then — flagged again, offered under both parent choices now, and
		// showing the merged-away wording as its alias.
		var entry = listed.GetProperty("values").EnumerateArray().Single(candidate => candidate.GetProperty("id").GetString() == mentor7.Value);
		var aliases = entry.GetProperty("aliases").EnumerateArray().ToList();
		aliases.ShouldHaveSingleItem();
		aliases[0].GetProperty("labelEn").GetString().ShouldBe("Mentr 7");
	}

	[Fact]
	public async Task GivenAMergedValue_WhenTheCurrentFormIsRead_ThenTheSurvivorCarriesTheMergedWordingAsAnAliasAndTheMergedValueIsNeverOffered()
	{
		// Given
		var (question, coopers) = await TypeAheadWithReporterValue("Coopers");
		TinyId target;
		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var loaded = await database.Questions.Include(q => q.Revisions).Include(q => q.AllChoices).SingleAsync(q => q.Id == question);
			var report = new Report(Locale.EnCa, At);
			report.Answer(loaded, "Cooper's", At);
			database.Reports.Add(report);
			await database.SaveChangesAsync();
			target = loaded.AllChoices.Single(choice => choice.LabelEn == "Cooper's").Id;
		}

		using var reviewer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		await reviewer.PostAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{coopers}/merge", UriKind.Relative), new { intoId = target.Value });

		// When
		using var anonymous = _factory.CreateClient();
		var form = await anonymous.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/questions/", UriKind.Relative));

		// Then
		var options = form.EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == question.Value)
			.GetProperty("options").EnumerateArray().ToList();
		options.ShouldNotContain(option => option.GetProperty("id").GetString() == coopers.Value);
		var survivor = options.Single(option => option.GetProperty("id").GetString() == target.Value);
		survivor.GetProperty("aliases").EnumerateArray().Select(alias => alias.GetProperty("labelEn").GetString()).ShouldBe(["Coopers"]);
	}

	[Fact]
	public async Task GivenAMergeNamingNoValue_WhenSent_ThenRefused()
	{
		// Given
		var (_, value) = await TypeAheadWithReporterValue("Nowhere yet");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri($"/api/admin/type-ahead-values/{value}/merge", UriKind.Relative), new { intoId = "nope" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenAPickerOption_WhenReviewed_ThenRefused()
	{
		// Given — a picker option is an Administrator's to fix or replace (ADR-0128)
		TinyId option;
		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var question = Question.Create(
				$"wing_{Guid.NewGuid():N}"[..20], QuestionType.SingleSelect, "Wing", "Aile", At, isActive: true,
				options: [new QuestionOptionInput("paraglider", "Paraglider", "Parapente")]);
			database.Questions.Add(question);
			await database.SaveChangesAsync();
			option = question.Choices.Single().Id;
		}

		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.PostAsync(new Uri($"/api/admin/type-ahead-values/{option}/approval", UriKind.Relative), null);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenAnUnknownValue_WhenReviewed_ThenNotFound()
	{
		// Given
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var malformed = await client.DeleteAsync(new Uri("/api/admin/type-ahead-values/not-an-id", UriKind.Relative));
		using var unknown = await client.DeleteAsync(new Uri($"/api/admin/type-ahead-values/{TinyId.New()}", UriKind.Relative));

		// Then
		malformed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAFlaggedValue_WhenASafetyOfficerReadsTheCounts_ThenItIsCounted()
	{
		// Given
		await TypeAheadWithReporterValue("Ridge nobody listed");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var counts = await client.GetFromJsonAsync<JsonElement>(Counts);

		// Then — the translation count stays an Administrator's alone (REQ-MOD-085)
		counts.GetProperty("typeAheadValuesAwaitingReview").GetInt32().ShouldBeGreaterThan(0);
		counts.GetProperty("answersAwaitingTranslation").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	/// <summary>A live type-ahead with one reporter-added value, named by the given number of answers.</summary>
	private async Task<(TinyId Question, TinyId Value)> TypeAheadWithReporterValue(string typed,
																				   int answers = 1)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var question = Question.Create(
			$"site_{Guid.NewGuid():N}"[..20], QuestionType.Autocomplete, "Where?", "Où ?", At, isActive: true);
		database.Questions.Add(question);

		for (var i = 0; i < answers; i++)
		{
			var report = new Report(Locale.EnCa, At);
			report.Answer(question, typed, At);
			database.Reports.Add(report);
		}

		await database.SaveChangesAsync();
		return (question.Id, question.AllChoices.Single().Id);
	}

	private async Task<QuestionChoice> Stored(TinyId value)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.QuestionChoices.AsNoTracking().SingleAsync(choice => choice.Id == value);
	}

	private async Task<bool> Audited(TinyId value,
									 AuditAction action)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.AuditLog.AnyAsync(entry => entry.TargetId == value && entry.Action == action);
	}
}
