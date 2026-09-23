using System.Globalization;
using HpacSafety.Core;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
///     <b>History. This seeds a table that no longer exists.</b>
/// </summary>
/// <remarks>
///     <para>
///         It wrote one obviously-fake local administrator into <c>admin_users</c> so a
///         developer could open the admin UI on a database they had just created. That
///         table is dropped by <c>DropAdminUsersForJwtIdentity</c>, and roles now come
///         from a claim on a validated token rather than from a row — see ADR-0065. A
///         developer signs in against the development token issuer instead (ADR-0066).
///     </para>
///     <para>
///         This class survives only because <c>20260823001528_InitialSchema</c> calls
///         it, and a committed migration is never edited. On a fresh database the
///         insert still runs and the later migration drops what it wrote; on an
///         existing one the guard was almost certainly never set. Either way it is
///         inert. Do not call it from anything new.
///     </para>
///     <para>
///         The guard is written into the SQL rather than evaluated in C#. A C# guard
///         would be evaluated on whichever machine ran <c>dotnet ef migrations script</c>
///         — so a script generated on a laptop would carry the insert into production
///         with the guard already resolved to "yes". Reading a PostgreSQL setting at
///         apply time means the decision is taken by the database being changed, which
///         is the only machine that knows whether it is production. See ADR-0020.
///     </para>
/// </remarks>
public static class DevelopmentAdminSeed
{
	/// <summary>
	///     The PostgreSQL setting that has to be <c>true</c> for the row to be
	///     written. Unset — which is what every database is until somebody says
	///     otherwise — means no.
	/// </summary>
	public const string SettingName = "hpac.seed_development_admin";

	/// <summary>
	///     The seeded identifier. Deliberately not a deliverable address: nothing
	///     can be sent to it, nobody can receive at it, and it is recognisable as a
	///     development artefact at a glance.
	/// </summary>
	public const string MemberIdentifier = "admin@localhost";

	/// <summary>
	///     The role code this wrote. A literal rather than
	///     <c>EnumCode.Of(AdminRole.Administrator)</c>, because that enum is gone
	///     and the SQL of a committed migration has to keep producing exactly the
	///     bytes it always did.
	/// </summary>
	private const string AdministratorRoleCode = "administrator";

	/// <summary>The identifier of the seeded row.</summary>
	public static TinyId Id => SeedIds.For($"admin_user:{MemberIdentifier}");

	/// <summary>
	///     The guarded insert. Safe to run against any database: it writes nothing
	///     unless <see cref="SettingName" /> is <c>true</c> on the connection
	///     applying it, and nothing again if the row is already there.
	/// </summary>
	public static string InsertSql()
	{
		return string.Create(
			CultureInfo.InvariantCulture,
			$"""
			 INSERT INTO admin_users (id, member_identifier, role, is_active, created_at)
			 SELECT '{Id}',
			        '{MemberIdentifier}',
			        '{AdministratorRoleCode}',
			        TRUE,
			        TIMESTAMPTZ '{QuestionBankSeed.SeededAt:yyyy-MM-dd HH:mm:sszzz}'
			 WHERE current_setting('{SettingName}', true) = 'true'
			   AND NOT EXISTS (SELECT 1 FROM admin_users WHERE member_identifier = '{MemberIdentifier}');
			 """);
	}
}
