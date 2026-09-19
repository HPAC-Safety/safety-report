using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

using Testcontainers.PostgreSql;

namespace HpacSafety.Api.Tests;

/// <summary>
/// One PostgreSQL 17 container and one <see cref="WebApplicationFactory{TEntryPoint}"/>
/// shared across every test in the collection. The API migrates the container
/// itself at startup (<c>HpacSafetyDbContext.EnsureMigratedAsync</c>, ADR-0055),
/// so booting the factory at all proves that path works.
/// </summary>
public sealed class ApiPostgresFixture : IAsyncLifetime
{
    // Pinned rather than floating on `latest` — see PostgresContainerTests.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>The API, booted in process against the container above.</summary>
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:HpacSafety", _postgres.GetConnectionString()));
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>Shares one container and factory across every API test class.</summary>
[CollectionDefinition(Name)]
public sealed class SharedApiPostgres : ICollectionFixture<ApiPostgresFixture>
{
    /// <summary>The collection name.</summary>
    public const string Name = "API PostgreSQL";
}
