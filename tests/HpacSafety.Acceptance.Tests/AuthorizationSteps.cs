using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The moderation scenarios that describe what the API refuses over HTTP.
/// </summary>
/// <remarks>
///     These run against the booted host rather than the domain, because that is
///     what they are about: "the API rejects the operation regardless of what the
///     UI would have shown" cannot be shown by calling a domain method. Detailed
///     coverage — every rejected token shape, every role at every endpoint — lives
///     in <c>HpacSafety.Api.Tests</c>; these prove the feature file's sentences are
///     true of the running system.
/// </remarks>
[Binding]
public sealed class AuthorizationSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri DevelopmentToken = new("/api/auth/token", UriKind.Relative);
	private static readonly Uri Health = new("/health", UriKind.Relative);

	private HttpClient? _client;
	private bool _productionShaped;
	private HttpResponseMessage? _response;
	private MemberRole _role;
	private readonly List<HttpResponseMessage> _reviews = [];

	[Given(@"the API is not running in development")]
	public async Task GivenProductionShapedHost()
	{
		var host = await BootedApi.ProductionShaped();
		_client = host.CreateClient();
		_client.DefaultRequestHeaders.Add(BootedApi.ProductionOriginSecretHeader, BootedApi.ProductionOriginSecret);
		_productionShaped = true;
	}

	[Given(@"the API is not running in development and no identity provider is configured")]
	public async Task GivenProductionShapedHostWithNoAuthority()
	{
		var host = await BootedApi.ProductionShapedWithNoAuthority();
		_client = host.CreateClient();
		_client.DefaultRequestHeaders.Add(BootedApi.ProductionOriginSecretHeader, BootedApi.ProductionOriginSecret);
		_productionShaped = true;
	}

	[When(@"the health endpoint is requested")]
	public async Task WhenHealthEndpointIsRequested()
	{
		_response = await _client!.GetAsync(Health);
	}

	[Then(@"the API answers 200")]
	public void ThenApiAnswers200()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[When(@"a request carrying a bearer token reaches an authorization-protected endpoint")]
	public async Task WhenBearerTokenReachesProtectedEndpoint()
	{
		// Any bearer token — not a real one. Nothing outside Development with
		// no Authority configured can validate any token, forged or genuine.
		_client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "any-bearer-token-value");
		_response = await _client.GetAsync(Questions);
	}

	[Given(@"a request carries no bearer token")]
	public async Task GivenNoBearerToken()
	{
		var host = await BootedApi.Factory();
		_client = host.CreateClient();
	}

	[Given(@"a member without the required role calls an admin operation")]
	public async Task GivenMemberWithoutRequiredRole()
	{
		// A User is signed in and proven to be a member. That is exactly the
		// case the UI would hide the Admin menu for — and hiding it is not the
		// boundary (ADR-0048).
		_role = MemberRole.User;
		_client = await BootedApi.SignedInAs(_role);
		_response = await _client.GetAsync(Questions);
	}

	// {word}, not (User|SafetyOfficer|Administrator): Reqnroll reads this as a
	// Cucumber Expression, where parentheses mean "optional text" rather than
	// alternation, so the regex form silently matches nothing.
	[Given(@"a member has the {word} role")]
	public async Task GivenMemberHasRole(string role)
	{
		_role = Enum.Parse<MemberRole>(role);
		_client = await BootedApi.SignedInAs(_role);
	}

	[When(@"the development token endpoint is called")]
	public async Task WhenDevelopmentTokenEndpointIsCalled()
	{
		_response = await _client!.PostAsJsonAsync(DevelopmentToken, new { username = "admin", password = "admin" });
	}

	[When(@"it reaches an admin endpoint")]
	public async Task WhenItReachesAnAdminEndpoint()
	{
		_response = await _client!.GetAsync(Questions);
	}

	[When(@"the API processes the request")]
	public void WhenTheApiProcessesTheRequest()
	{
		// The Given already made the call; this step is the sentence's grammar.
		_response.ShouldNotBeNull();
	}

	[When(@"^that member attempts to (submit an occurrence report|list the review queue|read a report's private detail|obtain an attachment link|edit a report's summary|publish a report|unpublish a report|delete a report|create a question revision|edit a question's choices)$")]
	public async Task WhenMemberAttemptsCapability(string capability)
	{
		// One representative endpoint call per capability, each on its own fresh
		// synthetic row so a call is judged on its own and none depends on another's
		// side effect. Rows are seeded straight into the database; only the call
		// under test is made as the member.
		_response = capability switch
		{
			"submit an occurrence report" => await Submit(),
			"list the review queue" => await _client!.GetAsync(new Uri("/api/admin/reports", UriKind.Relative)),
			"read a report's private detail" => await _client!.GetAsync(new Uri($"/api/admin/reports/{await BootedReports.Seed(ReportStatus.Pending, true)}", UriKind.Relative)),
			"obtain an attachment link" => await ObtainAttachmentLink(),
			"edit a report's summary" => await EditSummary(),
			"publish a report" => await Review(ReportStatus.Pending, "publish", version => new { version }),
			"unpublish a report" => await Review(ReportStatus.Published, "unpublish", version => new { version, note = "Synthetic note: duplicate." }),
			"soft-delete a report" => await _client!.DeleteAsync(new Uri($"/api/admin/reports/{await BootedReports.Seed(ReportStatus.Pending, true)}", UriKind.Relative)),
			"create a question revision" => await CreateQuestion(),
			"edit a question's choices" => await EditChoices(),
			_ => throw new ArgumentOutOfRangeException(nameof(capability), capability, "Not a capability the role outlines name."),
		};
	}

	private async Task<HttpResponseMessage> Submit()
	{
		return await _client!.PostAsJsonAsync(
			new Uri("/api/v1/reports", UriKind.Relative),
			new
			{
				language = "en-CA",
				answers = new object[] { new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (bool?)false } },
			});
	}

	private async Task<HttpResponseMessage> ObtainAttachmentLink()
	{
		TinyId fileId = default;
		var reportId = await BootedReports.Seed(ReportStatus.Pending, true, report => fileId = BootedReports.AddProcessedImage(report).Id);

		return await _client!.GetAsync(new Uri($"/api/admin/reports/{reportId}/attachments/{fileId}/view", UriKind.Relative));
	}

	private async Task<HttpResponseMessage> EditSummary()
	{
		var reportId = await BootedReports.Seed(ReportStatus.Pending, true);

		return await _client!.PutAsJsonAsync(
			new Uri($"/api/admin/reports/{reportId}/summary", UriKind.Relative),
			new { version = await VersionOf(reportId), aiSummaryEn = "An edited English summary.", aiSummaryFr = "Un résumé français modifié." });
	}

	private async Task<HttpResponseMessage> Review(ReportStatus from,
												   string command,
												   Func<string, object> body)
	{
		var reportId = await BootedReports.Seed(from, true);

		return await _client!.PostAsJsonAsync(
			new Uri($"/api/admin/reports/{reportId}/{command}", UriKind.Relative), body(await VersionOf(reportId)));
	}

	/// <summary>The report's current version, read as a SafetyOfficer so a stale one is never the reason for a refusal.</summary>
	private static async Task<string> VersionOf(string reportId)
	{
		using var officer = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var detail = await officer.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		return detail.GetProperty("version").GetString()!;
	}

	private async Task<HttpResponseMessage> CreateQuestion()
	{
		return await _client!.PostAsJsonAsync(
			Questions,
			new
			{
				key = $"acceptance_{Guid.NewGuid():n}"[..24],
				type = "short_text",
				labelEn = "An acceptance question",
				labelFr = "Une question d'acceptation",
				isPrivate = true,
				isRequired = false,
				isActive = true,
			});
	}

	private async Task<HttpResponseMessage> EditChoices()
	{
		static object Request(params string[] choices) => new
		{
			key = (string?)null,
			type = "single_select",
			labelEn = "Where did you launch?",
			labelFr = "D'où avez-vous décollé?",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = choices.Select(choice => new { code = (string?)null, labelEn = choice, labelFr = choice }),
		};

		using var administrator = await BootedApi.SignedInAs(MemberRole.Administrator);
		using var created = await administrator.PostAsJsonAsync(Questions, Request("North ridge"));
		created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
		var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

		return await _client!.PutAsJsonAsync(new Uri($"{Questions}/{id}", UriKind.Relative), Request("North ridge", "South ridge"));
	}

	[When(@"that member approves, corrects, merges, relinks, or removes a reporter-added type-ahead value")]
	public async Task WhenMemberReviewsTypeAheadValues()
	{
		// Five reporter-added values on one synthetic type-ahead whose values depend
		// on a synthetic make, one per review action, so each call is judged on its
		// own (ADR-0129, ADR-0151).
		TinyId[] values;
		TinyId ozone;
		await using (var scope = (await BootedApi.Factory()).Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var now = DateTimeOffset.UtcNow;
			var make = Question.Create(
				$"acceptance_make_{Guid.NewGuid():n}"[..28], QuestionType.SingleSelect, "Make?", "Marque ?", now, isActive: true,
				options: [new QuestionOptionInput("niviuk", "Niviuk", "Niviuk"), new QuestionOptionInput("ozone", "Ozone", "Ozone")]);
			var niviuk = make.Choice("niviuk")!.Id;
			ozone = make.Choice("ozone")!.Id;
			var question = Question.Create(
				$"acceptance_site_{Guid.NewGuid():n}"[..28], QuestionType.Autocomplete, "Where?", "Où ?",
				now, isActive: true, choicesDependOnQuestionId: make.Id);
			values = [.. new[] { "Approve me", "Correct me", "Merge me", "Remove me", "Relink me" }
				.Select(typed => question.AddChoiceFromReporter(typed, Locale.EnCa, now, niviuk).Id)];
			database.Questions.Add(make);
			database.Questions.Add(question);
			await database.SaveChangesAsync();
		}

		Uri Value(TinyId id, string suffix = "") => new($"/api/admin/type-ahead-values/{id}{suffix}", UriKind.Relative);

		_reviews.Add(await _client!.PostAsync(Value(values[0], "/approval"), null));
		_reviews.Add(await _client.PutAsJsonAsync(Value(values[1]), new { labelEn = "Corrected", labelFr = "Corrigé" }));
		_reviews.Add(await _client.PostAsJsonAsync(Value(values[2], "/merge"), new { intoId = values[0].Value }));
		_reviews.Add(await _client.DeleteAsync(Value(values[3])));
		_reviews.Add(await _client.PutAsJsonAsync(Value(values[4], "/parent"), new { parentChoiceIds = new[] { ozone.Value } }));
	}

	[Then(@"the route does not exist")]
	public void ThenRouteDoesNotExist()
	{
		_productionShaped.ShouldBeTrue();

		// 404, not 401: nothing maps the route outside Development, so there
		// is no flag anybody could set wrong.
		_response!.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the API refuses it before the handler runs")]
	public void ThenRefusedBeforeHandler()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Then(@"the API rejects the operation regardless of what the UI would have shown")]
	public async Task ThenRejectedRegardlessOfUi()
	{
		// 403, not 401: they are signed in, and it is still not theirs.
		_response!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

		var body = await _response.Content.ReadAsStringAsync();
		body.ShouldContain("insufficient-role");
	}

	[Then(@"the API {word} the attempt")]
	public async Task ThenApiOutcome(string outcome)
	{
		if (_reviews.Count > 0)
		{
			foreach (var review in _reviews)
			{
				if (outcome == "allows")
				{
					review.IsSuccessStatusCode.ShouldBeTrue($"an {_role} should be able to review a type-ahead value, but the API answered {review.StatusCode}.");
				}
				else
				{
					review.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
				}
			}

			return;
		}

		if (outcome is "accepts" or "allows")
		{
			_response!.IsSuccessStatusCode.ShouldBeTrue(
				$"a {_role} should be allowed, but the API answered {_response.StatusCode}: {await _response.Content.ReadAsStringAsync()}");
			return;
		}

		// Forbidden rather than merely "not success": a 401 here would mean the
		// token was not accepted at all, which is a different failure and would
		// let this scenario pass for the wrong reason.
		_response!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
}
