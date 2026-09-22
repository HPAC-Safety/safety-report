using HpacSafety.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>Wiring the database into an application.</summary>
public sealed class RegistrationTests
{
    private static IConfiguration ConfigurationWith(string? connectionString)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HpacSafety"] = connectionString
            })
            .Build();
    }

    [Fact]
    public void GivenConfiguredApplication_WhenPersistenceIsAdded_ThenContextResolves()
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
    public void GivenNoConnectionString_WhenPersistenceIsAdded_ThenApplicationRefusesToStart()
    {
        // Given
        var services = new ServiceCollection();

        // When / Then — a database nobody configured is not a database to guess at.
        Should.Throw<InvalidOperationException>(() => services.AddHpacSafetyPersistence(ConfigurationWith(null)));
    }
}
