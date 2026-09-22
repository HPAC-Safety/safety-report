using System.Net;
using System.Net.Http.Json;
using HpacSafety.Core.Features.Moderation;
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

	private HttpClient? _client;
	private bool _productionShaped;
	private HttpResponseMessage? _response;
	private MemberRole _role;

	[Given(@"the API is not running in development")]
	public async Task GivenProductionShapedHost()
	{
		var host = await BootedApi.ProductionShapedAsync();
		_client = host.CreateClient();
		_productionShaped = true;
	}

	[Given(@"a request carries no bearer token")]
	public async Task GivenNoBearerToken()
	{
		var host = await BootedApi.FactoryAsync();
		_client = host.CreateClient();
	}

	[Given(@"an authenticated member without the required role calls an admin operation")]
	public async Task GivenMemberWithoutRequiredRole()
	{
		// A User is signed in and proven to be a member. That is exactly the
		// case the UI would hide the Admin menu for — and hiding it is not the
		// boundary (ADR-0048).
		_role = MemberRole.User;
		_client = await BootedApi.SignedInAsAsync(_role);
		_response = await _client.GetAsync(Questions);
	}

	// {word}, not (User|SafetyOfficer|Administrator): Reqnroll reads this as a
	// Cucumber Expression, where parentheses mean "optional text" rather than
	// alternation, so the regex form silently matches nothing.
	[Given(@"a member has the {word} role")]
	public async Task GivenMemberHasRole(string role)
	{
		_role = Enum.Parse<MemberRole>(role);
		_client = await BootedApi.SignedInAsAsync(_role);
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

	[When(@"that member attempts to create a question revision")]
	public async Task WhenMemberAttemptsToCreateRevision()
	{
		_response = await _client!.PostAsJsonAsync(
			Questions,
			new
			{
				key = $"acceptance_{Guid.NewGuid():n}"[..24],
				type = "short_text",
				labelEn = "An acceptance question",
				labelFr = "Une question d'acceptation",
				isPrivate = true,
				isRequired = false,
				isActive = true
			});
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
	public void ThenApiOutcome(string outcome)
	{
		if (outcome == "accepts")
		{
			_response!.IsSuccessStatusCode.ShouldBeTrue(
				$"an {_role} should be able to author a question, but the API answered {_response.StatusCode}.");
			return;
		}

		// Forbidden rather than merely "not success": a 401 here would mean the
		// token was not accepted at all, which is a different failure and would
		// let this scenario pass for the wrong reason.
		_response!.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
}
