using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HpacSafety.Core.Features.Moderation;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     Signs a development token, so a developer can be each of the three roles
///     without an identity provider account.
/// </summary>
/// <remarks>
///     <para>
///         The token is <b>genuinely signed</b> and travels the same middleware,
///         validation parameters, and policies production uses. Only the issuer and the
///         key differ. A mock that skipped validation would only prove the mock worked.
///         See ADR-0066.
///     </para>
///     <para>
///         Registered only in Development, and the endpoint that exposes it is mapped
///         only in Development, so outside it there is no code path to reach — the
///         route is a 404 rather than a 401.
///     </para>
///     <para>
///         Verification itself is delegated to each registered
///         <see cref="IDevelopmentCredentialSource" />, tried in order — the
///         fixed accounts first, then the live members site (ADR-0078). This
///         type's only job is minting the token once a source resolves a role.
///     </para>
/// </remarks>
public sealed class DevelopmentTokenIssuer
{
	/// <summary>The issuer a development token names.</summary>
	public const string IssuerName = "https://localhost/hpac-safety-dev";

	/// <summary>
	///     The shortest key this will sign with. HS256 keys shorter than 256 bits
	///     weaken the signature, and a developer should not learn a habit here that
	///     would be wrong anywhere else.
	/// </summary>
	public const int MinimumKeyBytes = 32;

	private readonly HpacAuthenticationOptions _options;
	private readonly IReadOnlyList<IDevelopmentCredentialSource> _sources;
	private readonly TimeProvider _time;

	/// <summary>Creates the issuer.</summary>
	public DevelopmentTokenIssuer(
		IOptions<HpacAuthenticationOptions> options,
		IEnumerable<IDevelopmentCredentialSource> sources,
		TimeProvider time)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(sources);

		_options = options.Value;
		_sources = [.. sources];
		_time = time;
	}

	/// <summary>The signing key, as the validation side must also derive it.</summary>
	public static SymmetricSecurityKey KeyFrom(string signingKey)
	{
		return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
	}

	/// <summary>
	///     Issues a token for a credential pair any registered source recognizes,
	///     or <c>null</c> when none does.
	/// </summary>
	/// <remarks>
	///     The caller turns <c>null</c> into one generic failure. Nothing
	///     distinguishes an unknown username from a wrong password, or which
	///     source was tried, here or in the response. An
	///     <see cref="MembersSiteUnavailableException" /> from a source
	///     propagates rather than being treated as a non-match — a down members
	///     site is not the same failure as a wrong password.
	/// </remarks>
	public async Task<DevelopmentToken?> IssueAsync(
		string? username, string? password, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
		{
			return null;
		}

		var role = await ResolveRoleAsync(username, password, cancellationToken).ConfigureAwait(false);

		if (role is null)
		{
			return null;
		}

		var signingKey = _options.DevelopmentSigningKey
						 ?? throw new InvalidOperationException("The development signing key is not configured.");

		var issuedAt = _time.GetUtcNow();
		var expiresAt = issuedAt + _options.TokenLifetime;
		var subject = $"dev:{username}";

		var token = new JwtSecurityToken(
			IssuerName,
			_options.Audience,
			[
				new Claim(JwtRegisteredClaimNames.Sub, subject),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("n")),
				new Claim(_options.RoleClaimType, MemberRoles.CodeFor(role.Value))
			],
			issuedAt.UtcDateTime,
			expiresAt.UtcDateTime,
			new SigningCredentials(KeyFrom(signingKey), SecurityAlgorithms.HmacSha256));

		return new DevelopmentToken(
			new JwtSecurityTokenHandler().WriteToken(token), expiresAt, subject, role.Value);
	}

	private async Task<MemberRole?> ResolveRoleAsync(
		string username, string password, CancellationToken cancellationToken)
	{
		foreach (var source in _sources)
		{
			var role = await source.VerifyAsync(username, password, cancellationToken).ConfigureAwait(false);

			if (role is not null)
			{
				return role;
			}
		}

		return null;
	}
}

/// <summary>A signed development token and what it says.</summary>
/// <param name="AccessToken">The compact JWT, to be sent as a bearer token.</param>
/// <param name="ExpiresAt">When it stops being valid.</param>
/// <param name="Subject">The subject claim it carries.</param>
/// <param name="Role">The role claim it carries.</param>
public sealed record DevelopmentToken(
	string AccessToken,
	DateTimeOffset ExpiresAt,
	string Subject,
	MemberRole Role);
