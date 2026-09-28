using HpacSafety.Api.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests.Security;

/// <summary>
///     The origin-secret check must fail closed: a deployed environment
///     (Staging, Production — anything that is not Development) with no
///     configured secret must refuse to start, never silently let every
///     request through unverified. Same convention as
///     <c>AuthenticationRegistrationTests</c> for the same reason (ADR-0159).
/// </summary>
public sealed class OriginVerificationRegistrationTests
{
	[Fact]
	public void GivenNoSecretOutsideDevelopment_WhenRegistered_ThenItFailsLoudly()
	{
		// Given / When — "outside Development" covers Staging and Production
		// alike; the host takes this the same way AddHpacSafetyAuthentication
		// takes its environment decision, as a bool from the caller.
		var registering = () => Build([], useDevelopmentIssuer: false);

		// Then
		var exception = Should.Throw<InvalidOperationException>(registering);
		exception.Message.ShouldContain("Secret");
		exception.Message.ShouldContain("required outside Development");
	}

	[Fact]
	public void GivenABlankSecretOutsideDevelopment_WhenRegistered_ThenItFailsLoudly()
	{
		// Given / When — whitespace is not a secret.
		var registering = () => Build(
			new Dictionary<string, string?> { ["HpacSafety:Security:OriginVerification:Secret"] = "   " },
			useDevelopmentIssuer: false);

		// Then
		Should.Throw<InvalidOperationException>(registering);
	}

	[Fact]
	public void GivenNoSecretInDevelopment_WhenRegistered_ThenItStartsFine()
	{
		// Given / When — no CloudFront in front of a developer's machine.
		var registering = () => Build([], useDevelopmentIssuer: true);

		// Then
		Should.NotThrow(registering);
	}

	[Fact]
	public void GivenASecretOutsideDevelopment_WhenRegistered_ThenItStartsFine()
	{
		// Given / When
		var registering = () => Build(
			new Dictionary<string, string?> { ["HpacSafety:Security:OriginVerification:Secret"] = "a-real-secret" },
			useDevelopmentIssuer: false);

		// Then
		Should.NotThrow(registering);
	}

	private static void Build(Dictionary<string, string?> settings,
							  bool useDevelopmentIssuer)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
		var services = new ServiceCollection();
		services.AddHpacSafetyOriginVerification(configuration, useDevelopmentIssuer);
	}
}
