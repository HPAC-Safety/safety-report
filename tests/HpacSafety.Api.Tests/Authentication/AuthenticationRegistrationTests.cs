using HpacSafety.Api.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     Which concrete registration the environment decision produces, and the
///     misconfigurations the host refuses to start on.
/// </summary>
/// <remarks>
///     A host that silently accepts nothing, or silently accepts everything, is
///     worse than one that will not start.
/// </remarks>
public sealed class AuthenticationRegistrationTests
{
	private const string GoodKey = "hpac-safety-registration-test-signing-key";

	[Fact]
	public void GivenDevelopment_WhenAuthenticationIsRegistered_ThenDevelopmentIssuerResolves()
	{
		// Given
		var services = Build(new Dictionary<string, string?> { ["HpacSafety:Authentication:DevelopmentSigningKey"] = GoodKey }, true);

		// When
		var issuer = services.GetService<DevelopmentTokenIssuer>();

		// Then
		issuer.ShouldNotBeNull();
	}

	[Fact]
	public void GivenNonDevelopmentEnvironment_WhenAuthenticationIsRegistered_ThenNoDevelopmentIssuerExists()
	{
		// Given
		var services = Build(new Dictionary<string, string?> { ["HpacSafety:Authentication:Authority"] = "https://provider.example.test" }, false);

		// When
		var issuer = services.GetService<DevelopmentTokenIssuer>();

		// Then — nothing can mint a token here, whatever configuration says.
		issuer.ShouldBeNull();
	}

	[Fact]
	public void GivenNoSigningKeyInDevelopment_WhenAuthenticationIsRegistered_ThenItFailsLoudly()
	{
		// Given / When
		var registering = () => Build([], true);

		// Then
		var exception = Should.Throw<InvalidOperationException>(registering);
		exception.Message.ShouldContain("DevelopmentSigningKey");
	}

	[Fact]
	public void GivenSigningKeyShorterThanThirtyTwoBytes_WhenAuthenticationIsRegistered_ThenItFailsLoudly()
	{
		// Given — one byte short of the minimum
		var shortKey = new string('k', DevelopmentTokenIssuer.MinimumKeyBytes - 1);

		// When
		var registering = () => Build(
			new Dictionary<string, string?> { ["HpacSafety:Authentication:DevelopmentSigningKey"] = shortKey }, true);

		// Then
		var exception = Should.Throw<InvalidOperationException>(registering);
		exception.Message.ShouldContain("at least");
	}

	[Fact]
	public void GivenNoAuthorityOutsideDevelopment_WhenAuthenticationIsRegistered_ThenItDoesNotThrow()
	{
		// Given / When — an unconfigured identity provider is a stated
		// limitation (ADR-0158), not a reason for the host to refuse to start.
		var registering = () => Build([], false);

		// Then
		Should.NotThrow(registering);
	}

	[Fact]
	public void GivenNoAuthorityOutsideDevelopment_WhenAuthenticationIsRegistered_ThenNoDevelopmentIssuerExists()
	{
		// Given
		var services = Build([], false);

		// When
		var issuer = services.GetService<DevelopmentTokenIssuer>();

		// Then
		issuer.ShouldBeNull();
	}

	[Fact]
	public void GivenNoAuthorityOutsideDevelopment_WhenValidationParametersAreBuilt_ThenNoIssuerSigningKeyIsSet()
	{
		// Given
		var options = new HpacAuthenticationOptions();

		// When
		var parameters = HpacSafety.Api.Authentication.AuthenticationServiceCollectionExtensions.ValidationParametersFor(options, false);

		// Then — no key the handler could validate a signature against
		parameters.IssuerSigningKey.ShouldBeNull();
	}

	[Fact]
	public void GivenNoAuthorityOutsideDevelopment_WhenValidationParametersAreBuilt_ThenTheSigningKeyResolverReturnsNoKeys()
	{
		// Given
		var options = new HpacAuthenticationOptions();

		// When
		var parameters = HpacSafety.Api.Authentication.AuthenticationServiceCollectionExtensions.ValidationParametersFor(options, false);
		var resolved = parameters.IssuerSigningKeyResolver?.Invoke("token", null!, "kid", parameters);

		// Then
		resolved.ShouldNotBeNull();
		resolved.ShouldBeEmpty();
	}

	[Fact]
	public void GivenAConfiguredAuthorityOutsideDevelopment_WhenValidationParametersAreBuilt_ThenNoSigningKeyResolverIsSet()
	{
		// Given — the authority's published keys are fetched and rotated by the
		// handler itself; a configured environment keeps today's behavior.
		var options = new HpacAuthenticationOptions { Authority = "https://provider.example.test" };

		// When
		var parameters = HpacSafety.Api.Authentication.AuthenticationServiceCollectionExtensions.ValidationParametersFor(options, false);

		// Then
		parameters.IssuerSigningKeyResolver.ShouldBeNull();
	}

	private static ServiceProvider Build(Dictionary<string, string?> settings,
										 bool development)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		return new ServiceCollection()
			.AddLogging()
			.AddHpacSafetyAuthentication(configuration, development)
			.AddSingleton(TimeProvider.System)
			.BuildServiceProvider();
	}
}
