namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
///     Reads one Secrets Manager secret's current string value. Third-party
///     coupling stays at this one small edge (ADR-0033) rather than exposing
///     the AWS SDK's own client type to <see cref="DatabaseConnectionStringResolver" />'s
///     caller — a test fakes this, never the SDK.
/// </summary>
public interface ISecretValueReader
{
	/// <summary>The named secret's current <c>SecretString</c> value.</summary>
	Task<string> GetSecretValueAsync(string secretId,
									 CancellationToken cancellationToken);
}
