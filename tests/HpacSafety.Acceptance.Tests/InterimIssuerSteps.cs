using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core.Features.Moderation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The temporary interim issuer scenarios (issue #648, ADR-0172) in
///     <c>features/moderation-authentication-and-publication/</c>.
/// </summary>
/// <remarks>
///     Runs against the booted host, with the members-site transport stubbed
///     the same way <see cref="MembersSiteLoginSteps" /> does — never the real
///     network. Detailed coverage of the issuer itself (RS256 signature,
///     mutual exclusion with a configured Authority, the JWKS shape) lives in
///     <c>HpacSafety.Api.Tests</c>; this proves the feature file's sentences
///     are true of the running system. Deleted along with the whole feature
///     once ADR-0172's removal plan runs.
/// </remarks>
[Binding]
public sealed class InterimIssuerSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string AdministratorEmail = "interim-issuer-acceptance-admin@example.test";

	private const string LoginPageBody = """
										 <html><body>
										 <form action="/login" method="post">
										 <input type="hidden" name="authenticity_token" value="csrf-token-abc123" />
										 </form>
										 </body></html>
										 """;

	private HttpClient? _client;
	private HttpResponseMessage? _response;
	private TokenPayload? _issuedToken;

	[Given(@"the API is not running in development and the temporary interim issuer is enabled")]
	public async Task GivenInterimIssuerEnabled()
	{
		// Four scripted responses: one successful members-site round trip for
		// the allowlisted administrator, then one rejected round trip (a
		// re-rendered login page) for "admin"/"admin" — the members site has
		// no such account either, so there is no back door even if
		// FixedAccountCredentialSource were mistakenly reachable.
		var host = await BootedApi.ProductionShapedWithInterimIssuer(
			new StubTransport(LoginPage(), Redirect(), LoginPage(), LoginPage()),
			administratorEmails: [AdministratorEmail]);
		_client = host.CreateClient();
	}

	[Given(@"the API is not running in development and the temporary interim issuer is disabled")]
	public async Task GivenInterimIssuerDisabled()
	{
		// The default: no flag, no Authority — production's own configuration.
		var host = await BootedApi.ProductionShapedWithNoAuthority();
		_client = host.CreateClient();
	}

	[When(@"a member signs in with credentials the members site accepts")]
	public async Task WhenMemberSignsInWithMembersSiteCredentials()
	{
		EnsureOriginHeader();

		_response = await _client!.PostAsJsonAsync(
			"/api/auth/token", new { username = AdministratorEmail, password = "whatever-the-stub-accepts" });
		_response.EnsureSuccessStatusCode();
		_issuedToken = await _response.Content.ReadFromJsonAsync<TokenPayload>();
	}

	[Then(@"the API issues a token the API itself accepts")]
	public async Task ThenTheApiIssuesATokenTheApiItselfAccepts()
	{
		_issuedToken.ShouldNotBeNull();

		_client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _issuedToken.AccessToken);

		using var me = await _client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));
		me.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Then(@"an allowlisted administrator account's token carries the Administrator role")]
	public void ThenTokenCarriesAdministratorRole()
	{
		_issuedToken!.Role.ShouldBe(MemberRoles.CodeFor(MemberRole.Administrator));
	}

	[When(@"a sign-in is attempted with the fixed development administrator account")]
	public async Task WhenSignInAttemptedWithFixedDevelopmentAdministratorAccount()
	{
		// FixedAccountCredentialSource is never registered for the interim
		// issuer (the owner's security review on issue #648) — admin/admin
		// exists only under Development. The stubbed members-site transport
		// here would reject it too (a re-rendered login page), so nothing
		// this host recognizes accepts this pair.
		EnsureOriginHeader();
		_response = await _client!.PostAsJsonAsync("/api/auth/token", new { username = "admin", password = "admin" });
	}

	[Then(@"the API refuses it")]
	public void ThenTheApiRefusesIt()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[When(@"the interim issuer's discovery document is requested")]
	public async Task WhenDiscoveryDocumentIsRequested()
	{
		EnsureOriginHeader();
		_response = await _client!.GetAsync(new Uri("/api/auth/interim/.well-known/openid-configuration", UriKind.Relative));
	}

	[When(@"the interim issuer's JWKS is requested")]
	public async Task WhenJwksIsRequested()
	{
		EnsureOriginHeader();
		_response = await _client!.GetAsync(new Uri("/api/auth/interim/jwks", UriKind.Relative));
	}

	[When(@"a token is requested from the token endpoint")]
	public async Task WhenTokenIsRequestedFromTheTokenEndpoint()
	{
		EnsureOriginHeader();
		_response = await _client!.PostAsJsonAsync("/api/auth/token", new { username = "admin", password = "admin" });
	}

	/// <summary>
	///     Every request against a Production-shaped host needs this header
	///     (ADR-0159); added once, on first use, so a scenario touching several
	///     of these <c>When</c> steps in sequence does not add it twice.
	/// </summary>
	private void EnsureOriginHeader()
	{
		if (!_client!.DefaultRequestHeaders.Contains(BootedApi.ProductionOriginSecretHeader))
		{
			_client.DefaultRequestHeaders.Add(BootedApi.ProductionOriginSecretHeader, BootedApi.ProductionOriginSecret);
		}
	}

	[Then(@"the API answers 404")]
	public void ThenApiAnswers404()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the response carries only a public key, never a private key field")]
	public async Task ThenResponseCarriesOnlyAPublicKey()
	{
		var body = await _response!.Content.ReadAsStringAsync();

		body.ShouldContain("\"kty\":\"RSA\"");
		body.ShouldContain("\"n\":");
		body.ShouldContain("\"e\":");
		body.ShouldNotContain("\"d\":"); // the RSA private exponent
	}

	private static HttpResponseMessage LoginPage()
	{
		return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LoginPageBody) };
	}

	private static HttpResponseMessage Redirect()
	{
		return new HttpResponseMessage(HttpStatusCode.Found);
	}

	private sealed record TokenPayload(string AccessToken, string Role);

	/// <summary>
	///     A fixed script of responses for the members-site's two calls — the
	///     same shape as <c>MembersSiteLoginSteps</c>'s own stub, kept as its
	///     own copy because this type is temporary (ADR-0172) and deletes with
	///     it.
	/// </summary>
	private sealed class StubTransport : HttpMessageHandler
	{
		private readonly Queue<HttpResponseMessage> _responses;

		public StubTransport(params HttpResponseMessage[] responses)
		{
			_responses = new Queue<HttpResponseMessage>(responses);
		}

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			return Task.FromResult(_responses.Dequeue());
		}
	}
}
