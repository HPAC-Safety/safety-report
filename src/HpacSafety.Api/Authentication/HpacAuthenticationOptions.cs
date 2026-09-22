namespace HpacSafety.Api.Authentication;

/// <summary>
///     How this API validates a bearer token. Bound from the
///     <c>HpacSafety:Authentication</c> configuration section.
/// </summary>
/// <remarks>
///     Deliberately small. The API reads exactly two claims — the subject and the
///     role — so there is nothing here about names, email addresses, or profile
///     scopes, and adding one would widen what this system knows about a person.
///     See ADR-0064.
/// </remarks>
public sealed class HpacAuthenticationOptions
{
	/// <summary>The configuration section this binds from.</summary>
	public const string SectionName = "HpacSafety:Authentication";

	/// <summary>
	///     The identity provider's issuer URL. Used to discover its signing keys,
	///     and required outside Development.
	/// </summary>
	public string? Authority { get; set; }

	/// <summary>
	///     The audience a token must carry. The same value in every environment —
	///     only the issuer and the key differ between the development issuer and a
	///     real provider.
	/// </summary>
	public string Audience { get; set; } = "hpac-safety-api";

	/// <summary>
	///     The issuer a token must name. Defaults to <see cref="Authority" /> when
	///     a provider publishes both under one URL.
	/// </summary>
	public string? Issuer { get; set; }

	/// <summary>
	///     Which claim carries the role. Configurable because providers disagree:
	///     Auth0 wants a namespaced claim, Cognito emits <c>cognito:groups</c>. The
	///     claim's <em>values</em> are not configurable — they are this
	///     repository's invariant codes.
	/// </summary>
	public string RoleClaimType { get; set; } = "roles";

	/// <summary>
	///     The symmetric key the development issuer signs with. A throwaway, never
	///     set outside Development, and never a production secret — production
	///     validates against the provider's published keys and holds no signing key
	///     of its own. See ADR-0066.
	/// </summary>
	public string? DevelopmentSigningKey { get; set; }

	/// <summary>
	///     How long a development token lasts. Long enough for a working day,
	///     because there is no refresh flow to build — refresh is the provider's
	///     job in production.
	/// </summary>
	public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(8);
}
