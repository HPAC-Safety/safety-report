using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Amazon.S3;
using DotNet.Testcontainers.Containers;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Reqnroll;
using Testcontainers.PostgreSql;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The API, booted in process, for the scenarios that describe what it
///     refuses over HTTP.
/// </summary>
/// <remarks>
///     <para>
///         Most scenarios in <c>features/</c> describe rules that live in the domain
///         and execute against it directly — no host, no database, no container. The
///         authorization scenarios are different: "the API rejects the operation
///         regardless of what the UI would have shown" is a statement about a real
///         request reaching a real policy, and there is no honest way to assert it
///         without one.
///     </para>
///     <para>
///         <b>Started on first use, not at test-run start.</b> Reqnroll's
///         <c>[BeforeTestRun]</c> would pay for a container on every acceptance run,
///         including the domain-only ones that are the large majority. The host
///         applies migrations at startup (ADR-0055), so a database is required even
///         though most of these requests are refused before any handler runs.
///     </para>
///     <para>
///         This is deliberately not a second copy of <c>HpacSafety.Api.Tests</c>.
///         That suite proves the behaviour in detail — every rejected token shape,
///         every role at every endpoint. This one proves that the sentences in the
///         feature file are true of the running system.
///     </para>
/// </remarks>
public static class BootedApi
{
	/// <summary>Signs the tokens the booted host issues and then validates.</summary>
	public const string SigningKey = "hpac-safety-acceptance-signing-key-not-a-secret";

	private static readonly SemaphoreSlim Gate = new(1, 1);

	/// <summary>The private bucket the booted API writes attachments to.</summary>
	public const string BucketName = "hpac-safety-uploads";

	private static PostgreSqlContainer? postgres;
	private static IContainer? objectStore;
	private static AmazonS3Client? storage;
	private static WebApplicationFactory<Program>? factory;

	/// <summary>A client straight to the booted host's bucket, once it is up.</summary>
	public static IAmazonS3 Storage => storage ?? throw new InvalidOperationException("The API has not been booted yet.");

	/// <summary>The booted host, starting it if this is the first scenario to ask.</summary>
	public static async Task<WebApplicationFactory<Program>> Factory()
	{
		if (factory is not null)
		{
			return factory;
		}

		await Gate.WaitAsync().ConfigureAwait(false);

		try
		{
			if (factory is null)
			{
				var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
				var objects = S3Emulator.Build();
				await Task.WhenAll(container.StartAsync(), objects.StartAsync()).ConfigureAwait(false);
				postgres = container;
				objectStore = objects;
				storage = await S3Emulator.CreateBucket(objects, BucketName).ConfigureAwait(false);

				factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
				{
					// Development, so the host issues the tokens it validates.
					builder.UseEnvironment("Development");
					builder.UseSetting("ConnectionStrings:HpacSafety", container.GetConnectionString());
					builder.UseSetting("HpacSafety:Authentication:DevelopmentSigningKey", SigningKey);
					builder.UseSetting("HpacSafety:Media:Storage:S3:BucketName", BucketName);
					builder.UseSetting("HpacSafety:Media:Storage:S3:ServiceUrl", S3Emulator.ServiceUrl(objects));
					builder.UseSetting("HpacSafety:Media:Storage:S3:PublicServiceUrl", S3Emulator.ServiceUrl(objects));
					builder.UseSetting("HpacSafety:Media:Storage:S3:AccessKey", S3Emulator.AccessKey);
					builder.UseSetting("HpacSafety:Media:Storage:S3:SecretKey", S3Emulator.SecretKey);

					// Shared across every scenario in the run, many of which sign in
					// or submit repeatedly. Effectively unlimited here so ordinary
					// scenario traffic never trips a policy meant for a real client;
					// the two scenarios that actually prove 429 behavior derive their
					// own tightly-limited host instead. See RateLimited below,
					// ADR-0081, and issue #15.
					builder.UseSetting("HpacSafety:RateLimiting:PublicSubmission:PermitLimit", "100000");
					builder.UseSetting("HpacSafety:RateLimiting:PublicSubmission:WindowSeconds", "60");
					builder.UseSetting("HpacSafety:RateLimiting:SignIn:PermitLimit", "100000");
					builder.UseSetting("HpacSafety:RateLimiting:SignIn:WindowSeconds", "60");
					builder.UseSetting("HpacSafety:RateLimiting:AttachmentUpload:PermitLimit", "100000");
					builder.UseSetting("HpacSafety:RateLimiting:AttachmentUpload:WindowSeconds", "60");
				});
			}
		}
		finally
		{
			Gate.Release();
		}

		return factory;
	}

	/// <summary>
	///     A host that is not in Development, validating against a provider it can
	///     never reach — which is all these scenarios need, because the routes they
	///     ask about either do not exist there or refuse before any handler runs.
	/// </summary>
	public static async Task<WebApplicationFactory<Program>> ProductionShaped()
	{
		return (await Factory().ConfigureAwait(false)).WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Production");
			builder.UseSetting("HpacSafety:Authentication:Authority", "https://provider.example.test");
		});
	}

	private static WebApplicationFactory<Program>? recordingReads;

	/// <summary>
	///     A host whose blob store records how much of each object it reads, otherwise
	///     identical to <see cref="Factory" /> (REQ-SUB-075). Built once and shared.
	/// </summary>
	public static async Task<WebApplicationFactory<Program>> RecordingReads()
	{
		var booted = await Factory().ConfigureAwait(false);

		await Gate.WaitAsync().ConfigureAwait(false);
		try
		{
			return recordingReads ??= booted.WithWebHostBuilder(builder =>
				builder.ConfigureTestServices(services =>
				{
					var original = services.Last(descriptor => descriptor.ServiceType == typeof(IBlobStore));
					services.Remove(original);
					services.AddSingleton<IBlobStore>(provider =>
						new ReadRecordingBlobStore((IBlobStore)original.ImplementationFactory!(provider)));
				}));
		}
		finally
		{
			Gate.Release();
		}
	}

	/// <summary>
	///     A host with a one-permit rate-limit window for the given policy,
	///     otherwise identical to <see cref="Factory" />. See ADR-0081 and
	///     issue #15.
	/// </summary>
	public static async Task<WebApplicationFactory<Program>> RateLimited(string policy)
	{
		return (await Factory().ConfigureAwait(false)).WithWebHostBuilder(builder =>
		{
			builder.UseSetting($"HpacSafety:RateLimiting:{policy}:PermitLimit", "1");
			builder.UseSetting($"HpacSafety:RateLimiting:{policy}:WindowSeconds", "60");
		});
	}

	/// <summary>
	///     A client carrying a real token for that role, minted by the booted host
	///     and validated by the same middleware production runs (ADR-0066).
	/// </summary>
	public static async Task<HttpClient> SignedInAs(MemberRole role)
	{
		return await SignedInAs(role, await Factory().ConfigureAwait(false)).ConfigureAwait(false);
	}

	/// <summary>
	///     A client for one particular member, identified by <paramref name="subject" />:
	///     a token signed with the booted host's own development key, so scenarios
	///     that need two different members of the same role can have them.
	/// </summary>
	public static HttpClient SignedInAsMember(WebApplicationFactory<Program> host,
											  string subject,
											  string role = "user")
	{
		ArgumentNullException.ThrowIfNull(host);

		var token = new JwtSecurityToken(
			DevelopmentTokenIssuer.IssuerName,
			"hpac-safety-api",
			[new(JwtRegisteredClaimNames.Sub, subject), new("roles", role)],

			// JwtSecurityToken wants DateTime. Convert at this boundary and
			// never carry one past it (ADR-0035).
			DateTimeOffset.UtcNow.AddMinutes(-1).UtcDateTime,
			DateTimeOffset.UtcNow.AddHours(1).UtcDateTime,
			new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256));

		var client = host.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
		return client;
	}

	/// <summary>The same, against a specific already-booted host — see <see cref="RateLimited" />.</summary>
	public static async Task<HttpClient> SignedInAs(MemberRole role,
													WebApplicationFactory<Program> host)
	{
		ArgumentNullException.ThrowIfNull(host);

		var (username, password) = role switch
		{
			MemberRole.Administrator => ("admin", "admin"),
			MemberRole.SafetyOfficer => ("officer", "officer"),
			MemberRole.User => ("user", "user"),
			_ => throw new ArgumentOutOfRangeException(nameof(role)),
		};

		using var anonymous = host.CreateClient();
		using var response = await anonymous
			.PostAsJsonAsync("/api/auth/token", new { username, password })
			.ConfigureAwait(false);

		response.EnsureSuccessStatusCode();

		var token = await response.Content.ReadFromJsonAsync<TokenPayload>().ConfigureAwait(false);

		var client = host.CreateClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

		return client;
	}

	/// <summary>
	///     A Development host whose members-site login goes through a stubbed
	///     transport rather than the real network, with the given
	///     Development-only role allowlists — see ADR-0079.
	/// </summary>
	public static async Task<HttpClient> MembersSiteStubbed(
		HttpMessageHandler handler,
		IReadOnlyList<string>? administratorEmails = null,
		IReadOnlyList<string>? safetyOfficerEmails = null)
	{
		var host = (await Factory().ConfigureAwait(false)).WithWebHostBuilder(builder =>
		{
			builder.ConfigureTestServices(services =>
			{
				services
					.AddHttpClient(MembersSiteCredentialSource.HttpClientName)
					.ConfigurePrimaryHttpMessageHandler(() => handler);
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

		return host.CreateClient();
	}

	/// <summary>Stops the host and the containers once, after the whole run.</summary>
	[AfterTestRun]
	public static async Task Stop()
	{
		if (factory is not null)
		{
			await factory.DisposeAsync().ConfigureAwait(false);
			factory = null;
		}

		if (postgres is not null)
		{
			await postgres.DisposeAsync().ConfigureAwait(false);
			postgres = null;
		}

		storage?.Dispose();
		storage = null;

		if (objectStore is not null)
		{
			await objectStore.DisposeAsync().ConfigureAwait(false);
			objectStore = null;
		}
	}

	private sealed record TokenPayload(string AccessToken);
}
