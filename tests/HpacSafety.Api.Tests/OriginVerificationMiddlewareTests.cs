using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The API runs as a Lambda Function URL with authorization type
///     <c>NONE</c>, reached only through CloudFront; this is what refuses a
///     request that skipped CloudFront and hit the Function URL directly. See
///     ADR-0159, ADR-0042, and issue #443.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class OriginVerificationMiddlewareTests(ApiPostgresFixture fixture)
{
	private const string Secret = "test-cloudfront-origin-secret";
	private const string HeaderName = "X-Origin-Verify";

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoOriginSecretIsConfigured_WhenRequestedWithoutTheHeader_ThenItStillReachesTheEndpoint()
	{
		// Given — the shared fixture configures no secret at all, the
		// Development/test posture: there is no CloudFront in front of this
		// process.
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GivenAnOriginSecretIsConfigured_WhenRequestedWithoutTheHeader_ThenRefusedWith403()
	{
		// Given
		await using var verified = VerifiedFactory();
		using var client = verified.CreateClient();

		// When
		using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenAnOriginSecretIsConfigured_WhenRequestedWithTheWrongHeaderValue_ThenRefusedWith403()
	{
		// Given
		await using var verified = VerifiedFactory();
		using var client = verified.CreateClient();
		client.DefaultRequestHeaders.Add(HeaderName, "not-the-secret");

		// When
		using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenAnOriginSecretIsConfigured_WhenRequestedWithTheMatchingHeaderValue_ThenReachesTheEndpoint()
	{
		// Given
		await using var verified = VerifiedFactory();
		using var client = verified.CreateClient();
		client.DefaultRequestHeaders.Add(HeaderName, Secret);

		// When
		using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	/// <summary>
	///     Fail closed, end to end: a deployed environment with no configured
	///     origin secret must never boot far enough to answer a request
	///     unverified. <c>OriginVerificationRegistrationTests</c> proves the
	///     registration call itself throws; this proves the whole host
	///     (Production's own environment name, not just "not Development") never
	///     comes up to serve traffic. See ADR-0159.
	/// </summary>
	[Fact]
	public void GivenProductionEnvironmentWithNoOriginSecret_WhenTheHostStarts_ThenItFailsToStart()
	{
		// Given
		using var unverified = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

		// When — accessing Services is what forces the deferred host to build,
		// running Program.cs's top-level statements for the first time.
		var starting = () => unverified.Services;

		// Then — the origin-verification registration throws before anything
		// else in Program.cs runs.
		Should.Throw<Exception>(starting);
	}

	private WebApplicationFactory<Program> VerifiedFactory()
	{
		return _factory.WithWebHostBuilder(builder =>
		{
			builder.UseSetting("HpacSafety:Security:OriginVerification:HeaderName", HeaderName);
			builder.UseSetting("HpacSafety:Security:OriginVerification:Secret", Secret);
		});
	}
}
