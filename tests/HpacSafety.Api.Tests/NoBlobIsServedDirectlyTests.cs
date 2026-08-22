using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
/// "Private bucket, no public object URLs, ever. Admin views use short-lived
/// pre-signed GETs" — docs/data-handling.md.
/// <para>
/// The rule is about what the API is *not*: there is no route that reads a blob
/// and writes its bytes to the response, because such a route would be a second
/// door onto Restricted media with its own access-control story to get wrong.
/// This test walks the live route table and fails if one ever appears, which is
/// the only moment the rule can be enforced cheaply — at review time, on the
/// pull request that adds it.
/// </para>
/// </summary>
public class NoBlobIsServedDirectlyTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Substrings that name a route delivering bytes rather than JSON. A new route
    // that legitimately matches one of these is a conversation, not a rename.
    private static readonly string[] BlobServingPatterns =
    [
        "blob",
        "media/content",
        "files/content",
        "download",
        "/raw",
        "attachment",
    ];

    private readonly WebApplicationFactory<Program> _factory;

    public NoBlobIsServedDirectlyTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public void Given_the_api_route_table_When_it_is_read_Then_no_route_serves_a_blob_directly()
    {
        // Given
        using var scope = _factory.Services.CreateScope();
        var routes = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        // When
        var offenders = routes
            .Where(pattern => BlobServingPatterns.Any(p => pattern.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        // Then
        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void Given_the_api_route_table_When_it_is_read_Then_it_is_not_empty()
    {
        // Given
        using var scope = _factory.Services.CreateScope();

        // When
        var routes = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints;

        // Then
        // Guards the test above: an empty route table would pass it for the wrong
        // reason, and a guard that cannot fail is not a guard.
        routes.ShouldNotBeEmpty();
    }
}
