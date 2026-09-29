namespace HpacSafety.Api.Authentication;

/// <summary>
///     Configuration for the temporary interim issuer (issue #648, ADR-0172):
///     an RS256 identity provider this API runs itself, in staging only, until
///     a real provider is chosen (ADR-0064). Bound from the
///     <c>HpacSafety:Authentication:InterimIssuer</c> configuration section.
/// </summary>
/// <remarks>
///     Named "Interim" throughout — options, types, endpoints, Terraform,
///     Secrets Manager entry — so removing the whole feature once a real
///     provider exists is one sweep, not an audit. Never set in Development
///     (which keeps its own HS256 <see cref="DevelopmentTokenIssuer" />) and
///     never in production (<c>infra/production.tfvars</c> never sets the
///     flag).
/// </remarks>
public sealed class InterimIssuerOptions
{
	/// <summary>
	///     Turns the interim issuer on: it signs and validates its own RS256
	///     tokens and maps its endpoints. Honored only outside Development —
	///     Development keeps today's HS256 <see cref="DevelopmentTokenIssuer" />
	///     regardless of this setting.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	///     The RSA private key PEM the interim issuer signs with. Resolved from
	///     <see cref="SigningKeySecretArn" /> at cold start when one is
	///     configured (the same <c>SecretArnResolver</c> pattern
	///     <c>Program.cs</c> uses for the CloudFront origin secret and the
	///     DeepL key); set directly only by a test.
	/// </summary>
	public string? SigningKeyPem { get; set; }

	/// <summary>
	///     The Secrets Manager ARN holding <see cref="SigningKeyPem" />. Set by
	///     Terraform only when <c>interim_issuer_enabled</c> is true
	///     (<c>infra/secrets.tf</c>); never a value, only where to find it.
	/// </summary>
	public string? SigningKeySecretArn { get; set; }
}
