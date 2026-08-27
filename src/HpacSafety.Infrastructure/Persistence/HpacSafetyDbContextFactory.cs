using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HpacSafety.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without booting an application.
/// </summary>
/// <remarks>
/// This project is both the migrations project and the startup project for
/// <c>dotnet ef</c>, so scaffolding a migration needs no running API and no
/// deployment configuration. See <c>src/HpacSafety.Infrastructure/README.md</c>.
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
            .Options;

        return new HpacSafetyDbContext(options);
    }
}
