using System.Net;
using System.Net.Http.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core.Features.Moderation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The members-site-verified development login scenarios in
///     <c>features/moderation-authentication-and-publication/</c>. See
///     ADR-0079.
/// </summary>
/// <remarks>
///     Runs against the booted host, with the members-site transport stubbed
///     — never the real network, and no real credential is used. Detailed
///     coverage of the credential source itself (CSRF scraping, cookie
///     forwarding, every failure shape) lives in
///     <c>MembersSiteCredentialSourceTests</c> in <c>HpacSafety.Api.Tests</c>;
///     this proves the feature file's sentences are true of the running
///     system.
/// </remarks>
[Binding]
public sealed class MembersSiteLoginSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string LoginPageBody = """
		<html><body>
		<form action="/login" method="post">
		<input type="hidden" name="authenticity_token" value="csrf-token-abc123" />
		</form>
		</body></html>
		""";

	private string? _administratorEmail;
	private string? _safetyOfficerEmail;
	private string? _loginEmail;
	private HttpResponseMessage? _response;
	private HttpClient? _client;

	[Given(@"the development token endpoint is available")]
	public void GivenDevelopmentTokenEndpointAvailable()
	{
		// A marker for readability — every When step below boots a
		// Development-shaped host, where the route always exists.
	}

	[Given(@"{string} is on the development administrator list")]
	public void GivenEmailIsOnTheAdministratorList(string email)
	{
		_administratorEmail = email;
	}

	[Given(@"{string} is on the development safety-officer list")]
	public void GivenEmailIsOnTheSafetyOfficerList(string email)
	{
		_safetyOfficerEmail = email;
	}

	[Given(@"{string} is on neither development list")]
	public void GivenEmailIsOnNeitherList(string email)
	{
		_administratorEmail = null;
		_safetyOfficerEmail = null;
		_loginEmail = email;
	}

	[When(@"that email logs in with credentials the members site accepts")]
	public async Task WhenThatEmailLogsInSuccessfully()
	{
		var email = _administratorEmail ?? _safetyOfficerEmail ?? _loginEmail
			?? throw new InvalidOperationException("No email was set up by a preceding Given step.");

		var client = await BootedApi.MembersSiteStubbedAsync(
			new StubTransport(LoginPage(), Redirect()),
			administratorEmails: _administratorEmail is null ? null : [_administratorEmail],
			safetyOfficerEmails: _safetyOfficerEmail is null ? null : [_safetyOfficerEmail]);

		_response = await client.PostAsJsonAsync(
			"/api/auth/token", new { username = email, password = "whatever-the-stub-accepts" });
	}

	[When(@"a login is attempted with credentials the members site does not accept")]
	public async Task WhenLoginAttemptedWithBadCredentials()
	{
		var client = await BootedApi.MembersSiteStubbedAsync(new StubTransport(LoginPage(), LoginPage()));

		_response = await client.PostAsJsonAsync(
			"/api/auth/token", new { username = "nobody-special@example.test", password = "wrong-password" });
	}

	[When(@"the members site cannot be reached during a login attempt")]
	public async Task WhenMembersSiteCannotBeReached()
	{
		var client = await BootedApi.MembersSiteStubbedAsync(
			new StubTransport(new HttpRequestException("no route to host")));

		_response = await client.PostAsJsonAsync(
			"/api/auth/token", new { username = "member@example.test", password = "whatever" });
	}

	[Then(@"the API returns a signed development token with the {word} role")]
	public async Task ThenTokenHasRole(string role)
	{
		_response!.EnsureSuccessStatusCode();

		var payload = await _response.Content.ReadFromJsonAsync<TokenPayload>();
		var expectedRole = Enum.Parse<MemberRole>(role);

		payload!.Role.ShouldBe(MemberRoles.CodeFor(expectedRole));
	}

	[Then(@"the API returns one generic invalid-credentials failure")]
	public void ThenGenericInvalidCredentialsFailure()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Then(@"nothing distinguishes it from an unknown fixed development account")]
	public async Task ThenIndistinguishableFromFixedAccountFailure()
	{
		var body = await _response!.Content.ReadAsStringAsync();
		body.ShouldContain("invalid-credentials");
		body.ShouldNotContain("members");
	}

	[Then(@"the API reports the members site as unavailable")]
	public void ThenMembersSiteReportedUnavailable()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
	}

	[Then(@"it does not report invalid credentials")]
	public void ThenNotReportedAsInvalidCredentials()
	{
		_response!.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
	}

	// --- Repeated sign-in attempts for one identity are rate limited ---

	[Given(@"repeated sign-in attempts arrive for the same username")]
	public async Task GivenRepeatedSignInAttemptsForTheSameUsername()
	{
		var limited = (await BootedApi.RateLimitedAsync("SignIn")).CreateClient();

		// The one permit this policy allows — consumed here so the next attempt
		// is the one that exceeds it. A wrong password still consumes it: the
		// identity partition is keyed before credentials are checked.
		_response = await limited.PostAsJsonAsync(
			"/api/auth/token", new { username = "user", password = "wrong-password" });
		_client = limited;
	}

	[When(@"the sign-in rate limit for that identity is exceeded")]
	public async Task WhenTheSignInRateLimitForThatIdentityIsExceeded()
	{
		_response = await _client!.PostAsJsonAsync(
			"/api/auth/token", new { username = "user", password = "still-wrong" });
	}

	[Then(@"the API rejects further attempts with 429 and a safe retry signal")]
	public void ThenTheApiRejectsFurtherAttemptsWith429AndASafeRetrySignal()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
		_response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
	}

	[Then(@"the rejection does not reveal whether any attempted username or password was valid")]
	public async Task ThenTheRejectionDoesNotRevealCredentialValidity()
	{
		var body = await _response!.Content.ReadAsStringAsync();
		body.ShouldNotContain("user", Case.Insensitive);
		body.ShouldNotContain("password", Case.Insensitive);
	}

	private static HttpResponseMessage LoginPage()
	{
		return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LoginPageBody) };
	}

	private static HttpResponseMessage Redirect()
	{
		var response = new HttpResponseMessage(HttpStatusCode.Found);
		response.Headers.Location = new Uri("/dashboard", UriKind.Relative);
		return response;
	}

	private sealed record TokenPayload(string Role);

	/// <summary>Replays one canned response (or throws one exception) per call, in order.</summary>
	private sealed class StubTransport : HttpMessageHandler
	{
		private readonly Queue<object> _results;

		public StubTransport(params object[] results)
		{
			_results = new Queue<object>(results);
		}

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var result = _results.Dequeue();

			return result switch
			{
				Exception failure => throw failure,
				HttpResponseMessage response => Task.FromResult(response),
				_ => throw new InvalidOperationException("Unexpected stubbed result.")
			};
		}
	}
}
