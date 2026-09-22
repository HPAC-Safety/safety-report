using HpacSafety.Api.RateLimiting;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.Authentication;

/// <summary>The authentication endpoints.</summary>
public static class AuthEndpoints
{
	/// <summary>
	///     Maps them. The development token endpoint is mapped <b>only</b> in
	///     Development, so elsewhere the route does not exist — a 404, not a 401.
	///     There is no flag that turns it on in a deployed environment, because
	///     there is no code path that maps it there. See ADR-0066.
	/// </summary>
	/// <param name="app">The route builder.</param>
	/// <param name="isDevelopment">Whether this host issues its own tokens.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAuth(this IEndpointRouteBuilder app, bool isDevelopment)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/auth");

		// Anonymous, and present in every environment: this is how the browser
		// learns whether to offer third-party sign-in, instead of baking the
		// environment into its bundle at build time.
		group.MapGet("/config", Config).AllowAnonymous();

		// Proves a token was validated rather than merely minted.
		group.MapGet("/me", Me).RequireAuthorization(HpacPolicies.Member);

		if (isDevelopment)
		{
			group.MapPost("/token", Token).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.SignIn);
		}

		return group;
	}

	private static Ok<AuthConfigResponse> Config(IOptions<HpacAuthenticationOptions> options)
	{
		var configured = options.Value;
		var provider = !string.IsNullOrWhiteSpace(configured.Authority);

		return TypedResults.Ok(new AuthConfigResponse(
			provider ? "provider" : "development",
			provider,
			configured.Authority));
	}

	private static Results<Ok<MeResponse>, ProblemHttpResult> Me(
		HttpContext context, IOptions<HpacAuthenticationOptions> options)
	{
		var identity = MemberRoles.IdentityOf(context.User, options.Value.RoleClaimType);

		// A validated token should always carry a subject. If one does not,
		// this reads rather than assumes.
		return identity is null
			? TypedResults.Problem(
				title: "Not signed in.",
				detail: "The token carries no subject.",
				statusCode: StatusCodes.Status401Unauthorized,
				type: "https://hpac.ca/problems/not-signed-in")
			: TypedResults.Ok(new MeResponse(identity.Subject, MemberRoles.CodeFor(identity.Role)));
	}

	private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> Token(
		[FromBody] TokenRequest request,
		DevelopmentTokenIssuer issuer,
		HpacSafetyDbContext database,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		DevelopmentToken? token;

		try
		{
			token = await issuer.Issue(request.Username, request.Password, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (MembersSiteUnavailableException cause)
		{
			// Distinct from bad credentials: the members site could not be
			// asked, so nothing about this login attempt was actually judged.
			// Not an audited outcome either, for the same reason.
			return TypedResults.Problem(
				title: "The members site could not be reached.",
				detail: cause.Message,
				statusCode: StatusCodes.Status502BadGateway,
				type: "https://hpac.ca/problems/members-site-unavailable");
		}

		var at = clock.GetUtcNow();

		// One content-free row either way — never the username or the password
		// (ADR-0092). Written and saved before the response is returned, so a
		// failure to audit fails the sign-in attempt the same way.
		var attemptedIdentity = string.IsNullOrWhiteSpace(request.Username) ? "(unknown)" : request.Username;
		database.AuditLog.Add(token is null
			? new AuditLogEntry(attemptedIdentity, AuditAction.SignedInFailed, "Authentication", TinyId.New(), at)
			: new AuditLogEntry(token.Subject, AuditAction.SignedInSucceeded, "Authentication", TinyId.New(), at));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// One generic failure. Nothing distinguishes an unknown username from a
		// wrong password, or which development credential source was tried.
		return token is null
			? TypedResults.Problem(
				title: "Those credentials were not accepted.",
				detail: "Check the username and password and try again.",
				statusCode: StatusCodes.Status401Unauthorized,
				type: "https://hpac.ca/problems/invalid-credentials")
			: TypedResults.Ok(new TokenResponse(
				token.AccessToken, token.ExpiresAt, token.Subject, MemberRoles.CodeFor(token.Role)));
	}
}

/// <summary>What the browser needs to render the sign-in page.</summary>
/// <param name="Mode"><c>development</c> or <c>provider</c>.</param>
/// <param name="ThirdPartySignIn">Whether to offer a third-party sign-in option.</param>
/// <param name="Authority">The provider's issuer URL, when there is one.</param>
public sealed record AuthConfigResponse(string Mode, bool ThirdPartySignIn, string? Authority);

/// <summary>Development credentials.</summary>
/// <param name="Username">The username.</param>
/// <param name="Password">The password.</param>
public sealed record TokenRequest(string? Username, string? Password);

/// <summary>A signed token and what it says, so the browser need not parse it.</summary>
/// <param name="AccessToken">The compact JWT.</param>
/// <param name="ExpiresAt">When it stops being valid.</param>
/// <param name="Subject">The subject it carries.</param>
/// <param name="Role">The role code it carries.</param>
public sealed record TokenResponse(
	string AccessToken,
	DateTimeOffset ExpiresAt,
	string Subject,
	string Role);

/// <summary>Who the caller is, according to their validated token.</summary>
/// <param name="Subject">The subject claim.</param>
/// <param name="Role">The role code.</param>
public sealed record MeResponse(string Subject, string Role);
