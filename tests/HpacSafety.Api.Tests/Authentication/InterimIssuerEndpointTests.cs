using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using HpacSafety.Api.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     The temporary interim issuer's HTTP surface (issue #648, ADR-0172): its
///     discovery document, its JWKS, and <c>/api/auth/token</c> reused with an
///     RS256 signature. Everything here is deleted in one sweep with the
///     feature it tests.
/// </summary>
/// <remarks>
///     The interim issuer never registers <c>FixedAccountCredentialSource</c>
///     — the owner's security review on issue #648: staging's public address
///     must never accept the fixed development accounts
///     (<c>admin</c>/<c>admin</c> and friends), only a real members-site
///     login checked against the allowlists in <c>MembersSiteLoginOptions</c>.
///     Every sign-in test here stubs that HTTP call — never the live site.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class InterimIssuerEndpointTests(ApiPostgresFixture fixture)
{
	private const string OriginHeaderName = "X-Origin-Verify";
	private const string OriginSecret = "interim-issuer-test-origin-secret";

	private const string LoginPageBody = """
										 <html><body>
										 <form action="/login" method="post">
										 <input type="hidden" name="authenticity_token" value="csrf-token-abc123" />
										 </form>
										 </body></html>
										 """;

	private static readonly string SigningKeyPem = GenerateRsaPem();

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenAnEmailOnTheAdministratorAllowlist_WhenTokenIsRequestedThroughTheMembersSite_ThenItCarriesTheAdministratorRole()
	{
		// Given
		await using var host = InterimHost(
			new StubTransport(LoginPage(), Redirect()),
			administratorEmails: ["administrator@example.test"]);
		using var client = OriginVerifiedClient(host);

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri("/api/auth/token", UriKind.Relative),
			new { username = "administrator@example.test", password = "correct-password" });
		var token = await response.Content.ReadFromJsonAsync<TokenResponse>();

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		token!.Role.ShouldBe("administrator");
		token.AccessToken.ShouldNotBeNullOrWhiteSpace();

		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
		jwt.Header.Alg.ShouldBe("RS256");
		jwt.Issuer.ShouldBe(InterimTokenIssuer.IssuerName);
	}

	[Fact]
	public async Task GivenAnEmailOnNeitherAllowlist_WhenTokenIsRequestedThroughTheMembersSite_ThenItCarriesTheUserRole()
	{
		// Given — the members site accepts the login, but the email is on
		// neither MembersSiteLoginOptions list.
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		using var client = OriginVerifiedClient(host);

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri("/api/auth/token", UriKind.Relative),
			new { username = "nobody-special@example.test", password = "correct-password" });
		var token = await response.Content.ReadFromJsonAsync<TokenResponse>();

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		token!.Role.ShouldBe("user");
	}

	[Fact]
	public async Task GivenTheFixedDevelopmentAdminAccount_WhenTokenIsRequested_ThenItIsRefused()
	{
		// Given — admin/admin exists only under Development's
		// FixedAccountCredentialSource, never registered for the interim
		// issuer. The members-site stub here would reject this login anyway
		// (a re-rendered login page), proving there is no back door: nothing
		// here recognizes this pair as valid at all.
		await using var host = InterimHost(new StubTransport(LoginPage(), LoginPage()));
		using var client = OriginVerifiedClient(host);

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri("/api/auth/token", UriKind.Relative), new { username = "admin", password = "admin" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenInterimIssuerEnabled_WhenTheIssuedTokenIsValidatedAgainstThisHostsOwnRules_ThenItIsAccepted()
	{
		// Given — the whole round trip: mint through the endpoint, validate
		// with the same parameters this host itself registers.
		await using var host = InterimHost(
			new StubTransport(LoginPage(), Redirect()),
			safetyOfficerEmails: ["officer@example.test"]);
		using var client = OriginVerifiedClient(host);

		using var response = await client.PostAsJsonAsync(
			new Uri("/api/auth/token", UriKind.Relative),
			new { username = "officer@example.test", password = "correct-password" });
		var token = await response.Content.ReadFromJsonAsync<TokenResponse>();

		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

		// When — any endpoint requiring only membership.
		using var me = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative));

		// Then
		me.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GivenInterimIssuerEnabled_WhenDiscoveryDocumentIsRequested_ThenItPublishesTheInterimIssuer()
	{
		// Given
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		using var client = OriginVerifiedClient(host);

		// When
		using var response = await client.GetAsync(
			new Uri("/api/auth/interim/.well-known/openid-configuration", UriKind.Relative));
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		body.GetProperty("issuer").GetString().ShouldBe(InterimTokenIssuer.IssuerName);
		body.GetProperty("jwks_uri").GetString().ShouldEndWith("/api/auth/interim/jwks");
		body.GetProperty("token_endpoint").GetString().ShouldEndWith("/api/auth/token");
		body.GetProperty("id_token_signing_alg_values_supported")[0].GetString().ShouldBe("RS256");
	}

	[Fact]
	public async Task GivenInterimIssuerEnabled_WhenJwksIsRequested_ThenItPublishesOnlyThePublicKey()
	{
		// Given
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		using var client = OriginVerifiedClient(host);

		// When
		using var response = await client.GetAsync(new Uri("/api/auth/interim/jwks", UriKind.Relative));
		var body = await response.Content.ReadAsStringAsync();

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		body.ShouldNotContain("\"d\":"); // the RSA private exponent — never published
		body.ShouldContain("\"kty\":\"RSA\"");
		body.ShouldContain("\"n\":");
		body.ShouldContain("\"e\":");
	}

	[Fact]
	public async Task GivenInterimIssuerDisabled_WhenInterimEndpointsAreRequested_ThenTheyDoNotExist()
	{
		// Given — production's own configuration: no flag set at all.
		await using var disabled = _factory.WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Production");
			builder.UseSetting("HpacSafety:Security:OriginVerification:Secret", OriginSecret);
		});
		using var client = OriginVerifiedClient(disabled);

		// When
		using var discovery = await client.GetAsync(
			new Uri("/api/auth/interim/.well-known/openid-configuration", UriKind.Relative));
		using var jwks = await client.GetAsync(new Uri("/api/auth/interim/jwks", UriKind.Relative));
		using var token = await client.PostAsJsonAsync(
			new Uri("/api/auth/token", UriKind.Relative), new { username = "admin", password = "admin" });

		// Then — 404, not 401: there is no code path that maps any of these
		// without the flag.
		discovery.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		jwks.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		token.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	private WebApplicationFactory<Program> InterimHost(
		HttpMessageHandler membersSiteHandler,
		IReadOnlyList<string>? administratorEmails = null,
		IReadOnlyList<string>? safetyOfficerEmails = null)
	{
		return _factory.WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Production");
			builder.UseSetting("HpacSafety:Security:OriginVerification:Secret", OriginSecret);
			builder.UseSetting("HpacSafety:Authentication:InterimIssuer:Enabled", "true");
			builder.UseSetting("HpacSafety:Authentication:InterimIssuer:SigningKeyPem", SigningKeyPem);

			builder.ConfigureTestServices(services =>
			{
				services
					.AddHttpClient(MembersSiteCredentialSource.HttpClientName)
					.ConfigurePrimaryHttpMessageHandler(() => membersSiteHandler);
			});

			var emailSettings = new Dictionary<string, string?>();

			for (var index = 0; index < (administratorEmails?.Count ?? 0); index++)
			{
				emailSettings[$"MembersSiteLogin:AdministratorEmails:{index}"] = administratorEmails![index];
			}

			for (var index = 0; index < (safetyOfficerEmails?.Count ?? 0); index++)
			{
				emailSettings[$"MembersSiteLogin:SafetyOfficerEmails:{index}"] = safetyOfficerEmails![index];
			}

			foreach (var (key, value) in emailSettings)
			{
				builder.UseSetting(key, value);
			}
		});
	}

	private static HttpClient OriginVerifiedClient(WebApplicationFactory<Program> host)
	{
		var client = host.CreateClient();
		client.DefaultRequestHeaders.Add(OriginHeaderName, OriginSecret);
		return client;
	}

	private static string GenerateRsaPem()
	{
		using var rsa = RSA.Create(2048);
		return rsa.ExportPkcs8PrivateKeyPem();
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

	private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string Subject, string Role);

	/// <summary>
	///     A fixed script of responses for the members-site's two calls (GET
	///     the login page, POST the credentials) — the same shape
	///     <c>MembersSiteCredentialSourceTests</c>' own stub uses, kept as its
	///     own copy since this type is temporary (ADR-0172) and deletes with
	///     the feature it tests.
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
