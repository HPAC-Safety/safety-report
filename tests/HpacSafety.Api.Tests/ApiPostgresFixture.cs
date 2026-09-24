using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.S3;
using DotNet.Testcontainers.Containers;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace HpacSafety.Api.Tests;

/// <summary>
///     One PostgreSQL 17 container, one S3-compatible container for private attachment
///     storage (ADR-0096), and one <see cref="WebApplicationFactory{TEntryPoint}" />
///     shared across every test in the collection. The API migrates the container
///     itself at startup (<c>HpacSafetyDbContext.EnsureMigrated</c>, ADR-0055),
///     so booting the factory at all proves that path works.
/// </summary>
public sealed class ApiPostgresFixture : IAsyncLifetime
{
	/// <summary>
	///     The signing key the booted API issues and validates development tokens
	///     with. Long enough to satisfy the minimum the host enforces at startup.
	/// </summary>
	public const string SigningKey = "hpac-safety-api-test-signing-key-not-a-secret";

	/// <summary>The private bucket the booted API writes attachments to.</summary>
	public const string BucketName = "hpac-safety-uploads";

	// Pinned rather than floating on `latest` — see PostgresContainerTests and
	// S3Emulator.
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
	private readonly IContainer _objects = S3Emulator.Build();

	/// <summary>The API, booted in process against the containers above.</summary>
	public WebApplicationFactory<Program> Factory { get; private set; } = null!;

	/// <summary>A client straight to the bucket, for asserting what the API stored.</summary>
	public IAmazonS3 Storage { get; private set; } = null!;

	/// <inheritdoc />
	public async Task InitializeAsync()
	{
		await Task.WhenAll(_postgres.StartAsync(), _objects.StartAsync());
		Storage = await S3Emulator.CreateBucket(_objects, BucketName);

		Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Development");
			builder.UseSetting("ConnectionStrings:HpacSafety", _postgres.GetConnectionString());
			builder.UseSetting("HpacSafety:Authentication:DevelopmentSigningKey", SigningKey);
			builder.UseSetting("HpacSafety:Media:Storage:S3:BucketName", BucketName);
			builder.UseSetting("HpacSafety:Media:Storage:S3:ServiceUrl", S3Emulator.ServiceUrl(_objects));
			builder.UseSetting("HpacSafety:Media:Storage:S3:PublicServiceUrl", S3Emulator.ServiceUrl(_objects));
			builder.UseSetting("HpacSafety:Media:Storage:S3:AccessKey", S3Emulator.AccessKey);
			builder.UseSetting("HpacSafety:Media:Storage:S3:SecretKey", S3Emulator.SecretKey);

			// This factory is shared across every test in the collection, many of
			// which sign in or submit repeatedly against the same identity/IP
			// partition. Effectively unlimited here so ordinary test traffic never
			// trips a policy meant for a real client. A test that actually proves
			// 429 behavior derives its own factory with a tiny limit instead — see
			// RateLimitingEndpointTests. See issue #15.
			builder.UseSetting("HpacSafety:RateLimiting:PublicSubmission:PermitLimit", "100000");
			builder.UseSetting("HpacSafety:RateLimiting:PublicSubmission:WindowSeconds", "60");
			builder.UseSetting("HpacSafety:RateLimiting:SignIn:PermitLimit", "100000");
			builder.UseSetting("HpacSafety:RateLimiting:SignIn:WindowSeconds", "60");
			builder.UseSetting("HpacSafety:RateLimiting:AttachmentUpload:PermitLimit", "100000");
			builder.UseSetting("HpacSafety:RateLimiting:AttachmentUpload:WindowSeconds", "60");
		});
	}

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		await Factory.DisposeAsync();
		Storage.Dispose();
		await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _objects.DisposeAsync().AsTask());
	}
}

/// <summary>
///     Signs a test client in by asking the booted API for a real token.
/// </summary>
/// <remarks>
///     Deliberately not a faked <c>ClaimsPrincipal</c> or a test authentication
///     handler: the token is minted by the host and then validated by the same
///     middleware production runs, so a test exercises signature verification,
///     issuer and audience checks, expiry, claim extraction, and policy evaluation
///     rather than trusting a stub. See ADR-0066.
/// </remarks>
public static class SignedInClient
{
	/// <summary>The development credential pair for each role.</summary>
	public static (string Username, string Password) CredentialsFor(MemberRole role)
	{
		return role switch
		{
			MemberRole.Administrator => ("admin", "admin"),
			MemberRole.SafetyOfficer => ("officer", "officer"),
			MemberRole.User => ("user", "user"),
			_ => throw new ArgumentOutOfRangeException(nameof(role)),
		};
	}

	/// <summary>Asks the booted API for a signed token in that role.</summary>
	public static async Task<string> TokenFor(WebApplicationFactory<Program> factory,
											  MemberRole role)
	{
		ArgumentNullException.ThrowIfNull(factory);

		var (username, password) = CredentialsFor(role);

		using var anonymous = factory.CreateClient();
		using var response = await anonymous.PostAsJsonAsync("/api/auth/token", new { username, password });

		response.EnsureSuccessStatusCode();

		var token = await response.Content.ReadFromJsonAsync<TokenPayload>();
		return token!.AccessToken;
	}

	/// <summary>A client carrying a real bearer token for that role.</summary>
	public static async Task<HttpClient> As(WebApplicationFactory<Program> factory,
											MemberRole role)
	{
		ArgumentNullException.ThrowIfNull(factory);

		var token = await TokenFor(factory, role);
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		return client;
	}

	/// <summary>A client carrying an arbitrary bearer token.</summary>
	public static HttpClient Bearing(WebApplicationFactory<Program> factory,
									 string token)
	{
		ArgumentNullException.ThrowIfNull(factory);

		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		return client;
	}

	private sealed record TokenPayload(string AccessToken, DateTimeOffset ExpiresAt, string Subject, string Role);
}

/// <summary>Shares one container and factory across every API test class.</summary>
[CollectionDefinition(Name)]
public sealed class SharedApiPostgres : ICollectionFixture<ApiPostgresFixture>
{
	/// <summary>The collection name.</summary>
	public const string Name = "API PostgreSQL";
}
