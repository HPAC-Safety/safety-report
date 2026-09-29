using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     The interim issuer's one RSA key pair (issue #648, ADR-0172), parsed
///     once at startup from <see cref="InterimIssuerOptions.SigningKeyPem" />.
///     <see cref="InterimTokenIssuer" /> signs with it; validation and
///     <c>/api/auth/interim/jwks</c> both read only its public half.
/// </summary>
public sealed class InterimIssuerSigningKey
{
	/// <summary>The RSA security key, its <c>kid</c> set from the public key itself.</summary>
	public RsaSecurityKey SecurityKey { get; }

	private InterimIssuerSigningKey(RsaSecurityKey securityKey)
	{
		SecurityKey = securityKey;
	}

	/// <summary>
	///     Parses a PEM-encoded RSA private key. Never throws away the private
	///     key material to anywhere but the in-process <see cref="RSA" /> this
	///     wraps.
	/// </summary>
	/// <exception cref="InvalidOperationException">The PEM does not parse as an RSA key.</exception>
	public static InterimIssuerSigningKey FromPem(string pem)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(pem);

		var rsa = RSA.Create();

		try
		{
			rsa.ImportFromPem(pem);
		}
		catch (Exception cause) when (cause is CryptographicException or FormatException)
		{
			rsa.Dispose();
			throw new InvalidOperationException(
				$"{HpacAuthenticationOptions.SectionName}:InterimIssuer:SigningKeyPem is not a valid PEM-encoded RSA private key.",
				cause);
		}

		var key = new RsaSecurityKey(rsa) { KeyId = KeyIdFrom(rsa) };
		return new InterimIssuerSigningKey(key);
	}

	/// <summary>
	///     A JWKS document publishing only the public key — never the private
	///     exponent or any other private field.
	/// </summary>
	public object ToJwks()
	{
		var parameters = SecurityKey.Rsa!.ExportParameters(includePrivateParameters: false);

		return new
		{
			keys = new[]
			{
				new
				{
					kty = "RSA",
					use = "sig",
					alg = "RS256",
					kid = SecurityKey.KeyId,
					n = Base64UrlEncoder.Encode(parameters.Modulus),
					e = Base64UrlEncoder.Encode(parameters.Exponent),
				},
			},
		};
	}

	/// <summary>
	///     A stable <c>kid</c> derived from the public key itself, so a JWKS
	///     consumer can tell keys apart without this system tracking a rotation
	///     counter it does not otherwise need — one key, one identity, for as
	///     long as this temporary issuer exists.
	/// </summary>
	private static string KeyIdFrom(RSA rsa)
	{
		var publicKey = rsa.ExportSubjectPublicKeyInfo();
		var hash = SHA256.HashData(publicKey);
		return Convert.ToHexString(hash)[..16].ToLowerInvariant();
	}
}
