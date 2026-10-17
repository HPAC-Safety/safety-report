using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.RoleAssignments;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
	public async Task GivenAnActiveAdministratorAssignment_WhenTokenIsRequestedThroughTheMembersSite_ThenItCarriesTheAdministratorRole()
	{
		// Given
		var email = UniqueEmail();
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		await Assign(host, email, MemberRole.Administrator);
		using var client = OriginVerifiedClient(host);

		// When
		var token = await SignIn(client, email);

		// Then
		token.Role.ShouldBe("administrator");
		token.AccessToken.ShouldNotBeNullOrWhiteSpace();

		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
		jwt.Header.Alg.ShouldBe("RS256");
		jwt.Issuer.ShouldBe(InterimTokenIssuer.IssuerName);
	}

	[Theory]
	[InlineData("{0}", "{0}")]
	[InlineData("{0}", " {0} ")]
	public async Task GivenAnAssignmentTypedInAnotherCase_WhenTheMemberSignsIn_ThenTheAssignmentMatches(string assigned,
																											string typed)
	{
		// Given — assigned in one form, typed in upper case with spaces.
		var email = UniqueEmail();
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		await Assign(host, string.Format(System.Globalization.CultureInfo.InvariantCulture, assigned, email), MemberRole.SafetyOfficer);
		using var client = OriginVerifiedClient(host);

		// When
		var token = await SignIn(client, string.Format(System.Globalization.CultureInfo.InvariantCulture, typed, email).ToUpperInvariant());

		// Then
		token.Role.ShouldBe("safety_officer");
	}

	[Fact]
	public async Task GivenAnEmailOnTheDevelopmentLists_WhenTheInterimIssuerMintsAToken_ThenTheListsAreIgnored()
	{
		// Given — the lists are Development's alone (ADR-0199).
		var email = UniqueEmail();
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()), listedAdministrator: email);
		using var client = OriginVerifiedClient(host);

		// When
		var token = await SignIn(client, email);

		// Then
		token.Role.ShouldBe("user");
	}

	[Fact]
	public async Task GivenTheSuperAdministrator_WhenTheyMintAToken_ThenItCarriesTheAdministratorRole()
	{
		// Given
		var email = UniqueEmail();
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()), superAdmin: email);
		using var client = OriginVerifiedClient(host);

		// When
		var token = await SignIn(client, email.ToUpperInvariant());

		// Then
		token.Role.ShouldBe("administrator");
	}

	[Fact]
	public async Task GivenRoleAssignmentsCannotBeRead_WhenMembersSignIn_ThenAMemberIsAUserAndTheSuperAdministratorAnAdministrator()
	{
		// Given
		var assigned = UniqueEmail();
		var superAdmin = UniqueEmail();
		await using var host = InterimHost(
			new StubTransport(LoginPage(), Redirect(), LoginPage(), Redirect()),
			superAdmin: superAdmin);
		await Assign(host, assigned, MemberRole.SafetyOfficer);
		using var client = OriginVerifiedClient(host);

		TokenResponse member;
		TokenResponse super;

		// When
		await using (await Unreadable.Begin(host))
		{
			member = await SignIn(client, assigned);
			super = await SignIn(client, superAdmin);
		}

		// Then
		member.Role.ShouldBe("user");
		super.Role.ShouldBe("administrator");
	}

	[Fact]
	public async Task GivenSignInsAndAnUnreadableStore_WhenMembersAreResolved_ThenNoEmailOrMemberSubjectReachesALog()
	{
		// Given — CON-IF-011: the interim subject is member:<email>
		var assigned = UniqueEmail();
		var stranger = UniqueEmail();
		var logs = new CapturedLogs();
		await using var host = InterimHost(
			new StubTransport(LoginPage(), Redirect(), LoginPage(), Redirect(), LoginPage(), Redirect()),
			configure: services => services.AddLogging(logging => logging.AddProvider(logs)));
		await Assign(host, assigned, MemberRole.SafetyOfficer);
		using var client = OriginVerifiedClient(host);

		// When
		var token = await SignIn(client, assigned);
		await SignIn(client, stranger);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

		await using (await Unreadable.Begin(host))
		{
			await SignIn(client, assigned);
			using var forbidden = await client.GetAsync(new Uri("/api/admin/counts", UriKind.Relative), TestContext.Current.CancellationToken);
			forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}

		// Then
		logs.Messages.ShouldNotBeEmpty();
		logs.Messages.ShouldNotContain(message => message.Contains(assigned, StringComparison.OrdinalIgnoreCase)
												  || message.Contains(stranger, StringComparison.OrdinalIgnoreCase)
												  || message.Contains("member:", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task GivenARevokedAssignment_WhenTheMemberAsksWithTheTokenTheyHold_ThenTheRequestIsForbidden()
	{
		// Given — a Safety Officer by assignment, holding a token.
		var email = UniqueEmail();
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		await Assign(host, email, MemberRole.SafetyOfficer);
		using var client = OriginVerifiedClient(host);
		var token = await SignIn(client, email);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

		using var before = await client.GetAsync(new Uri("/api/admin/counts", UriKind.Relative), TestContext.Current.CancellationToken);

		// When — an Administrator removes the assignment.
		await Revoke(host, email);
		using var after = await client.GetAsync(new Uri("/api/admin/counts", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		before.StatusCode.ShouldBe(HttpStatusCode.OK);
		after.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenAnEmailWithNoAssignment_WhenTokenIsRequestedThroughTheMembersSite_ThenItCarriesTheUserRole()
	{
		// Given — the members site accepts the login, but the email holds no
		// role assignment.
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		using var client = OriginVerifiedClient(host);

		// When
		using var response = await client.PostAsJsonAsync(new Uri("/api/auth/token", UriKind.Relative), new { username = "nobody-special@example.test", password = "correct-password" }, cancellationToken: TestContext.Current.CancellationToken);
		var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		token!.Role.ShouldBe("user");
	}

	[Theory]
	[InlineData("", "correct-password")]
	[InlineData("someone@example.test", "")]
	public async Task GivenABlankCredential_WhenTokenIsRequested_ThenItIsRefusedWithoutAskingTheMembersSite(string username,
																									   string password)
	{
		// Given
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		using var client = OriginVerifiedClient(host);

		// When
		using var response = await client.PostAsJsonAsync(new Uri("/api/auth/token", UriKind.Relative), new { username, password }, cancellationToken: TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
		using var response = await client.PostAsJsonAsync(new Uri("/api/auth/token", UriKind.Relative), new { username = "admin", password = "admin" }, cancellationToken: TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenInterimIssuerEnabled_WhenTheIssuedTokenIsValidatedAgainstThisHostsOwnRules_ThenItIsAccepted()
	{
		// Given — the whole round trip: mint through the endpoint, validate
		// with the same parameters this host itself registers.
		await using var host = InterimHost(new StubTransport(LoginPage(), Redirect()));
		using var client = OriginVerifiedClient(host);

		using var response = await client.PostAsJsonAsync(new Uri("/api/auth/token", UriKind.Relative), new { username = "officer@example.test", password = "correct-password" }, cancellationToken: TestContext.Current.CancellationToken);
		var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: TestContext.Current.CancellationToken);

		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

		// When — any endpoint requiring only membership.
		using var me = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), TestContext.Current.CancellationToken);

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
		using var response = await client.GetAsync(new Uri("/api/auth/interim/.well-known/openid-configuration", UriKind.Relative), TestContext.Current.CancellationToken);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);

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
		using var response = await client.GetAsync(new Uri("/api/auth/interim/jwks", UriKind.Relative), TestContext.Current.CancellationToken);
		var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

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
		using var discovery = await client.GetAsync(new Uri("/api/auth/interim/.well-known/openid-configuration", UriKind.Relative), TestContext.Current.CancellationToken);
		using var jwks = await client.GetAsync(new Uri("/api/auth/interim/jwks", UriKind.Relative), TestContext.Current.CancellationToken);
		using var token = await client.PostAsJsonAsync(new Uri("/api/auth/token", UriKind.Relative), new { username = "admin", password = "admin" }, cancellationToken: TestContext.Current.CancellationToken);

		// Then — 404, not 401: there is no code path that maps any of these
		// without the flag.
		discovery.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		jwks.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		token.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	private WebApplicationFactory<Program> InterimHost(
		HttpMessageHandler membersSiteHandler,
		string? superAdmin = null,
		string? listedAdministrator = null,
		Action<IServiceCollection>? configure = null)
	{
		return _factory.WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Production");
			builder.UseSetting("HpacSafety:Security:OriginVerification:Secret", OriginSecret);
			builder.UseSetting("HpacSafety:Authentication:InterimIssuer:Enabled", "true");
			builder.UseSetting("HpacSafety:Authentication:InterimIssuer:SigningKeyPem", SigningKeyPem);

			if (superAdmin is not null)
			{
				builder.UseSetting("HpacSafety:Authentication:SuperAdmin", superAdmin);
			}

			if (listedAdministrator is not null)
			{
				builder.UseSetting("MembersSiteLogin:AdministratorEmails:0", listedAdministrator);
			}

			builder.ConfigureTestServices(services =>
			{
				services
					.AddHttpClient(MembersSiteCredentialSource.HttpClientName)
					.ConfigurePrimaryHttpMessageHandler(() => membersSiteHandler);

				configure?.Invoke(services);
			});
		});
	}

	private static string UniqueEmail()
	{
		return $"{Guid.NewGuid():n}@example.com";
	}

	private static async Task<TokenResponse> SignIn(HttpClient client,
													string username)
	{
		using var response = await client.PostAsJsonAsync(new Uri("/api/auth/token", UriKind.Relative), new { username, password = "correct-password" }, cancellationToken: TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		return (await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: TestContext.Current.CancellationToken))!;
	}

	private static async Task Assign(WebApplicationFactory<Program> host,
									 string email,
									 MemberRole role)
	{
		await using var scope = host.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		database.RoleAssignments.Add(RoleAssignment.Grant(email, role, "seed", DateTimeOffset.UtcNow));
		await database.SaveChangesAsync(TestContext.Current.CancellationToken);
	}

	private static async Task Revoke(WebApplicationFactory<Program> host,
									 string email)
	{
		await using var scope = host.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var normalised = RoleAssignment.Normalize(email);

		await database.Database.ExecuteSqlInterpolatedAsync(
			$"UPDATE role_assignments SET revoked_at = now(), revoked_by_subject = 'seed' WHERE email = {normalised} AND revoked_at IS NULL",
			TestContext.Current.CancellationToken);
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
