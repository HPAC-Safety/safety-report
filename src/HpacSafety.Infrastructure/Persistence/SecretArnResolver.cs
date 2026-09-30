namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
///     Resolves a single plain-string secret from Secrets Manager by ARN when
///     one is configured, mirroring <see cref="DatabaseConnectionStringResolver" />'s
///     ARN pattern (#586) for a value that is already a plain string rather
///     than RDS's username/password JSON: the CloudFront origin-verification
///     header value (API, ADR-0159/ADR-0163) and the Gemini API key
///     (Worker and API, #597, ADR-0179). Each caller reads its own ARN setting once, at
///     cold start, before the host builds.
/// </summary>
public static class SecretArnResolver
{
	/// <summary>
	///     With <paramref name="secretArn" /> set — every deployed environment
	///     — reads that secret's current value from Secrets Manager and
	///     returns it, ignoring <paramref name="plainValue" />. Left unset —
	///     Development, and every test host — returns
	///     <paramref name="plainValue" /> unchanged.
	/// </summary>
	/// <param name="plainValue">The plain configured value, used when no ARN is configured.</param>
	/// <param name="secretArn">
	///     The secret's ARN, or <see langword="null" /> or blank when this
	///     value is not read from Secrets Manager.
	/// </param>
	/// <param name="secretReader">
	///     Reads the named secret's current value. Optional so a test can
	///     supply a fake; defaults to <see cref="SecretsManagerSecretValueReader" />
	///     when not given.
	/// </param>
	/// <param name="cancellationToken">Cancels the Secrets Manager call.</param>
	public static async Task<string?> ResolveAsync(
		string? plainValue,
		string? secretArn,
		ISecretValueReader? secretReader = null,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(secretArn))
		{
			return plainValue;
		}

		var reader = secretReader ?? new SecretsManagerSecretValueReader();
		return await reader.GetSecretValueAsync(secretArn, cancellationToken).ConfigureAwait(false);
	}
}
