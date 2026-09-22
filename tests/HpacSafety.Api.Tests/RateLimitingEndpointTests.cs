using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Proves the two <c>RateLimiter</c> policies actually reject once their
///     tiny, test-only limit is exceeded. Each test derives its own factory with
///     a one-permit window rather than touching <see cref="ApiPostgresFixture" />'s
///     shared, effectively-unlimited settings — every other test in the
///     collection depends on those staying generous. See ADR-0081 and issue #15.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class RateLimitingEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly Uri Token = new("/api/auth/token", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenTheSubmissionLimitIsExceeded_WhenSubmitted_ThenRejectedWith429()
	{
		// Given — a one-permit-per-minute window, and one authenticated reporter.
		// A malformed body still consumes a permit: the limiter runs before the
		// endpoint's own validation, so this needs no question-bank setup.
		await using var limited = RateLimitedFactory(publicSubmissionPermitLimit: 1);
		using var reporter = await SignedInClient.As(limited, MemberRole.User);
		using var firstBody = new MultipartFormDataContent();
		using var secondBody = new MultipartFormDataContent();

		// When
		using var first = await reporter.PostAsync(Submit, firstBody);
		using var second = await reporter.PostAsync(Submit, secondBody);

		// Then — the first consumes the one permit and fails validation as usual
		// (no "report" part); the second never reaches the endpoint at all.
		first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
		second.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
	}

	[Fact]
	public async Task GivenTheSubmissionLimitIsExceeded_WhenRejected_ThenNothingAboutTheReporterIsInTheResponse()
	{
		// Given
		await using var limited = RateLimitedFactory(publicSubmissionPermitLimit: 1);
		using var reporter = await SignedInClient.As(limited, MemberRole.User);
		using var firstBody = new MultipartFormDataContent();
		using var secondBody = new MultipartFormDataContent();
		await reporter.PostAsync(Submit, firstBody);

		// When
		using var rejected = await reporter.PostAsync(Submit, secondBody);
		var body = await rejected.Content.ReadAsStringAsync();

		// Then — a generic, content-free rejection: no IP, no token, no answer.
		body.ShouldNotContain("Bearer");
		body.ShouldNotContain("report");
		body.ShouldContain("hpac.ca/problems/rate-limited");
	}

	[Fact]
	public async Task GivenTheSignInLimitIsExceededForOneIdentity_WhenSignedIn_ThenRejectedWith429()
	{
		// Given — a one-permit-per-minute window for sign-in.
		await using var limited = RateLimitedFactory(signInPermitLimit: 1);
		using var client = limited.CreateClient();

		// When — the same username twice; a wrong password still consumes the
		// identity's permit, because partitioning happens before credentials are
		// checked.
		using var first = await client.PostAsJsonAsync(Token, new { username = "user", password = "wrong" });
		using var second = await client.PostAsJsonAsync(Token, new { username = "user", password = "wrong" });

		// Then
		first.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
	}

	[Fact]
	public async Task GivenTheSignInLimitIsExceededForOneIdentity_WhenADifferentIdentitySignsIn_ThenNotRejected()
	{
		// Given — the sign-in policy partitions by identity, so one username
		// being throttled must not throttle a different one sharing the same
		// connection.
		await using var limited = RateLimitedFactory(signInPermitLimit: 1);
		using var client = limited.CreateClient();
		await client.PostAsJsonAsync(Token, new { username = "user", password = "wrong" });

		// When
		using var response = await client.PostAsJsonAsync(Token, new { username = "admin", password = "admin" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private WebApplicationFactory<Program> RateLimitedFactory(
		int publicSubmissionPermitLimit = 100000, int signInPermitLimit = 100000)
	{
		return _factory.WithWebHostBuilder(builder =>
		{
			builder.UseSetting(
				"HpacSafety:RateLimiting:PublicSubmission:PermitLimit",
				publicSubmissionPermitLimit.ToString(CultureInfo.InvariantCulture));
			builder.UseSetting("HpacSafety:RateLimiting:PublicSubmission:WindowSeconds", "60");
			builder.UseSetting(
				"HpacSafety:RateLimiting:SignIn:PermitLimit",
				signInPermitLimit.ToString(CultureInfo.InvariantCulture));
			builder.UseSetting("HpacSafety:RateLimiting:SignIn:WindowSeconds", "60");
		});
	}
}
