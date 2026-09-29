using System.Security.Cryptography;
using HpacSafety.Api.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     Registration behavior for the temporary interim issuer (issue #648,
///     ADR-0172): enabled only outside Development, refuses to start
///     alongside a configured Authority, and requires its signing key. See
///     <see cref="AuthenticationRegistrationTests" /> for the Development and
///     no-Authority cases this does not repeat.
/// </summary>
public sealed class InterimIssuerRegistrationTests
{
	private static readonly string SigningKeyPem = GenerateRsaPem();

	[Fact]
	public void GivenInterimIssuerEnabled_WhenAuthenticationIsRegistered_ThenInterimIssuerResolvesAsTheMemberTokenIssuer()
	{
		// Given
		var services = Build(new Dictionary<string, string?>
		{
			["HpacSafety:Authentication:InterimIssuer:Enabled"] = "true",
			["HpacSafety:Authentication:InterimIssuer:SigningKeyPem"] = SigningKeyPem,
		});

		// When
		var issuer = services.GetService<IMemberTokenIssuer>();

		// Then
		issuer.ShouldBeOfType<InterimTokenIssuer>();
	}

	[Fact]
	public void GivenInterimIssuerEnabled_WhenAuthenticationIsRegistered_ThenNoDevelopmentTokenIssuerExists()
	{
		// Given
		var services = Build(new Dictionary<string, string?>
		{
			["HpacSafety:Authentication:InterimIssuer:Enabled"] = "true",
			["HpacSafety:Authentication:InterimIssuer:SigningKeyPem"] = SigningKeyPem,
		});

		// When
		var issuer = services.GetService<DevelopmentTokenIssuer>();

		// Then — Development's own issuer is a separate, unaffected type.
		issuer.ShouldBeNull();
	}

	[Fact]
	public void GivenInterimIssuerEnabledInDevelopment_WhenAuthenticationIsRegistered_ThenTheDevelopmentIssuerIsUsedInstead()
	{
		// Given — the owner's decision: Development keeps its HS256 issuer
		// regardless of this setting.
		var services = Build(
			new Dictionary<string, string?>
			{
				["HpacSafety:Authentication:DevelopmentSigningKey"] = "hpac-safety-registration-test-signing-key",
				["HpacSafety:Authentication:InterimIssuer:Enabled"] = "true",
				["HpacSafety:Authentication:InterimIssuer:SigningKeyPem"] = SigningKeyPem,
			},
			development: true);

		// When
		var issuer = services.GetService<IMemberTokenIssuer>();

		// Then
		issuer.ShouldBeOfType<DevelopmentTokenIssuer>();
	}

	[Fact]
	public void GivenInterimIssuerEnabledWithNoSigningKey_WhenAuthenticationIsRegistered_ThenItFailsLoudly()
	{
		// Given / When
		var registering = () => Build(new Dictionary<string, string?>
		{
			["HpacSafety:Authentication:InterimIssuer:Enabled"] = "true",
		});

		// Then
		var exception = Should.Throw<InvalidOperationException>(registering);
		exception.Message.ShouldContain("SigningKeyPem");
	}

	[Fact]
	public void GivenInterimIssuerEnabledAlongsideAConfiguredAuthority_WhenAuthenticationIsRegistered_ThenItFailsLoudly()
	{
		// Given / When — a real provider replaces the interim issuer; the two
		// are never meant to run together.
		var registering = () => Build(new Dictionary<string, string?>
		{
			["HpacSafety:Authentication:Authority"] = "https://provider.example.test",
			["HpacSafety:Authentication:InterimIssuer:Enabled"] = "true",
			["HpacSafety:Authentication:InterimIssuer:SigningKeyPem"] = SigningKeyPem,
		});

		// Then
		var exception = Should.Throw<InvalidOperationException>(registering);
		exception.Message.ShouldContain("Authority");
	}

	[Fact]
	public void GivenInterimIssuerDisabled_WhenAuthenticationIsRegistered_ThenNoInterimIssuerExists()
	{
		// Given — the default: disabled, as production.tfvars sets it.
		var services = Build([]);

		// When
		var issuer = services.GetService<InterimTokenIssuer>();

		// Then
		issuer.ShouldBeNull();
	}

	private static ServiceProvider Build(Dictionary<string, string?> settings,
										 bool development = false)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

		return new ServiceCollection()
			.AddLogging()
			.AddHpacSafetyAuthentication(configuration, development)
			.AddSingleton(TimeProvider.System)
			.BuildServiceProvider();
	}

	private static string GenerateRsaPem()
	{
		using var rsa = RSA.Create(2048);
		return rsa.ExportPkcs8PrivateKeyPem();
	}
}
