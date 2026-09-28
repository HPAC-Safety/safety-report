using System.Diagnostics.CodeAnalysis;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
///     Reads a secret through the AWS SDK's own credential and region
///     resolution — the function's Lambda execution role in every deployed
///     environment (ADR-0033: the only place this codebase names
///     <see cref="IAmazonSecretsManager" /> directly).
/// </summary>
/// <remarks>
///     Excluded from coverage for the same reason as <c>FfmpegVideoRemuxer</c>'s
///     process-launching methods and <see cref="Worker.AwsLambdaInvoker" />: a
///     thin wrapper with nothing to unit test except "does the AWS SDK's own
///     client work", which needs a real secret to answer.
///     <see cref="DatabaseConnectionStringResolver" /> — the class with an
///     actual decision to test — is covered exhaustively against a fake of
///     <see cref="ISecretValueReader" />.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class SecretsManagerSecretValueReader : ISecretValueReader
{
	/// <inheritdoc />
	public async Task<string> GetSecretValueAsync(string secretId,
												  CancellationToken cancellationToken)
	{
		using var client = new AmazonSecretsManagerClient();
		var response = await client
			.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretId }, cancellationToken)
			.ConfigureAwait(false);

		return response.SecretString;
	}
}
