using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     A Production-shaped host with no <c>HpacSafety:Authentication:Authority</c>
///     configured — the environment ADR-0158 describes: it still starts and
///     serves its public endpoints, but no bearer token can ever validate. See
///     issue #647.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class UnconfiguredAuthorityEndpointTests(ApiPostgresFixture fixture)
{
	private const string OriginHeaderName = "X-Origin-Verify";
	private const string OriginSecret = "unconfigured-authority-test-origin-secret";

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public void GivenProductionEnvironmentWithNoAuthority_WhenTheHostStarts_ThenItDoesNotThrow()
	{
		// Given
		using var unconfigured = ProductionHostWithNoAuthority();

		// When — accessing Services forces the deferred host to build, running
		// Program.cs's top-level statements for the first time.
		var starting = () => unconfigured.Services;

		// Then
		Should.NotThrow(starting);
	}

	[Fact]
	public async Task GivenProductionEnvironmentWithNoAuthority_WhenHealthIsRequested_ThenReturnsOk()
	{
		// Given
		await using var unconfigured = ProductionHostWithNoAuthority();
		using var client = OriginVerifiedClient(unconfigured);

		// When
		using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GivenProductionEnvironmentWithNoAuthority_WhenApiHealthIsRequested_ThenReturnsOk()
	{
		// Given
		await using var unconfigured = ProductionHostWithNoAuthority();
		using var client = OriginVerifiedClient(unconfigured);

		// When — the path CloudFront forwards to this function, and the one
		// the release's smoke test calls.
		using var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GivenProductionEnvironmentWithNoAuthority_WhenAnAuthorizationProtectedEndpointIsCalledWithABearerToken_ThenRefusedWith401()
	{
		// Given — any bearer token, not a real one: nothing here can validate
		// any token, forged or genuine.
		await using var unconfigured = ProductionHostWithNoAuthority();
		using var client = OriginVerifiedClient(unconfigured);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "any-bearer-token-value");

		// When
		using var response = await client.GetAsync(new Uri("/api/admin/questions", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenProductionEnvironmentWithNoAuthority_WhenAnAuthorizationProtectedEndpointIsCalledWithNoToken_ThenRefusedWith401()
	{
		// Given
		await using var unconfigured = ProductionHostWithNoAuthority();
		using var client = OriginVerifiedClient(unconfigured);

		// When
		using var response = await client.GetAsync(new Uri("/api/admin/questions", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	private WebApplicationFactory<Program> ProductionHostWithNoAuthority()
	{
		return _factory.WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Production");

			// Deliberately no HpacSafety:Authentication:Authority setting.

			// Outside Development the origin-secret check is required to start
			// at all (ADR-0159) — a boundary independent of the identity
			// provider one under test here.
			builder.UseSetting($"HpacSafety:Security:OriginVerification:HeaderName", OriginHeaderName);
			builder.UseSetting("HpacSafety:Security:OriginVerification:Secret", OriginSecret);
		});
	}

	private static HttpClient OriginVerifiedClient(WebApplicationFactory<Program> host)
	{
		var client = host.CreateClient();
		client.DefaultRequestHeaders.Add(OriginHeaderName, OriginSecret);
		return client;
	}
}
