using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The temporary interim issuer scenarios (issue #648, ADR-0172) in
///     <c>.spec/features/authentication-and-roles/</c>.
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

	private const string MemberEmail = "interim-issuer-acceptance@example.test";

	private HttpClient? _client;
	private HttpResponseMessage? _response;
	private TokenPayload? _issuedToken;
	private readonly RoleAssignmentWorld _world;

	/// <summary>Reqnroll hands each scenario its own role-assignment world, which builds the deployment.</summary>
	public InterimIssuerSteps(RoleAssignmentWorld world)
	{
		_world = world;
	}

	[Given(@"a deployment outside development with the temporary interim issuer enabled")]
	public async Task GivenInterimIssuerEnabled()
	{
		// The deployment is the role-assignment world's: outside Development, the interim
		// issuer on, the members site stubbed to accept any email address and refuse the
		// fixed development accounts, which have no "@" — so there is no back door even if
		// FixedAccountCredentialSource were mistakenly reachable.
		_client = await _world.AnonymousClient();
	}

	[Given(@"a deployment outside development with the temporary interim issuer disabled")]
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
			"/api/auth/token", new { username = MemberEmail, password = "whatever-the-stub-accepts" });
		_response.EnsureSuccessStatusCode();
		_issuedToken = await _response.Content.ReadFromJsonAsync<TokenPayload>();
	}

	[Then(@"the member receives a token that later requests are accepted with")]
	public async Task ThenTheApiIssuesATokenTheApiItselfAccepts()
	{
		_issuedToken.ShouldNotBeNull();

		_client!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _issuedToken.AccessToken);

		using var me = await _client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));
		me.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[When(@"a sign-in is attempted with the fixed development Administrator account")]
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

	[Then(@"the sign-in is refused")]
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

	[When(@"a token is requested from the interim issuer")]
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

	[Then(@"it is not found")]
	public void ThenApiAnswers404()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the response carries only a public key, never a private key")]
	public async Task ThenResponseCarriesOnlyAPublicKey()
	{
		var body = await _response!.Content.ReadAsStringAsync();

		body.ShouldContain("\"kty\":\"RSA\"");
		body.ShouldContain("\"n\":");
		body.ShouldContain("\"e\":");
		body.ShouldNotContain("\"d\":"); // the RSA private exponent
	}

	private sealed record TokenPayload(string AccessToken, string Role);
}
