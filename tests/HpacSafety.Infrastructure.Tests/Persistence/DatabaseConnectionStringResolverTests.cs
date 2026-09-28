using HpacSafety.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <see cref="DatabaseConnectionStringResolver" /> assembles the connection
///     string from the RDS-managed master-user secret in every deployed
///     environment, and falls back to the plain connection string everywhere
///     else (#443, coordinated with #465).
/// </summary>
public sealed class DatabaseConnectionStringResolverTests
{
	[Fact]
	public async Task GivenNoDatabaseConfiguration_WhenResolved_ThenThePlainConnectionStringIsUsed()
	{
		// Given — the Development/test shape: no HpacSafety:Database:* at all.
		var configuration = Build(new Dictionary<string, string?>
		{
			["ConnectionStrings:HpacSafety"] = "Host=localhost;Database=hpac_safety",
		});

		// When
		var connectionString = await DatabaseConnectionStringResolver.ResolveAsync(configuration, new NeverCalledSecretReader());

		// Then
		connectionString.ShouldBe("Host=localhost;Database=hpac_safety");
	}

	[Fact]
	public async Task GivenNeitherShapeOfConfiguration_WhenResolved_ThenItFailsLoudly()
	{
		// Given
		var configuration = Build([]);

		// When
		var resolving = () => DatabaseConnectionStringResolver.ResolveAsync(configuration, new NeverCalledSecretReader());

		// Then
		var exception = await Should.ThrowAsync<InvalidOperationException>(resolving);
		exception.Message.ShouldContain("HpacSafety:Database");
	}

	[Fact]
	public async Task GivenFullDatabaseConfiguration_WhenResolved_ThenTheSecretsManagerCredentialsAreUsed()
	{
		// Given — the deployed shape: host/port/name plus the master secret's ARN.
		var configuration = Build(new Dictionary<string, string?>
		{
			["HpacSafety:Database:Host"] = "db.internal.example",
			["HpacSafety:Database:Port"] = "5432",
			["HpacSafety:Database:Name"] = "hpacsafety",
			["HpacSafety:Database:MasterSecretArn"] = "arn:aws:secretsmanager:ca-central-1:111111111111:secret:rds-master",
		});

		var reader = new StubSecretReader("""{"username":"hpacsafety","password":"s3cr3t!"}""");

		// When
		var connectionString = await DatabaseConnectionStringResolver.ResolveAsync(configuration, reader);

		// Then
		reader.RequestedSecretId.ShouldBe("arn:aws:secretsmanager:ca-central-1:111111111111:secret:rds-master");

		var parsed = new NpgsqlConnectionStringBuilder(connectionString);
		parsed.Host.ShouldBe("db.internal.example");
		parsed.Port.ShouldBe(5432);
		parsed.Database.ShouldBe("hpacsafety");
		parsed.Username.ShouldBe("hpacsafety");
		parsed.Password.ShouldBe("s3cr3t!");
		parsed.SslMode.ShouldBe(SslMode.Require);
	}

	[Fact]
	public async Task GivenASecretThatIsNotTheExpectedShape_WhenResolved_ThenItFailsLoudly()
	{
		// Given
		var configuration = Build(new Dictionary<string, string?>
		{
			["HpacSafety:Database:Host"] = "db.internal.example",
			["HpacSafety:Database:Port"] = "5432",
			["HpacSafety:Database:Name"] = "hpacsafety",
			["HpacSafety:Database:MasterSecretArn"] = "arn:aws:secretsmanager:ca-central-1:111111111111:secret:rds-master",
		});

		var reader = new StubSecretReader("""{"unexpected":"shape"}""");

		// When
		var resolving = () => DatabaseConnectionStringResolver.ResolveAsync(configuration, reader);

		// Then
		var exception = await Should.ThrowAsync<InvalidOperationException>(resolving);
		exception.Message.ShouldContain("rds-master");
	}

	[Fact]
	public async Task GivenASecretMissingItsPassword_WhenResolved_ThenItFailsLoudly()
	{
		// Given — a username with no password is still not the expected shape.
		var configuration = Build(new Dictionary<string, string?>
		{
			["HpacSafety:Database:Host"] = "db.internal.example",
			["HpacSafety:Database:Port"] = "5432",
			["HpacSafety:Database:Name"] = "hpacsafety",
			["HpacSafety:Database:MasterSecretArn"] = "arn:aws:secretsmanager:ca-central-1:111111111111:secret:rds-master",
		});

		var reader = new StubSecretReader("""{"username":"hpacsafety"}""");

		// When
		var resolving = () => DatabaseConnectionStringResolver.ResolveAsync(configuration, reader);

		// Then
		await Should.ThrowAsync<InvalidOperationException>(resolving);
	}

	[Theory]
	[InlineData("HpacSafety:Database:Host")]
	[InlineData("HpacSafety:Database:Port")]
	[InlineData("HpacSafety:Database:Name")]
	[InlineData("HpacSafety:Database:MasterSecretArn")]
	public async Task GivenExactlyOneDatabaseKeyIsMissing_WhenResolved_ThenThePlainConnectionStringIsUsed(string missingKey)
	{
		// Given — each of the four is independently required; leaving out any
		// one of them is the Development/test shape, not a partial deployed one.
		var settings = new Dictionary<string, string?>
		{
			["HpacSafety:Database:Host"] = "db.internal.example",
			["HpacSafety:Database:Port"] = "5432",
			["HpacSafety:Database:Name"] = "hpacsafety",
			["HpacSafety:Database:MasterSecretArn"] = "arn:aws:secretsmanager:ca-central-1:111111111111:secret:rds-master",
			["ConnectionStrings:HpacSafety"] = "Host=localhost;Database=hpac_safety",
		};
		settings.Remove(missingKey);
		var configuration = Build(settings);

		// When
		var connectionString = await DatabaseConnectionStringResolver.ResolveAsync(configuration, new NeverCalledSecretReader());

		// Then
		connectionString.ShouldBe("Host=localhost;Database=hpac_safety");
	}

	private static IConfiguration Build(Dictionary<string, string?> settings)
	{
		return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
	}

	private sealed class StubSecretReader(string secretString) : ISecretValueReader
	{
		public string? RequestedSecretId { get; private set; }

		public Task<string> GetSecretValueAsync(string secretId,
												CancellationToken cancellationToken)
		{
			RequestedSecretId = secretId;
			return Task.FromResult(secretString);
		}
	}

	/// <summary>Proves the fallback path never touches Secrets Manager at all.</summary>
	private sealed class NeverCalledSecretReader : ISecretValueReader
	{
		public Task<string> GetSecretValueAsync(string secretId,
												CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("Should not be called when the plain connection string is configured.");
		}
	}
}
