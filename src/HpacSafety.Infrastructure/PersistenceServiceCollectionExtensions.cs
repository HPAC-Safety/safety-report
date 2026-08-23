using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Encryption;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Infrastructure;

/// <summary>
/// Registers the database, the field cipher, and the context that binds them.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>The connection string name in configuration.</summary>
    public const string ConnectionStringName = "HpacSafety";

    /// <summary>
    /// Adds <see cref="HpacSafetyDbContext"/> and its dependencies.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="configuration">
    /// Application configuration. The connection string comes from
    /// <c>ConnectionStrings:HpacSafety</c> and the encryption key from
    /// <c>HpacSafety:FieldEncryption:Key</c> — a development literal locally, a
    /// Secrets Manager reference in production. See ADR-0019.
    /// </param>
    public static IServiceCollection AddHpacSafetyPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<FieldEncryptionOptions>(
            configuration.GetSection(FieldEncryptionOptions.SectionName));

        services.AddSingleton<IFieldCipher, AesGcmFieldCipher>();

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"No connection string named '{ConnectionStringName}' is configured.");

        services.AddDbContext<HpacSafetyDbContext>(options => options
            .UseNpgsql(connectionString)
            .ReplaceService<IModelCacheKeyFactory, FieldCipherModelCacheKeyFactory>());

        return services;
    }
}
