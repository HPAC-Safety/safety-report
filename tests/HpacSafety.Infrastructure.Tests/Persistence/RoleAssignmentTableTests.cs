using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.RoleAssignments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     <c>AddRoleAssignments</c> keeps role assignments correct in the database
///     (CON-DP-017, ADR-0199): one active assignment per email, a normalised email,
///     a revoke pair set together once, and no delete, truncate, or other update.
///     Emails here are synthetic <c>example.com</c> addresses.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedPostgres.Name)]
public sealed class RoleAssignmentTableTests(PostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task GivenAnActiveAssignment_WhenRevokedThroughTheModel_ThenItLeavesTheActiveViewAndTheEmailCanBeAssignedAgain()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			context.RoleAssignments.Add(RoleAssignment.Grant(" Officer@Example.COM ", MemberRole.SafetyOfficer, "granter", At));
			await context.SaveChangesAsync(TestContext.Current.CancellationToken);
		}

		// When
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			var assignment = await context.RoleAssignments.SingleAsync(TestContext.Current.CancellationToken);
			assignment.Email.ShouldBe("officer@example.com");
			assignment.Revoke("revoker", At.AddHours(1));
			await context.SaveChangesAsync(TestContext.Current.CancellationToken);
		}

		// Then
		await using (var context = PostgresFixture.ContextFor(connectionString))
		{
			(await context.ActiveRoleAssignments.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
			context.RoleAssignments.Add(RoleAssignment.Grant("officer@example.com", MemberRole.Administrator, "granter", At.AddHours(2)));
			await context.SaveChangesAsync(TestContext.Current.CancellationToken);

			var active = await context.ActiveRoleAssignments.SingleAsync(TestContext.Current.CancellationToken);
			active.Email.ShouldBe("officer@example.com");
			active.Role.ShouldBe(MemberRole.Administrator);
			(await context.RoleAssignments.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
		}
	}

	[Theory]
	[InlineData("INSERT INTO role_assignments VALUES ('bbbbbbbbbbb','officer@example.com','administrator',now(),'g',NULL,NULL)", "ux_role_assignments_active_email")]
	[InlineData("INSERT INTO role_assignments VALUES ('bbbbbbbbbbb','Other@example.com','administrator',now(),'g',NULL,NULL)", "ck_role_assignments_email")]
	[InlineData("INSERT INTO role_assignments VALUES ('bbbbbbbbbbb',' other@example.com','administrator',now(),'g',NULL,NULL)", "ck_role_assignments_email")]
	[InlineData("INSERT INTO role_assignments VALUES ('bbbbbbbbbbb','other@example.com','user',now(),'g',NULL,NULL)", "ck_role_assignments_role")]
	[InlineData("UPDATE role_assignments SET revoked_at = now()", "ck_role_assignments_revoked_together")]
	[InlineData("UPDATE role_assignments SET revoked_by_subject = 'r'", "ck_role_assignments_revoked_together")]
	public async Task GivenAnActiveAssignment_WhenAStatementBreaksAConstraint_ThenTheDatabaseRefusesIt(string statement,
																											 string constraint)
	{
		// Given
		await using var connection = await SeededConnection();

		// When
		var refusal = await Should.ThrowAsync<PostgresException>(() => Execute(connection, statement));

		// Then
		refusal.ConstraintName.ShouldBe(constraint);
	}

	[Theory]
	[InlineData("DELETE FROM role_assignments", "role_assignments rows are never deleted")]
	[InlineData("TRUNCATE role_assignments", "role_assignments rows are never deleted")]
	[InlineData("UPDATE role_assignments SET email = 'other@example.com'", "role_assignments.email cannot be changed")]
	[InlineData("UPDATE role_assignments SET role = 'safety_officer'", "role_assignments.role cannot be changed")]
	[InlineData("UPDATE role_assignments SET granted_at = now()", "role_assignments.granted_at cannot be changed")]
	[InlineData("UPDATE role_assignments SET granted_by_subject = 'x'", "role_assignments.granted_by_subject cannot be changed")]
	public async Task GivenAnActiveAssignment_WhenErasedOrChanged_ThenTheDatabaseRefusesIt(string statement,
																								string message)
	{
		// Given
		await using var connection = await SeededConnection();

		// When
		var refusal = await Should.ThrowAsync<PostgresException>(() => Execute(connection, statement));

		// Then
		refusal.MessageText.ShouldBe(message);
		refusal.SqlState.ShouldBe("23000");
		(await Scalar(connection, "SELECT email FROM role_assignments")).ShouldBe("officer@example.com");
	}

	[Theory]
	[InlineData("DELETE FROM role_assignments")]
	[InlineData("TRUNCATE role_assignments")]
	[InlineData("UPDATE role_assignments SET email = 'other@example.com'")]
	[InlineData("UPDATE role_assignments SET role = 'safety_officer'")]
	[InlineData("UPDATE role_assignments SET revoked_at = now(), revoked_by_subject = 'again'")]
	[InlineData("UPDATE role_assignments SET revoked_at = NULL, revoked_by_subject = NULL")]
	public async Task GivenARevokedAssignment_WhenErasedOrChanged_ThenTheDatabaseRefusesIt(string statement)
	{
		// Given
		await using var connection = await SeededConnection();
		await Execute(connection, "UPDATE role_assignments SET revoked_at = '2026-10-16T13:00:00Z', revoked_by_subject = 'revoker'");

		// When
		var refusal = await Should.ThrowAsync<PostgresException>(() => Execute(connection, statement));

		// Then
		refusal.SqlState.ShouldBe("23000");
		(await Scalar(connection, "SELECT revoked_by_subject FROM role_assignments")).ShouldBe("revoker");
	}

	private async Task<NpgsqlConnection> SeededConnection()
	{
		var migrated = await postgres.CreateMigratedDatabase();

		// One database per case: drop the migrator's idle pooled connection so the
		// server never runs out of client slots, and do not pool this one either.
		NpgsqlConnection.ClearAllPools();
		var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(migrated) { Pooling = false }.ConnectionString);
		await connection.OpenAsync();
		await Execute(connection, "INSERT INTO role_assignments VALUES ('aaaaaaaaaaa','officer@example.com','administrator',now(),'granter',NULL,NULL)");
		return connection;
	}

	private static async Task Execute(NpgsqlConnection connection,
									  string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<string?> Scalar(NpgsqlConnection connection,
											  string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		var value = await command.ExecuteScalarAsync();
		return value is null or DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
	}
}
