using Amazon.SecretsManager.Model;
using HpacSafety.Api.Authentication;
using HpacSafety.Infrastructure.Persistence;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>The optional Super Administrator secret at cold start (ADR-0200, CON-INF-028).</summary>
public sealed class SuperAdminSecretTests
{
	[Fact]
	public async Task GivenAnEntryWithNoVersion_WhenResolved_ThenThereIsNoSuperAdministrator()
	{
		// Given — Terraform created the entry; the deploy never put a value.
		var reader = new FakeReader(() => throw new ResourceNotFoundException("no AWSCURRENT version"));

		// When
		var value = await SuperAdminSecret.ResolveAsync(null, "arn:aws:secretsmanager:ca-central-1:000000000000:secret:x", reader, TestContext.Current.CancellationToken);

		// Then
		value.ShouldBeNull();
	}

	[Fact]
	public async Task GivenAnEmptyValue_WhenResolved_ThenThereIsNoSuperAdministrator()
	{
		// Given
		var reader = new FakeReader(() => "  ");

		// When
		var value = await SuperAdminSecret.ResolveAsync(null, "arn:x", reader, TestContext.Current.CancellationToken);

		// Then
		value.ShouldBeNull();
	}

	[Fact]
	public async Task GivenAValue_WhenResolved_ThenItIsReturned()
	{
		// Given
		var reader = new FakeReader(() => "super@example.com");

		// When
		var value = await SuperAdminSecret.ResolveAsync(null, "arn:x", reader, TestContext.Current.CancellationToken);

		// Then
		value.ShouldBe("super@example.com");
	}

	[Fact]
	public async Task GivenNoArn_WhenResolved_ThenThePlainValueIsUsed()
	{
		// When
		var value = await SuperAdminSecret.ResolveAsync("local@example.com", null, new FakeReader(() => throw new InvalidOperationException()), TestContext.Current.CancellationToken);

		// Then
		value.ShouldBe("local@example.com");
	}

	[Fact]
	public async Task GivenAnyOtherFailure_WhenResolved_ThenTheHostStillStops()
	{
		// Given — access denied is a misconfiguration, not "none".
		var reader = new FakeReader(() => throw new InvalidOperationException("denied"));

		// When
		var resolving = () => SuperAdminSecret.ResolveAsync(null, "arn:x", reader, TestContext.Current.CancellationToken);

		// Then
		await Should.ThrowAsync<InvalidOperationException>(resolving);
	}

	private sealed class FakeReader(Func<string> read) : ISecretValueReader
	{
		public Task<string> GetSecretValueAsync(string secretId,
												CancellationToken cancellationToken)
		{
			return Task.FromResult(read());
		}
	}
}
