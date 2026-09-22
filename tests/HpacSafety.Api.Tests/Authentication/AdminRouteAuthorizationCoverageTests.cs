using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     Every <c>/api/admin/*</c> route, from the live route table. REQ-MOD-024:
///     the API authorizes every admin operation regardless of what the UI shows,
///     and this is what keeps that true as new admin endpoints are added.
/// </summary>
/// <remarks>
///     A route-table walk rather than a fixed endpoint list, so a future admin
///     endpoint that forgets <c>RequireAuthorization</c> fails this test the
///     moment it is added — the cheapest point to catch it — instead of relying
///     on someone remembering to extend a list here.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class AdminRouteAuthorizationCoverageTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public void GivenEveryAdminRoute_WhenItsMetadataIsRead_ThenItRequiresAuthorizationAndAllowsNoAnonymousAccess()
	{
		// Given
		using var scope = _factory.Services.CreateScope();
		var adminRoutes = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Where(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty)
				.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
			.ToArray();

		// When / Then
		adminRoutes.ShouldNotBeEmpty();

		foreach (var route in adminRoutes)
		{
			route.Metadata.GetMetadata<IAuthorizeData>()
				.ShouldNotBeNull($"{route.RoutePattern.RawText} has no authorization requirement.");
			route.Metadata.GetMetadata<IAllowAnonymous>()
				.ShouldBeNull($"{route.RoutePattern.RawText} allows anonymous access.");
		}
	}
}
