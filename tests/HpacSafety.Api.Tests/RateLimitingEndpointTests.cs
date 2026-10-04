using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Proves the <c>RateLimiter</c> policies actually reject once their
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
	private static readonly Uri Uploads = new("/api/v1/uploads", UriKind.Relative);

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
		using var first = await reporter.PostAsync(Submit, firstBody, TestContext.Current.CancellationToken);
		using var second = await reporter.PostAsync(Submit, secondBody, TestContext.Current.CancellationToken);

		// Then — the first consumes the one permit and fails validation as usual
		// (a submission is JSON); the second never reaches the endpoint at all.
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
		await reporter.PostAsync(Submit, firstBody, TestContext.Current.CancellationToken);

		// When
		using var rejected = await reporter.PostAsync(Submit, secondBody, TestContext.Current.CancellationToken);
		var body = await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

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
		using var first = await client.PostAsJsonAsync(Token, new { username = "user", password = "wrong" }, cancellationToken: TestContext.Current.CancellationToken);
		using var second = await client.PostAsJsonAsync(Token, new { username = "user", password = "wrong" }, cancellationToken: TestContext.Current.CancellationToken);

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
		await client.PostAsJsonAsync(Token, new { username = "user", password = "wrong" }, cancellationToken: TestContext.Current.CancellationToken);

		// When
		using var response = await client.PostAsJsonAsync(Token, new { username = "admin", password = "admin" }, cancellationToken: TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GivenTheUploadLimitIsExceeded_WhenUploaded_ThenRejectedWith429()
	{
		// Given — a one-permit window for attachment uploads (REQ-SUB-044). An
		// empty body still consumes the permit before the endpoint refuses it.
		await using var limited = RateLimitedFactory(attachmentUploadPermitLimit: 1);
		using var reporter = await SignedInClient.As(limited, MemberRole.User);
		using var firstBody = new ByteArrayContent([]);
		using var secondBody = new ByteArrayContent([]);

		// When
		using var first = await reporter.PostAsync(Uploads, firstBody, TestContext.Current.CancellationToken);
		using var second = await reporter.PostAsync(Uploads, secondBody, TestContext.Current.CancellationToken);

		// Then
		first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
	}

	/// <summary>
	///     ADR-0159: the partition key is <c>CloudFront-Viewer-Address</c>, not
	///     the shared loopback address every request in this in-process test
	///     host otherwise carries — proven by two "reporters" behind different
	///     viewer addresses each getting their own one-permit window.
	/// </summary>
	[Fact]
	public async Task GivenTwoDifferentCloudFrontViewerAddresses_WhenBothSubmitOnceEach_ThenNeitherIsRejected()
	{
		// Given
		await using var limited = RateLimitedFactory(publicSubmissionPermitLimit: 1);
		using var first = await SignedInClient.As(limited, MemberRole.User);
		using var second = await SignedInClient.As(limited, MemberRole.User);
		first.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "203.0.113.10:52341");
		second.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "203.0.113.99:11402");

		// When
		using var firstBody = new MultipartFormDataContent();
		using var secondBody = new MultipartFormDataContent();
		using var firstResponse = await first.PostAsync(Submit, firstBody, TestContext.Current.CancellationToken);
		using var secondResponse = await second.PostAsync(Submit, secondBody, TestContext.Current.CancellationToken);

		// Then — both fail validation (a submission is JSON), neither is
		// rate-limited, because each is its own partition.
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	/// <summary>The same viewer address, read from the header, still shares one window.</summary>
	[Fact]
	public async Task GivenTheSameCloudFrontViewerAddressTwice_WhenBothSubmit_ThenTheSecondIsRejected()
	{
		// Given
		await using var limited = RateLimitedFactory(publicSubmissionPermitLimit: 1);
		using var reporter = await SignedInClient.As(limited, MemberRole.User);
		reporter.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "198.51.100.7:40010");

		// When
		using var firstBody = new MultipartFormDataContent();
		using var secondBody = new MultipartFormDataContent();
		using var firstResponse = await reporter.PostAsync(Submit, firstBody, TestContext.Current.CancellationToken);
		using var secondResponse = await reporter.PostAsync(Submit, secondBody, TestContext.Current.CancellationToken);

		// Then
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
	}

	/// <summary>ADR-0159: CloudFront sends IPv6 viewers as <c>[addr]:port</c>.</summary>
	[Fact]
	public async Task GivenTwoDifferentBracketedIpv6ViewerAddresses_WhenBothSubmitOnceEach_ThenNeitherIsRejected()
	{
		// Given
		await using var limited = RateLimitedFactory(publicSubmissionPermitLimit: 1);
		using var first = await SignedInClient.As(limited, MemberRole.User);
		using var second = await SignedInClient.As(limited, MemberRole.User);
		first.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "[2001:db8::1]:443");
		second.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "[2001:db8::2]:443");

		// When
		using var firstBody = new MultipartFormDataContent();
		using var secondBody = new MultipartFormDataContent();
		using var firstResponse = await first.PostAsync(Submit, firstBody);
		using var secondResponse = await second.PostAsync(Submit, secondBody);

		// Then
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	/// <summary>The port is stripped, so one IPv6 viewer on two ports shares a window.</summary>
	[Fact]
	public async Task GivenOneBracketedIpv6ViewerOnTwoPorts_WhenBothSubmit_ThenSecondIsRejected()
	{
		// Given
		await using var limited = RateLimitedFactory(publicSubmissionPermitLimit: 1);
		using var first = await SignedInClient.As(limited, MemberRole.User);
		using var second = await SignedInClient.As(limited, MemberRole.User);
		first.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "[2001:db8::1]:443");
		second.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "[2001:db8::1]:51000");

		// When
		using var firstBody = new MultipartFormDataContent();
		using var secondBody = new MultipartFormDataContent();
		using var firstResponse = await first.PostAsync(Submit, firstBody);
		using var secondResponse = await second.PostAsync(Submit, secondBody);

		// Then
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
	}

	/// <summary>An address with no port is used whole as the partition key.</summary>
	[Fact]
	public async Task GivenViewerAddressWithoutPort_WhenSameAddressSubmitsTwice_ThenSecondIsRejected()
	{
		// Given
		await using var limited = RateLimitedFactory(publicSubmissionPermitLimit: 1);
		using var reporter = await SignedInClient.As(limited, MemberRole.User);
		reporter.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "198.51.100.8");

		// When
		using var firstBody = new MultipartFormDataContent();
		using var secondBody = new MultipartFormDataContent();
		using var firstResponse = await reporter.PostAsync(Submit, firstBody);
		using var secondResponse = await reporter.PostAsync(Submit, secondBody);

		// Then
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
	}

	[Fact]
	public async Task GivenTwoDifferentViewerAddressesWithoutPort_WhenBothSubmitOnceEach_ThenNeitherIsRejected()
	{
		// Given
		await using var limited = RateLimitedFactory(publicSubmissionPermitLimit: 1);
		using var first = await SignedInClient.As(limited, MemberRole.User);
		using var second = await SignedInClient.As(limited, MemberRole.User);
		first.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "198.51.100.8");
		second.DefaultRequestHeaders.Add("CloudFront-Viewer-Address", "198.51.100.9");

		// When
		using var firstBody = new MultipartFormDataContent();
		using var secondBody = new MultipartFormDataContent();
		using var firstResponse = await first.PostAsync(Submit, firstBody);
		using var secondResponse = await second.PostAsync(Submit, secondBody);

		// Then
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private WebApplicationFactory<Program> RateLimitedFactory(
		int publicSubmissionPermitLimit = 100000,
		int signInPermitLimit = 100000,
		int attachmentUploadPermitLimit = 100000)
	{
		return _factory.WithWebHostBuilder(builder =>
		{
			builder.UseSetting(
				"HpacSafety:RateLimiting:AttachmentUpload:PermitLimit",
				attachmentUploadPermitLimit.ToString(CultureInfo.InvariantCulture));
			builder.UseSetting("HpacSafety:RateLimiting:AttachmentUpload:WindowSeconds", "60");
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
