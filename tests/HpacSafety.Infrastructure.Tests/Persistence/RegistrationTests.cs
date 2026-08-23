using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Encryption;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// Wiring the database into an application, and the two settings it insists on.
/// </summary>
public sealed class RegistrationTests
{
    private static IConfiguration ConfigurationWith(string? connectionString, string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HpacSafety"] = connectionString,
                ["HpacSafety:FieldEncryption:Key"] = key,
            })
            .Build();

    [Fact]
    public void Given_a_configured_application_When_persistence_is_added_Then_the_context_and_its_cipher_resolve()
    {
        // Given
        var services = new ServiceCollection();
        services.AddHpacSafetyPersistence(
            ConfigurationWith("Host=nowhere;Database=unused", PostgresFixture.Key));

        // When
        using var provider = services.BuildServiceProvider();

        // Then
        provider.GetRequiredService<IFieldCipher>().ShouldBeOfType<AesGcmFieldCipher>();
        provider.GetRequiredService<HpacSafetyDbContext>().ShouldNotBeNull();
    }

    [Fact]
    public void Given_no_connection_string_When_persistence_is_added_Then_the_application_refuses_to_start()
    {
        // Given
        var services = new ServiceCollection();

        // When / Then — a database nobody configured is not a database to guess at.
        Should.Throw<InvalidOperationException>(
            () => services.AddHpacSafetyPersistence(ConfigurationWith(null, PostgresFixture.Key)));
    }

    [Fact]
    public void Given_no_encryption_key_When_the_cipher_is_resolved_Then_the_application_refuses_to_start()
    {
        // Given
        var services = new ServiceCollection();
        services.AddHpacSafetyPersistence(ConfigurationWith("Host=nowhere;Database=unused", key: null));

        // When
        using var provider = services.BuildServiceProvider();

        // Then — never a silent fallback to storing Restricted text in the clear.
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IFieldCipher>());
    }
}
