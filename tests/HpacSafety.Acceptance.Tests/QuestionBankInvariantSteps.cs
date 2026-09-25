using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
///     REQ-QB-027: the invariants every saved revision keeps. The authoring
///     clauses run over HTTP against the booted host, because the key's
///     uniqueness and the refusal a saving Administrator sees are the API's; the
///     consent clauses run against the domain, where the system questions' rules
///     live and where both consents can be built without depending on what the
///     booted database happens to hold. Every question here is synthetic.
/// </summary>
[Binding]
public sealed partial class QuestionBankInvariantSteps
{
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);
	private static readonly DateTimeOffset Noon = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	private HttpClient? _admin;
	private JsonElement _saved;

	[Given(@"an Administrator saves a new revision")]
	public async Task GivenAnAdministratorSavesANewRevision()
	{
		_admin = await BootedApi.SignedInAs(MemberRole.Administrator);

		using var response = await Save(Draft(UniqueKey(), "short_text"));
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		_saved = await response.Content.ReadFromJsonAsync<JsonElement>();
	}

	[Then(@"the stable key is a non-empty, unique, non-localized identifier")]
	public async Task ThenTheKeyIsAStableIdentifier()
	{
		var key = _saved.GetProperty("key").GetString();
		key.ShouldNotBeNullOrWhiteSpace();
		KeyShape().IsMatch(key).ShouldBeTrue($"'{key}' is an invariant identifier, not wording in either language");

		using var again = await Save(Draft(key, "short_text"));
		again.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a second live question cannot take the same key");
	}

	[Then(@"both English and French labels are present for an answer-producing question")]
	public async Task ThenBothLabelsArePresent()
	{
		_saved.GetProperty("labelEn").GetString().ShouldNotBeNullOrWhiteSpace();
		_saved.GetProperty("labelFr").GetString().ShouldNotBeNullOrWhiteSpace();

		using var englishOnly = await Save(Draft(UniqueKey(), "short_text") with { LabelFr = "   " });
		englishOnly.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a revision is never saved in one language");
	}

	[Then(@"a single-select or multi-select question has at least one live choice, a type-ahead may start with none, and every other type has none")]
	public async Task ThenChoicesFitTheType()
	{
		foreach (var type in new[] { "single_select", "multi_select" })
		{
			using var empty = await Save(Draft(UniqueKey(), type));
			empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"a {type} with no choice could not be answered");
		}

		using var typeAhead = await Save(Draft(UniqueKey(), "autocomplete"));
		typeAhead.StatusCode.ShouldBe(HttpStatusCode.Created, "reporters fill a type-ahead (ADR-0063)");

		// A choice sent with a type that takes none is dropped, not kept: the saved
		// text question offers nothing.
		using var textWithChoice = await Save(Draft(UniqueKey(), "short_text") with { Options = [new Option(null, "Glider", "Planeur")] });
		textWithChoice.StatusCode.ShouldBe(HttpStatusCode.Created, await textWithChoice.Content.ReadAsStringAsync());
		(await textWithChoice.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("options").GetArrayLength().ShouldBe(0);
	}

	[Then(@"only the publication-consent and media-consent questions may be marked system")]
	public async Task ThenOnlyTheConsentsAreSystem()
	{
		// By role, not key: the seeded publication consent keeps the key its
		// Typeform import gave it.
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var systemRoles = await database.Questions.AsNoTracking()
			.Where(question => question.IsSystem)
			.Select(question => question.Role)
			.ToListAsync();

		systemRoles.ShouldNotBeEmpty();
		systemRoles.ShouldAllBe(role => role == QuestionRole.ConsentPublish || role == QuestionRole.ConsentMedia);

		// No authoring route marks a question system: an ordinary one never is.
		_saved.GetProperty("isSystem").GetBoolean().ShouldBeFalse();
	}

#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.
	[Then(@"both consent questions stay active, yes\/no, and private, and no edit can make either one otherwise")]
	public void ThenBothConsentsStayActiveYesNoAndPrivate()
	{
		var consents = new[]
		{
			Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Noon),
			Question.CreateConsentMedia("May we show your files?", "Pouvons-nous montrer vos fichiers ?", Noon),
		};

		foreach (var consent in consents)
		{
			consent.IsSystem.ShouldBeTrue();
			consent.IsActive.ShouldBeTrue();
			consent.Type.ShouldBe(QuestionType.YesNo);
			consent.IsPrivate.ShouldBeTrue();

			var labelEn = consent.CurrentRevision.LabelEn;
			var labelFr = consent.CurrentRevision.LabelFr;

			Should.Throw<DomainRuleViolationException>(() =>
				consent.ApplyEdit(true, QuestionType.YesNo, labelEn, labelFr, false, true, 0, Noon, isRequired: true));
			Should.Throw<DomainRuleViolationException>(() =>
				consent.ApplyEdit(true, QuestionType.YesNo, labelEn, labelFr, true, false, 0, Noon, isRequired: true));
			Should.Throw<DomainRuleViolationException>(() =>
				consent.ApplyEdit(true, QuestionType.ShortText, labelEn, labelFr, true, true, 0, Noon, isRequired: true));

			consent.IsPrivate.ShouldBeTrue();
			consent.IsActive.ShouldBeTrue();
			consent.Type.ShouldBe(QuestionType.YesNo);
		}
	}
#pragma warning restore CA1822

	private async Task<HttpResponseMessage> Save(SaveQuestion request)
	{
		return await _admin!.PostAsJsonAsync(Questions, request);
	}

	private static string UniqueKey()
	{
		return $"synthetic_{Guid.NewGuid():N}"[..40];
	}

	private static SaveQuestion Draft(string? key,
									  string type)
	{
		return new SaveQuestion(key, type, "A synthetic question", "Une question synthétique", null, null, null, null,
			false, true, true, null, null, null, []);
	}

	[GeneratedRegex("^[a-z0-9_]+$")]
	private static partial Regex KeyShape();

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
		string? GroupedUnderQuestionId,
		IReadOnlyList<Option> Options);

	private sealed record Option(string? Code, string? LabelEn, string? LabelFr);
}
