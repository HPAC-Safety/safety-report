using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Boots the API in process through <see cref="WebApplicationFactory{TEntryPoint}" />.
///     The endpoint under test is trivial; the harness is not, and this is what
///     proves the harness works before any real endpoint depends on it.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class HealthEndpointTests(ApiPostgresFixture fixture)
{
    private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

    [Fact]
    public async Task GivenApiIsRunning_WhenHealthIsRequested_ThenReturnsOk()
    {
        // Given
        using var client = _factory.CreateClient();

        // When
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GivenApiIsRunning_WhenHealthIsRequested_ThenBodyReportsStatusOk()
    {
        // Given
        using var client = _factory.CreateClient();

        // When
        var body = await client.GetFromJsonAsync<JsonElement>(new Uri("/health", UriKind.Relative));

        // Then
        body.GetProperty("status").GetString().ShouldBe("ok");
    }

    [Fact]
    public async Task GivenApiIsRunning_WhenUnmappedRouteIsRequested_ThenReturnsNotFound()
    {
        // Given
        using var client = _factory.CreateClient();

        // When
        using var response = await client.GetAsync(new Uri("/no-such-endpoint", UriKind.Relative));

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
