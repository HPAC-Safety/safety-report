using Amazon.SecretsManager.Model;
using HpacSafety.Infrastructure.Persistence;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     Reads the optional Super Administrator secret at cold start (ADR-0200,
///     CON-INF-028). Terraform creates the entry with no version and the deploy
///     fills it only where <c>SUPER_ADMIN</c> is set, yet every environment
///     carries the ARN, so an entry with no value is the normal "there is none"
///     and not a startup failure. Any other failure (access denied, throttling)
///     still stops the host: a Super Administrator that silently vanished would
///     be worse than a loud start.
/// </summary>
public static class SuperAdminSecret
{
	/// <summary>The configured email, or <c>null</c> where there is none.</summary>
	/// <param name="plainValue">The plain configured value (Development and tests).</param>
	/// <param name="secretArn">The Secrets Manager ARN, when deployed.</param>
	/// <param name="secretReader">A fake in a test; the AWS reader otherwise.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	public static async Task<string?> ResolveAsync(string? plainValue,
												   string? secretArn,
												   ISecretValueReader? secretReader = null,
												   CancellationToken cancellationToken = default)
	{
		try
		{
			var value = await SecretArnResolver.ResolveAsync(plainValue, secretArn, secretReader, cancellationToken)
				.ConfigureAwait(false);

			return string.IsNullOrWhiteSpace(value) ? null : value;
		}
		catch (ResourceNotFoundException)
		{
			return null;
		}
	}
}
