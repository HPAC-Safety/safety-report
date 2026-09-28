namespace HpacSafety.Api.Security;

/// <summary>
///     Binds <c>HpacSafety:Security:OriginVerification</c>. See
///     <see cref="OriginVerificationMiddlewareExtensions" />.
/// </summary>
public sealed class OriginVerificationOptions
{
	/// <summary>The configuration section this binds.</summary>
	public const string SectionName = "HpacSafety:Security:OriginVerification";

	/// <summary>The header CloudFront injects and this API checks.</summary>
	public string HeaderName { get; set; } = "X-Origin-Verify";

	/// <summary>
	///     The shared secret CloudFront's origin request policy injects as
	///     <see cref="HeaderName" />'s value. In every deployed environment
	///     this is resolved from <see cref="SecretArn" /> at cold start
	///     (<see cref="OriginVerificationServiceCollectionExtensions" />), not
	///     read from configuration directly — left unset, the check does not
	///     run, which is the Development and test posture: there is no
	///     CloudFront in front of a developer's machine. Required outside
	///     Development. A missing value there fails the host at startup rather
	///     than silently answering every request that reaches the Function URL
	///     directly.
	/// </summary>
	public string? Secret { get; set; }

	/// <summary>
	///     The Secrets Manager ARN Terraform sets in every deployed environment
	///     (<c>infra/lambda.tf</c>). When present, <see cref="Secret" /> is
	///     resolved from this secret's current value at cold start instead of
	///     from <see cref="Secret" />'s own configured value; the Lambda
	///     environment never carries the secret's value itself (#597).
	/// </summary>
	public string? SecretArn { get; set; }
}
