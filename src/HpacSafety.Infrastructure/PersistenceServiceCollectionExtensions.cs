using HpacSafety.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Infrastructure;

/// <summary>Registers the database context.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>The connection string name in configuration.</summary>
    public const string ConnectionStringName = "HpacSafety";

    /// <summary>
    /// Adds <see cref="HpacSafetyDbContext"/>.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="configuration">
    /// Application configuration. The connection string comes from
    /// <c>ConnectionStrings:HpacSafety</c>. Storage and transport encryption are
    /// managed by PostgreSQL and TLS; there is no application-side key to
    /// configure. See ADR-0019 (superseded).
    /// </param>
    public static IServiceCollection AddHpacSafetyPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"No connection string named '{ConnectionStringName}' is configured.");

        services.AddDbContext<HpacSafetyDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
