namespace HpacSafety.Api.Authentication;

/// <summary>
///     Mints a member token for a credential pair, whichever issuer is
///     registered for this host: <see cref="DevelopmentTokenIssuer" /> in
///     Development, or the temporary <see cref="InterimTokenIssuer" />
///     (issue #648, ADR-0172) where it is enabled. <c>AuthEndpoints</c>'s
///     <c>/api/auth/token</c> handler depends on this, not a concrete issuer,
///     so the same endpoint code serves either one.
/// </summary>
public interface IMemberTokenIssuer
{
	/// <summary>
	///     Issues a token for a credential pair any registered
	///     <see cref="IDevelopmentCredentialSource" /> recognizes, or
	///     <c>null</c> when none does. See <see cref="DevelopmentTokenIssuer.Issue" />
	///     for what a caller does with a null result and with a thrown
	///     <see cref="MembersSiteUnavailableException" />.
	/// </summary>
	Task<DevelopmentToken?> Issue(string? username,
								  string? password,
								  CancellationToken cancellationToken);
}
