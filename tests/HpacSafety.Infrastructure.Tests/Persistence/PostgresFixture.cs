using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Encryption;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Npgsql;

using Testcontainers.PostgreSql;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// One PostgreSQL 17 container for the whole suite, with a fresh database per
/// test so nothing a test writes can be seen by another.
/// </summary>
/// <remarks>
/// The version is pinned rather than floating on <c>latest</c>: a database
/// version that moves underneath the suite is a failure nobody can reproduce.
/// See <c>docs/testing-conventions.md</c>.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>A 256-bit key. Tests that need a second, different key use <see cref="OtherKey"/>.</summary>
    public static string Key { get; } = Convert.ToBase64String(Enumerable.Repeat((byte)0x2f, 32).ToArray());

    /// <summary>A different 256-bit key, for proving a column cannot be read without the right one.</summary>
    public static string OtherKey { get; } = Convert.ToBase64String(Enumerable.Repeat((byte)0xd4, 32).ToArray());

    /// <summary>Starts the container.</summary>
    public Task InitializeAsync() => _postgres.StartAsync();

    /// <summary>Stops and removes the container.</summary>
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>
    /// Creates an empty database and returns a connection string for it.
    /// </summary>
    /// <param name="startupOptions">
    /// PostgreSQL <c>options</c> for the session, if any — for example
    /// <c>-c hpac.seed_development_admin=true</c>. This is how a development
    /// machine opts into the seeded local administrator.
    /// </param>
    public async Task<string> CreateDatabaseAsync(string? startupOptions = null)
    {
        var name = "db_" + Guid.NewGuid().ToString("n");

        await using (var maintenance = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await maintenance.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", maintenance);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = name,
            Options = startupOptions,
        }.ConnectionString;
    }

    /// <summary>
    /// Creates an empty database, applies every migration to it, and returns
    /// the connection string. This is <c>dotnet ef database update</c> against a
    /// clean PostgreSQL 17, run by the test.
    /// </summary>
    public async Task<string> CreateMigratedDatabaseAsync(string? startupOptions = null)
    {
        var connectionString = await CreateDatabaseAsync(startupOptions);
        await using var context = ContextFor(connectionString);
        await context.Database.MigrateAsync();
        return connectionString;
    }

    /// <summary>Opens a context against an existing database.</summary>
    /// <param name="connectionString">The database to open.</param>
    /// <param name="key">The field-encryption key, base64. Defaults to <see cref="Key"/>.</param>
    public static HpacSafetyDbContext ContextFor(string connectionString, string? key = null)
    {
        var options = new DbContextOptionsBuilder<HpacSafetyDbContext>()
            .UseNpgsql(connectionString)
            .ReplaceService<IModelCacheKeyFactory, FieldCipherModelCacheKeyFactory>()
            .Options;

        return new HpacSafetyDbContext(options, CipherFor(key ?? Key));
    }

    /// <summary>A cipher over the given base64 key.</summary>
    /// <param name="key">The base64 key.</param>
    public static IFieldCipher CipherFor(string key) => new AesGcmFieldCipher(new FieldEncryptionOptions { Key = key });
}

/// <summary>Shares one container across every integration test class.</summary>
[CollectionDefinition(Name)]
public sealed class SharedPostgres : ICollectionFixture<PostgresFixture>
{
    /// <summary>The collection name.</summary>
    public const string Name = "PostgreSQL 17";
}
