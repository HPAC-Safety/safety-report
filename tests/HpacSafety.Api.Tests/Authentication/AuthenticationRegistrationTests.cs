using HpacSafety.Api.Authentication;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
/// Which concrete registration the environment decision produces, and the
/// misconfigurations the host refuses to start on.
/// </summary>
/// <remarks>
/// A host that silently accepts nothing, or silently accepts everything, is
/// worse than one that will not start.
/// </remarks>
public sealed class AuthenticationRegistrationTests
{
    private const string GoodKey = "hpac-safety-registration-test-signing-key";

    [Fact]
    public void GivenDevelopment_WhenAuthenticationIsRegistered_ThenDevelopmentIssuerResolves()
    {
        // Given
        var services = Build(new() { ["HpacSafety:Authentication:DevelopmentSigningKey"] = GoodKey }, development: true);

        // When
        var issuer = services.GetService<DevelopmentTokenIssuer>();

        // Then
        issuer.ShouldNotBeNull();
    }

    [Fact]
    public void GivenNonDevelopmentEnvironment_WhenAuthenticationIsRegistered_ThenNoDevelopmentIssuerExists()
    {
        // Given
        var services = Build(new() { ["HpacSafety:Authentication:Authority"] = "https://provider.example.test" }, development: false);

        // When
        var issuer = services.GetService<DevelopmentTokenIssuer>();

        // Then — nothing can mint a token here, whatever configuration says.
        issuer.ShouldBeNull();
    }

    [Fact]
    public void GivenNoSigningKeyInDevelopment_WhenAuthenticationIsRegistered_ThenItFailsLoudly()
    {
        // Given / When
        var registering = () => Build([], development: true);

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
            new() { ["HpacSafety:Authentication:DevelopmentSigningKey"] = shortKey }, development: true);

        // Then
        var exception = Should.Throw<InvalidOperationException>(registering);
        exception.Message.ShouldContain("at least");
    }

    [Fact]
    public void GivenNoAuthorityOutsideDevelopment_WhenAuthenticationIsRegistered_ThenItFailsLoudly()
    {
        // Given / When — without an authority there are no keys to validate against
        var registering = () => Build([], development: false);

        // Then
        var exception = Should.Throw<InvalidOperationException>(registering);
        exception.Message.ShouldContain("Authority");
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings, bool development)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection()
            .AddLogging()
            .AddHpacSafetyAuthentication(configuration, useDevelopmentIssuer: development)
            .AddSingleton(TimeProvider.System)
            .BuildServiceProvider();
    }
}
