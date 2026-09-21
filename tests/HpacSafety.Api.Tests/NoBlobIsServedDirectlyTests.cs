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
/// door onto private media with its own access-control story to get wrong.
/// This test walks the live route table and fails if one ever appears, which is
/// the only moment the rule can be enforced cheaply — at review time, on the
/// pull request that adds it.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class NoBlobIsServedDirectlyTests(ApiPostgresFixture fixture)
{
    // Substrings that name a route delivering bytes rather than JSON. A new route
    // that legitimately matches one of these is a conversation, not a rename.
    //
    // This is a tripwire, not a proof: a route could serve blob bytes under a
    // name nobody listed. It is here because the cheapest moment to catch that
    // is the pull request that adds it, and a reviewer who has to rename a route
    // to get past this test has been asked the right question.
    private static readonly string[] BlobServingPatterns =
    [
        "blob",
        "photo",
        "image",
        "thumbnail",
        "preview",
        "media/content",
        "files/content",
        "download",
        "/raw",
        "attachment",
    ];

    private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

    [Fact]
    public void GivenApiRouteTable_WhenRead_ThenNoRouteServesBlobDirectly()
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
    public void GivenApiRouteTable_WhenRead_ThenNotEmpty()
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
