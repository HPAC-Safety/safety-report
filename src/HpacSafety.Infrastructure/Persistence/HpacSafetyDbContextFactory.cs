using System.Security.Cryptography;

using HpacSafety.Infrastructure.Persistence.Encryption;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without booting an application.
/// </summary>
/// <remarks>
/// <para>
/// This project is both the migrations project and the startup project for
/// <c>dotnet ef</c>, so scaffolding a migration needs no running API and no
/// deployment configuration. See <c>src/HpacSafety.Infrastructure/README.md</c>.
/// </para>
/// <para>
/// The key below exists only so the model can be built. The shape of the model
/// does not depend on the key's value, and nothing this factory produces is
/// ever used to read or write a real row.
/// </para>
/// </remarks>
public sealed class HpacSafetyDbContextFactory : IDesignTimeDbContextFactory<HpacSafetyDbContext>
{
    /// <summary>
    /// The connection string design-time tooling uses when nothing else is set.
    /// </summary>
    public const string ConnectionStringVariable = "HPAC_SAFETY_CONNECTION";

    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=hpac_safety;Username=postgres;Password=postgres";

    /// <inheritdoc />
    public HpacSafetyDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<HpacSafetyDbContext>()
            .UseNpgsql(connection)
            .ReplaceService<IModelCacheKeyFactory, FieldCipherModelCacheKeyFactory>()
            .Options;

        return new HpacSafetyDbContext(options, DesignTimeCipher());
    }

    private static AesGcmFieldCipher DesignTimeCipher() =>
        new(new FieldEncryptionOptions
        {
            // A throwaway. Design-time tooling reads the model, never a row.
            Key = Convert.ToBase64String(SHA256.HashData("design-time, never used against data"u8.ToArray())),
        });
}
