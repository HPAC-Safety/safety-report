using HpacSafety.Core.Features.Moderation;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     One way to verify a development credential pair. <see cref="DevelopmentTokenIssuer" />
///     tries each registered source in order and mints a token for the first
///     one that resolves a role. See ADR-0079.
/// </summary>
public interface IDevelopmentCredentialSource
{
	/// <summary>
	///     Verifies a credential pair, returning the role to grant on success or
	///     <c>null</c> when this source does not recognize the pair.
	/// </summary>
	/// <param name="username">The submitted username or email.</param>
	/// <param name="password">
	///     The submitted password. A source must not log or persist this beyond
	///     the call.
	/// </param>
	/// <param name="cancellationToken">Cancels the verification.</param>
	Task<MemberRole?> Verify(string username,
							 string password,
							 CancellationToken cancellationToken);
}
