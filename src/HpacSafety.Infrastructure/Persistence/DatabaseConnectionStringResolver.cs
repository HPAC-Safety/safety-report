using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
///     Builds the Npgsql connection string the API and the Worker each open at
///     cold start (ADR-0055 applies to both the same way). Terraform can set
///     the database's host, port, and name — none of that is secret — but it
///     cannot write the connection string itself without putting the
///     RDS-managed master password in state, which ADR-0010 forbids for every
///     application secret. So a deployed environment names the secret instead
///     (<c>HpacSafety:Database:MasterSecretArn</c>), and this reads its
///     current value once, here, and assembles the real connection string
///     from it.
/// </summary>
/// <remarks>
///     Coordinated with #465, which grants the API's and the Worker's Lambda
///     roles <c>secretsmanager:GetSecretValue</c> on that one secret only
///     (<c>infra/lambda.tf</c>, <c>infra/iam.tf</c>).
/// </remarks>
public static class DatabaseConnectionStringResolver
{
	private const string HostKey = "HpacSafety:Database:Host";
	private const string PortKey = "HpacSafety:Database:Port";
	private const string NameKey = "HpacSafety:Database:Name";
	private const string MasterSecretArnKey = "HpacSafety:Database:MasterSecretArn";

	// RDS's managed master-user secret is lowercase JSON ({"username":…,
	// "password":…}); System.Text.Json is case-sensitive by default.
	private static readonly JsonSerializerOptions SecretJsonOptions = new() { PropertyNameCaseInsensitive = true };

	/// <summary>
	///     Assembles the connection string. With every one of
	///     <c>HpacSafety:Database:Host/Port/Name/MasterSecretArn</c> set — every
	///     deployed environment — reads the current username/password from the
	///     named Secrets Manager secret and builds the connection string from
	///     them, requiring TLS. Left unset — Development, and every test host —
	///     falls back to the plain <c>ConnectionStrings:HpacSafety</c> value,
	///     unchanged from before this existed.
	/// </summary>
	/// <param name="configuration">Application configuration.</param>
	/// <param name="secretReader">
	///     Reads the named secret's current value. Optional so a test can
	///     supply a fake; defaults to <see cref="SecretsManagerSecretValueReader" />
	///     when not given.
	/// </param>
	/// <param name="cancellationToken">Cancels the Secrets Manager call.</param>
	/// <exception cref="InvalidOperationException">
	///     Neither shape of configuration is present, or the named secret's
	///     value is not the <c>{"username":"…","password":"…"}</c> shape RDS's
	///     managed master-user secret always is.
	/// </exception>
	public static async Task<string> ResolveAsync(
		IConfiguration configuration,
		ISecretValueReader? secretReader = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var host = configuration[HostKey];
		var port = configuration[PortKey];
		var databaseName = configuration[NameKey];
		var masterSecretArn = configuration[MasterSecretArnKey];

		if (string.IsNullOrWhiteSpace(host)
			|| string.IsNullOrWhiteSpace(port)
			|| string.IsNullOrWhiteSpace(databaseName)
			|| string.IsNullOrWhiteSpace(masterSecretArn))
		{
			return configuration.GetConnectionString("HpacSafety")
				?? throw new InvalidOperationException(
					$"Neither {HostKey}/{PortKey}/{NameKey}/{MasterSecretArnKey} nor "
					+ "ConnectionStrings:HpacSafety is configured. One of the two is required.");
		}

		var reader = secretReader ?? new SecretsManagerSecretValueReader();
		var secretString = await reader.GetSecretValueAsync(masterSecretArn, cancellationToken).ConfigureAwait(false);

		RdsMasterUserSecret? credentials;
		try
		{
			credentials = JsonSerializer.Deserialize<RdsMasterUserSecret>(secretString, SecretJsonOptions);
		}
		catch (JsonException exception)
		{
			throw new InvalidOperationException(
				$"The secret at {masterSecretArn} is not the username/password JSON RDS's managed master-user secret holds.",
				exception);
		}

		if (credentials is null
			|| string.IsNullOrEmpty(credentials.Username)
			|| string.IsNullOrEmpty(credentials.Password))
		{
			throw new InvalidOperationException(
				$"The secret at {masterSecretArn} is not the username/password JSON RDS's managed master-user secret holds.");
		}

		var builder = new NpgsqlConnectionStringBuilder
		{
			Host = host,
			Port = int.Parse(port, CultureInfo.InvariantCulture),
			Database = databaseName,
			Username = credentials.Username,
			Password = credentials.Password,
			SslMode = SslMode.Require,
		};

		return builder.ConnectionString;
	}

	private sealed record RdsMasterUserSecret(string? Username, string? Password);
}
