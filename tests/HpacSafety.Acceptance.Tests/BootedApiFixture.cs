using System.Net.Http.Headers;
using System.Net.Http.Json;

using HpacSafety.Core.Features.Moderation;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Reqnroll;

using Testcontainers.PostgreSql;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
/// The API, booted in process, for the scenarios that describe what it
/// refuses over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// Most scenarios in <c>features/</c> describe rules that live in the domain
/// and execute against it directly — no host, no database, no container. The
/// authorization scenarios are different: "the API rejects the operation
/// regardless of what the UI would have shown" is a statement about a real
/// request reaching a real policy, and there is no honest way to assert it
/// without one.
/// </para>
/// <para>
/// <b>Started on first use, not at test-run start.</b> Reqnroll's
/// <c>[BeforeTestRun]</c> would pay for a container on every acceptance run,
/// including the domain-only ones that are the large majority. The host
/// applies migrations at startup (ADR-0055), so a database is required even
/// though most of these requests are refused before any handler runs.
/// </para>
/// <para>
/// This is deliberately not a second copy of <c>HpacSafety.Api.Tests</c>.
/// That suite proves the behaviour in detail — every rejected token shape,
/// every role at every endpoint. This one proves that the sentences in the
/// feature file are true of the running system.
/// </para>
/// </remarks>
public static class BootedApi
{
    /// <summary>Signs the tokens the booted host issues and then validates.</summary>
    public const string SigningKey = "hpac-safety-acceptance-signing-key-not-a-secret";

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static PostgreSqlContainer? postgres;
    private static WebApplicationFactory<Program>? factory;

    /// <summary>The booted host, starting it if this is the first scenario to ask.</summary>
    public static async Task<WebApplicationFactory<Program>> FactoryAsync()
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
                await container.StartAsync().ConfigureAwait(false);
                postgres = container;

                factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                {
                    // Development, so the host issues the tokens it validates.
                    builder.UseEnvironment("Development");
                    builder.UseSetting("ConnectionStrings:HpacSafety", container.GetConnectionString());
                    builder.UseSetting("HpacSafety:Authentication:DevelopmentSigningKey", SigningKey);
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
    /// A host that is not in Development, validating against a provider it can
    /// never reach — which is all these scenarios need, because the routes they
    /// ask about either do not exist there or refuse before any handler runs.
    /// </summary>
    public static async Task<WebApplicationFactory<Program>> ProductionShapedAsync() =>
        (await FactoryAsync().ConfigureAwait(false)).WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("HpacSafety:Authentication:Authority", "https://provider.example.test");
        });

    /// <summary>
    /// A client carrying a real token for that role, minted by the booted host
    /// and validated by the same middleware production runs (ADR-0066).
    /// </summary>
    public static async Task<HttpClient> SignedInAsAsync(MemberRole role)
    {
        var (username, password) = role switch
        {
            MemberRole.Administrator => ("admin", "admin"),
            MemberRole.SafetyOfficer => ("officer", "officer"),
            MemberRole.User => ("user", "user"),
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };

        var host = await FactoryAsync().ConfigureAwait(false);

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

    /// <summary>Stops the host and the container once, after the whole run.</summary>
    [AfterTestRun]
    public static async Task StopAsync()
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
    }

    private sealed record TokenPayload(string AccessToken);
}
