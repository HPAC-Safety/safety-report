using HpacSafety.Infrastructure.Persistence;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <see cref="SecretArnResolver" /> resolves a plain-string secret from
///     Secrets Manager when an ARN is configured, and falls back to the plain
///     value otherwise — the shape every ARN-based secret in this repository
///     shares (#586, #597).
/// </summary>
public sealed class SecretArnResolverTests
{
	[Fact]
	public async Task GivenNoSecretArn_WhenResolved_ThenThePlainValueIsUsed()
	{
		// Given — the Development/test shape: no ARN at all.
		// When
		var value = await SecretArnResolver.ResolveAsync("a-plain-value", null, new NeverCalledSecretReader(), TestContext.Current.CancellationToken);

		// Then
		value.ShouldBe("a-plain-value");
	}

	[Fact]
	public async Task GivenABlankSecretArn_WhenResolved_ThenThePlainValueIsUsed()
	{
		// Given / When — whitespace is not an ARN.
		var value = await SecretArnResolver.ResolveAsync("a-plain-value", "   ", new NeverCalledSecretReader(), TestContext.Current.CancellationToken);

		// Then
		value.ShouldBe("a-plain-value");
	}

	[Fact]
	public async Task GivenASecretArn_WhenResolved_ThenTheSecretsManagerValueIsUsed()
	{
		// Given — the deployed shape: an ARN, ignoring whatever plain value
		// (typically none) is also configured.
		var reader = new StubSecretReader("the-real-secret");

		// When
		var value = await SecretArnResolver.ResolveAsync(null, "arn:aws:secretsmanager:ca-central-1:111111111111:secret:origin-secret", reader, TestContext.Current.CancellationToken);

		// Then
		value.ShouldBe("the-real-secret");
		reader.RequestedSecretId.ShouldBe("arn:aws:secretsmanager:ca-central-1:111111111111:secret:origin-secret");
	}

	[Fact]
	public async Task GivenASecretArnTheReaderCannotRead_WhenResolved_ThenTheFailurePropagates()
	{
		// Given — an ARN naming a secret this role cannot read, or that does
		// not exist; the caller must fail closed, not silently fall back.
		var reader = new AlwaysThrowsSecretReader();

		// When
		var resolving = () => SecretArnResolver.ResolveAsync(
			null, "arn:aws:secretsmanager:ca-central-1:111111111111:secret:origin-secret", reader);

		// Then
		await Should.ThrowAsync<InvalidOperationException>(resolving);
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
			throw new InvalidOperationException("Should not be called when no ARN is configured.");
		}
	}

	/// <summary>Stands in for an unreadable secret: denied access, or gone.</summary>
	private sealed class AlwaysThrowsSecretReader : ISecretValueReader
	{
		public Task<string> GetSecretValueAsync(string secretId,
												CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("The secret could not be read.");
		}
	}
}
