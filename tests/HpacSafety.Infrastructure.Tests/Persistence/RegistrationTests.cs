using HpacSafety.Infrastructure;
using HpacSafety.Infrastructure.Persistence;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>Wiring the database into an application.</summary>
public sealed class RegistrationTests
{
    private static IConfiguration ConfigurationWith(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HpacSafety"] = connectionString,
            })
            .Build();

    [Fact]
    public void Given_a_configured_application_When_persistence_is_added_Then_the_context_resolves()
    {
        // Given
        var services = new ServiceCollection();
        services.AddHpacSafetyPersistence(ConfigurationWith("Host=nowhere;Database=unused"));

        // When
        using var provider = services.BuildServiceProvider();

        // Then
        provider.GetRequiredService<HpacSafetyDbContext>().ShouldNotBeNull();
    }

    [Fact]
    public void Given_no_connection_string_When_persistence_is_added_Then_the_application_refuses_to_start()
    {
        // Given
        var services = new ServiceCollection();

        // When / Then — a database nobody configured is not a database to guess at.
        Should.Throw<InvalidOperationException>(
            () => services.AddHpacSafetyPersistence(ConfigurationWith(null)));
    }
}
