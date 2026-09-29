using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     The two discovery endpoints the temporary interim issuer publishes
///     (issue #648, ADR-0172): a minimal OpenID Connect discovery document and
///     a JWKS holding only its public key. Mapped by <see cref="AuthEndpoints.MapAuth" />
///     only where the interim issuer is enabled, under <c>/api/auth/interim</c>.
///     <c>/api/auth/token</c> itself stays in <see cref="AuthEndpoints" /> — it
///     already exists and only its issuer differs.
/// </summary>
public static class InterimIssuerEndpoints
{
	/// <summary>Maps both endpoints under <c>/interim</c> on the given group.</summary>
	public static RouteGroupBuilder MapInterimIssuer(this RouteGroupBuilder authGroup)
	{
		ArgumentNullException.ThrowIfNull(authGroup);

		var group = authGroup.MapGroup("/interim").AllowAnonymous();

		group.MapGet("/.well-known/openid-configuration", Discovery);
		group.MapGet("/jwks", Jwks);

		return authGroup;
	}

	private static Ok<InterimDiscoveryDocument> Discovery(HttpContext context)
	{
		var origin = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";

		return TypedResults.Ok(new InterimDiscoveryDocument(
			InterimTokenIssuer.IssuerName,
			$"{origin}/api/auth/interim/jwks",
			$"{origin}/api/auth/token",
			["RS256"]));
	}

	private static Ok<object> Jwks(InterimIssuerSigningKey signingKey)
	{
		return TypedResults.Ok(signingKey.ToJwks());
	}
}

/// <summary>A minimal OpenID Connect discovery document — only what issue #648 asks this to publish.</summary>
/// <param name="Issuer">The issuer every interim token names.</param>
/// <param name="JwksUri">Where to fetch this issuer's public keys.</param>
/// <param name="TokenEndpoint">Where a member exchanges credentials for a token.</param>
/// <param name="IdTokenSigningAlgValuesSupported">Always exactly <c>["RS256"]</c>.</param>
/// <remarks>
///     OpenID discovery is conventionally snake_case, unlike every other
///     response shape in this API (camelCase, ASP.NET's default) — the
///     <see cref="JsonPropertyNameAttribute" />s below are deliberate, not a
///     convention this repository is adopting more broadly.
/// </remarks>
public sealed record InterimDiscoveryDocument(
	[property: JsonPropertyName("issuer")] string Issuer,
	[property: JsonPropertyName("jwks_uri")] string JwksUri,
	[property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
	[property: JsonPropertyName("id_token_signing_alg_values_supported")] string[] IdTokenSigningAlgValuesSupported);
