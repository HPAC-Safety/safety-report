namespace HpacSafety.Api.Admin;

/// <summary>
/// The admin boundary for the question-authoring endpoints.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a stub, and deliberately a single one.</b> Real authentication is
/// ADR-0005's <c>IMemberAuthenticator</c>, which has no implementation yet; the
/// web application's signed-in state is currently a marker in
/// <c>sessionStorage</c> (issue #172). Until the OIDC adapter lands, these
/// endpoints require that marker to be presented as a header, which stops the
/// admin API answering an anonymous request but proves nothing about who is
/// asking.
/// </para>
/// <para>
/// It lives in one place so the adapter replaces one filter rather than a check
/// copied into every endpoint. The authorization boundary is the API, never
/// hidden markup — ADR-0048.
/// </para>
/// </remarks>
public static class AdminGate
{
    /// <summary>The header the web application presents its session marker in.</summary>
    public const string SessionHeader = "X-Hpac-Member-Session";

    /// <summary>
    /// Requires a member session on every endpoint in the group. Replaced
    /// wholesale when ADR-0005's authenticator exists.
    /// </summary>
    /// <param name="builder">The endpoint group being gated.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder RequireAdminSession<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEndpointFilter(static async (context, next) =>
            string.IsNullOrWhiteSpace(context.HttpContext.Request.Headers[SessionHeader])
                ? Results.Problem(
                    title: "Not signed in.",
                    detail: "These endpoints are available to signed-in HPAC administrators.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    type: "https://hpac.ca/problems/not-signed-in")
                : await next(context).ConfigureAwait(false));

        return builder;
    }
}
