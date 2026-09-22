using System.Net;
using System.Net.Http.Json;
using HpacSafety.Api.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>The three authentication endpoints.</summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class AuthEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Config = new("/api/auth/config", UriKind.Relative);
	private static readonly Uri Token = new("/api/auth/token", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Theory]
	[InlineData("admin", "admin", "administrator", "dev:admin")]
	[InlineData("officer", "officer", "safety_officer", "dev:officer")]
	[InlineData("user", "user", "user", "dev:user")]
	public async Task GivenDevelopmentCredentials_WhenTokenIsRequested_ThenItCarriesThatRole(
		string username, string password, string role, string subject)
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.PostAsJsonAsync(Token, new { username, password });
		var token = await response.Content.ReadFromJsonAsync<TokenResponse>();

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		token!.Role.ShouldBe(role);
		token.Subject.ShouldBe(subject);
		token.AccessToken.ShouldNotBeNullOrWhiteSpace();
		token.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task GivenCredentialsThatAreNotValid_WhenTokenIsRequested_ThenEveryFailureIsIdentical()
	{
		// Given — a wrong password, an unknown username, and nothing at all
		using var client = _factory.CreateClient();

		// When
		var problems = new List<string>();
		var statuses = new List<HttpStatusCode>();

		foreach (var (username, password) in new[]
				 {
					 ("admin", "wrong"),
					 ("nobody", "nobody"),
					 ("", "")
				 })
		{
			using var response = await client.PostAsJsonAsync(Token, new { username, password });
			statuses.Add(response.StatusCode);

			// Everything except traceId, which ASP.NET stamps per request and
			// which says nothing about the credentials.
			var problem = await response.Content.ReadFromJsonAsync<ProblemShape>();
			problems.Add($"{problem!.Type}|{problem.Title}|{problem.Detail}|{problem.Status}");
		}

		// Then — one and the same problem every time, so nothing distinguishes
		// an unknown username from a wrong password. A test that checked only
		// the status code would miss a detail message quietly saying which.
		statuses.ShouldAllBe(status => status == HttpStatusCode.Unauthorized);
		problems.Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
	}

	[Fact]
	public async Task GivenUnknownUsername_WhenTokenIsRequested_ThenResponseDoesNotEchoIt()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.PostAsJsonAsync(
			Token, new { username = "wing-commander", password = "hunter2" });
		var body = await response.Content.ReadAsStringAsync();

		// Then
		body.ShouldNotContain("wing-commander");
		body.ShouldNotContain("hunter2");
	}

	[Fact]
	public async Task GivenDevelopment_WhenAuthConfigIsRead_ThenThirdPartySignInIsOff()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(Config);
		var config = await response.Content.ReadFromJsonAsync<AuthConfigResponse>();

		// Then — this is how the browser knows to hide the button, instead of
		// baking the environment into its bundle.
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		config!.Mode.ShouldBe("development");
		config.ThirdPartySignIn.ShouldBeFalse();
		config.Authority.ShouldBeNull();
	}

	[Fact]
	public async Task GivenConfiguredProvider_WhenAuthConfigIsRead_ThenThirdPartySignInIsOffered()
	{
		// Given
		await using var configured = WithSettings(
			("HpacSafety:Authentication:Authority", "https://provider.example.test"));
		using var client = configured.CreateClient();

		// When
		using var response = await client.GetAsync(Config);
		var config = await response.Content.ReadFromJsonAsync<AuthConfigResponse>();

		// Then
		config!.Mode.ShouldBe("provider");
		config.ThirdPartySignIn.ShouldBeTrue();
	}

	[Fact]
	public async Task GivenNonDevelopmentEnvironment_WhenTokenEndpointIsCalled_ThenRouteDoesNotExist()
	{
		// Given — a production-shaped host, validating against a provider
		await using var production = _factory.WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Production");
			builder.UseSetting("HpacSafety:Authentication:Authority", "https://provider.example.test");
		});

		using var client = production.CreateClient();

		// When
		using var response = await client.PostAsJsonAsync(Token, new { username = "admin", password = "admin" });

		// Then — 404, not 401. There is no flag that turns this on in a
		// deployed environment, because there is no code path that maps it.
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAnonymousCaller_WhenAuthConfigIsRead_ThenItAnswers()
	{
		// Given — the sign-in page has to read this before anybody has a token
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(Config);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings)
	{
		return _factory.WithWebHostBuilder(builder =>
		{
			foreach (var (key, value) in settings)
			{
				builder.UseSetting(key, value);
			}
		});
	}

	private sealed record ProblemShape(string? Type, string? Title, string? Detail, int? Status);
}
