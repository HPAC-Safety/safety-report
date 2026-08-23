using System.Globalization;

using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
/// One obviously-fake local administrator, so a developer can open the admin UI
/// on a database they created five seconds ago.
/// </summary>
/// <remarks>
/// <para>
/// The real safety-officer allowlist is not here and is not seeded by any
/// migration. It is a later issue, and it is a list of real people that belongs
/// in an operational process rather than in source control.
/// </para>
/// <para>
/// The guard is written into the SQL rather than evaluated in C#. A C# guard
/// would be evaluated on whichever machine ran <c>dotnet ef migrations script</c>
/// — so a script generated on a laptop would carry the insert into production
/// with the guard already resolved to "yes". Reading a PostgreSQL setting at
/// apply time means the decision is taken by the database being changed, which
/// is the only machine that knows whether it is production. See ADR-0020.
/// </para>
/// </remarks>
public static class DevelopmentAdminSeed
{
    /// <summary>
    /// The PostgreSQL setting that has to be <c>true</c> for the row to be
    /// written. Unset — which is what every database is until somebody says
    /// otherwise — means no.
    /// </summary>
    public const string SettingName = "hpac.seed_development_admin";

    /// <summary>
    /// The seeded identifier. Deliberately not a deliverable address: nothing
    /// can be sent to it, nobody can receive at it, and it is recognisable as a
    /// development artefact at a glance.
    /// </summary>
    public const string Subject = "admin@localhost";

    /// <summary>The identifier of the seeded row.</summary>
    public static TinyId Id => SeedIds.For($"admin_user:{Subject}");

    /// <summary>
    /// The guarded insert. Safe to run against any database: it writes nothing
    /// unless <see cref="SettingName"/> is <c>true</c> on the connection
    /// applying it, and nothing again if the row is already there.
    /// </summary>
    public static string InsertSql() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"""
             INSERT INTO admin_users (id, subject, role, is_active, created_at)
             SELECT '{Id}',
                    '{Subject}',
                    '{EnumCode.Of(AdminRole.Administrator)}',
                    TRUE,
                    TIMESTAMPTZ '{QuestionBankSeed.SeededAt:yyyy-MM-dd HH:mm:sszzz}'
             WHERE current_setting('{SettingName}', true) = 'true'
               AND NOT EXISTS (SELECT 1 FROM admin_users WHERE subject = '{Subject}');
             """);
}
