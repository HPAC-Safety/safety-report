using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     "There is no scheduled report purge and no physical-delete path in the
///     application" and "there is no restore transition" — REQ-DOM-007,
///     REQ-DOM-010. Soft deletion is the only deletion behavior this system has.
/// </summary>
/// <remarks>
///     A route-table walk, the same shape as <see cref="NoBlobIsServedDirectlyTests" />:
///     the cheapest moment to catch a purge/restore/undelete route is the pull
///     request that adds it, not a design review after the fact.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class NoPurgeOrRestorePathExistsTests(ApiPostgresFixture fixture)
{
	private static readonly string[] ForbiddenPatterns = ["purge", "restore", "undelete", "un-delete"];

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public void GivenApiRouteTable_WhenRead_ThenNoRoutePurgesOrRestores()
	{
		// Given
		using var scope = _factory.Services.CreateScope();
		var routes = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
			.ToArray();

		// When
		var offenders = routes
			.Where(pattern => ForbiddenPatterns.Any(p => pattern.Contains(p, StringComparison.OrdinalIgnoreCase)))
			.ToArray();

		// Then
		offenders.ShouldBeEmpty();
	}

	[Fact]
	public void GivenTheReportDeletionEndpoint_WhenRead_ThenItIsTheOnlyMethodMappedForThatRoute()
	{
		// Given — DELETE exists (soft delete); nothing else may share the route,
		// which is what would make a restore/undo possible
		using var scope = _factory.Services.CreateScope();
		var methods = scope.ServiceProvider.GetRequiredService<EndpointDataSource>().Endpoints
			.OfType<RouteEndpoint>()
			.Where(endpoint => endpoint.RoutePattern.RawText == "/api/admin/reports/{id}")
			.SelectMany(endpoint => endpoint.Metadata.OfType<Microsoft.AspNetCore.Routing.HttpMethodMetadata>())
			.SelectMany(metadata => metadata.HttpMethods)
			.ToArray();

		// Then
		methods.ShouldBe(["DELETE"]);
	}
}
