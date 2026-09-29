using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HpacSafety.Core.Features.Moderation;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     Signs an RS256 token for staging, until a real identity provider
///     exists (issue #648, ADR-0172). Temporary and deliberately narrow: the
///     credential check is exactly <see cref="DevelopmentTokenIssuer" />'s —
///     the fixed accounts first, then the live members site (ADR-0079) — and
///     only this type's signature and issuer differ. Registered, and its
///     endpoints mapped, only where <c>HpacSafety:Authentication:InterimIssuer:Enabled</c>
///     is set and this host is not Development.
/// </summary>
/// <remarks>
///     Everything this type touches is named "Interim" so the whole feature —
///     this class, its options, its endpoints, its Terraform, its Secrets
///     Manager entry — deletes in one sweep once a real provider replaces it.
/// </remarks>
public sealed class InterimTokenIssuer : IMemberTokenIssuer
{
	/// <summary>
	///     The issuer every interim token names, and the only issuer
	///     <c>InterimIssuerParameters</c> accepts. A URN, not a URL: nothing
	///     ever fetches provider metadata from it (no Authority, no
	///     CloudFront/Lambda cycle) — the discovery document at
	///     <c>/api/auth/interim/.well-known/openid-configuration</c> exists
	///     only for a client that wants one, never for this API's own
	///     validation.
	/// </summary>
	public const string IssuerName = "urn:hpac-safety:interim-issuer";

	private readonly HpacAuthenticationOptions _options;
	private readonly IReadOnlyList<IDevelopmentCredentialSource> _sources;
	private readonly TimeProvider _time;
	private readonly InterimIssuerSigningKey _signingKey;

	/// <summary>Creates the issuer.</summary>
	public InterimTokenIssuer(
		IOptions<HpacAuthenticationOptions> options,
		IEnumerable<IDevelopmentCredentialSource> sources,
		TimeProvider time,
		InterimIssuerSigningKey signingKey)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(sources);

		_options = options.Value;
		_sources = [.. sources];
		_time = time;
		_signingKey = signingKey;
	}

	/// <inheritdoc />
	public async Task<DevelopmentToken?> Issue(
		string? username,
		string? password,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(username)
			|| string.IsNullOrEmpty(password))
		{
			return null;
		}

		var role = await ResolveRole(username, password, cancellationToken).ConfigureAwait(false);

		if (role is null)
		{
			return null;
		}

		var issuedAt = _time.GetUtcNow();
		var expiresAt = issuedAt + _options.TokenLifetime;

		// "member:", not "dev:" — this host is never Development.
		var subject = $"member:{username}";

		var token = new JwtSecurityToken(
			IssuerName,
			_options.Audience,
			[
				new Claim(JwtRegisteredClaimNames.Sub, subject),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("n")),
				new Claim(_options.RoleClaimType, MemberRoles.CodeFor(role.Value)),
			],
			issuedAt.UtcDateTime,
			expiresAt.UtcDateTime,
			new SigningCredentials(_signingKey.SecurityKey, SecurityAlgorithms.RsaSha256));

		return new DevelopmentToken(
			new JwtSecurityTokenHandler().WriteToken(token), expiresAt, subject, role.Value);
	}

	/// <summary>
	///     Tries each registered source in order, returning the first role one
	///     resolves.
	/// </summary>
	/// <remarks>
	///     Deliberately the same shape as <see cref="DevelopmentTokenIssuer" />'s
	///     private role resolution, kept as its own copy rather than a shared
	///     helper: this type is temporary and named for one-sweep deletion,
	///     and sharing code with the permanent <see cref="DevelopmentTokenIssuer" />
	///     would put that deletion back on the table.
	/// </remarks>
	private async Task<MemberRole?> ResolveRole(
		string username,
		string password,
		CancellationToken cancellationToken)
	{
		foreach (var source in _sources)
		{
			var role = await source.Verify(username, password, cancellationToken).ConfigureAwait(false);

			if (role is not null)
			{
				return role;
			}
		}

		return null;
	}
}
