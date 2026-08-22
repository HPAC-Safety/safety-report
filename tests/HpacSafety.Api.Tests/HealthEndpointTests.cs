using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
/// Boots the API in process through <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// The endpoint under test is trivial; the harness is not, and this is what
/// proves the harness works before any real endpoint depends on it.
/// </summary>
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Given_the_api_is_running_When_health_is_requested_Then_it_returns_ok()
    {
        // Given
        using var client = _factory.CreateClient();

        // When
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Given_the_api_is_running_When_health_is_requested_Then_the_body_reports_status_ok()
    {
        // Given
        using var client = _factory.CreateClient();

        // When
        var body = await client.GetFromJsonAsync<JsonElement>(new Uri("/health", UriKind.Relative));

        // Then
        body.GetProperty("status").GetString().ShouldBe("ok");
    }

    [Fact]
    public async Task Given_the_api_is_running_When_an_unmapped_route_is_requested_Then_it_returns_not_found()
    {
        // Given
        using var client = _factory.CreateClient();

        // When
        using var response = await client.GetAsync(new Uri("/no-such-endpoint", UriKind.Relative));

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
