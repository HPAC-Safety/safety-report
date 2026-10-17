using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using Npgsql;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Role assignments — REQ-AUTH-012 to REQ-AUTH-024, REQ-AUTH-026 to
///     REQ-AUTH-028 (#909, ADR-0199, ADR-0200). The API steps go through the
///     booted host's real policies; the storage steps are real SQL against the
///     scenario's own PostgreSQL database, past the application, so what they
///     prove is the database's trigger and not the entity. REQ-AUTH-019 and
///     REQ-AUTH-029 are <c>@ui</c> and run in <c>tests/e2e</c>.
/// </summary>
[Binding]
public sealed class RoleAssignmentSteps
{
	private const string RoleAssignments = "/api/admin/role-assignments";
	private const string OfficerEmail = "officer@example.com";

	private readonly RoleAssignmentWorld _world;

	/// <summary>Reqnroll hands each scenario its own world.</summary>
	public RoleAssignmentSteps(RoleAssignmentWorld world)
	{
		_world = world;
	}

	[AfterScenario]
	public async Task DropTheScenarioDatabase()
	{
		await _world.Release();
	}

	// --- Givens: who the members are ---

	[Given(@"a Super Administrator is configured")]
	public void GivenASuperAdministratorIsConfigured()
	{
		_world.SuperAdministratorConfigured = true;
	}

	[Given(@"^""([^""]+)"" has an active (Safety Officer|Administrator) role assignment$")]
	public async Task GivenAnActiveAssignment(string email,
											  string role)
	{
		_world.TargetEmail = email;
		_world.TargetId = await _world.Seed(email, GlossaryNames.Role(role));
	}

	[Given(@"the role assignments cannot be read")]
	public async Task GivenTheRoleAssignmentsCannotBeRead()
	{
		await _world.MakeUnreadable();
	}

	[Given(@"an Administrator whose role comes from their own role assignment")]
	public async Task GivenAnAdministratorFromAnAssignment()
	{
		const string email = "self.admin@example.com";
		_world.TargetEmail = email;
		_world.TargetId = await _world.Seed(email, MemberRole.Administrator);
		_world.Actor = await _world.SignIn(email);
		_world.Actor.Role.ShouldBe(MemberRoles.CodeFor(MemberRole.Administrator));
	}

	[Given(@"^a (Safety Officer|User)$")]
	public async Task GivenAMemberWithRole(string role)
	{
		if (role == "Safety Officer")
		{
			await _world.Seed("scenario.officer@example.com", MemberRole.SafetyOfficer);
			_world.Actor = await _world.SignIn("scenario.officer@example.com");
		}
		else
		{
			_world.Actor = await _world.SignIn("scenario.user@example.com");
		}

		_world.Actor.Role.ShouldBe(MemberRoles.CodeFor(GlossaryNames.Role(role)));
	}

	[Given(@"^a member whose (Safety Officer|Administrator) role comes from an active role assignment$")]
	public async Task GivenAMemberFromAnAssignment(string role)
	{
		var email = role == "Safety Officer" ? "assigned.officer@example.com" : "assigned.admin@example.com";
		_world.TargetEmail = email;
		_world.TargetId = await _world.Seed(email, GlossaryNames.Role(role));
		_world.Actor = await _world.SignIn(email);
		_world.Actor.Role.ShouldBe(MemberRoles.CodeFor(GlossaryNames.Role(role)));
	}

	[Given(@"^""([^""]+)"" has no role assignment$")]
	public void GivenNoAssignment(string email)
	{
		_world.TargetEmail = email;
	}

	[Given(@"an Administrator has since removed that role assignment")]
	public async Task GivenAnAdministratorHasSinceRemovedIt()
	{
		var administrator = await _world.SignIn(RoleAssignmentWorld.SuperAdministratorEmail);
		using var response = await administrator.Client.PostAsync(
			new Uri($"{RoleAssignments}/{_world.TargetId}/revoke", UriKind.Relative), content: null);

		response.EnsureSuccessStatusCode();
	}

	// --- Whens: the API ---

	[When(@"^the Administrator assigns the (Safety Officer|Administrator) role to ""([^""]+)""$")]
	public async Task WhenTheAdministratorAssigns(string role,
												  string email)
	{
		await Assign(role, email);
	}

	[When(@"^an Administrator assigns the (Safety Officer|Administrator) role to ""([^""]+)""$")]
	public async Task WhenAnAdministratorAssigns(string role,
												 string email)
	{
		await Assign(role, email);
	}

	[When(@"an Administrator removes that role assignment")]
	public async Task WhenAnAdministratorRemovesIt()
	{
		var administrator = await _world.Administrator();
		_world.LastResponse = await administrator.Client.PostAsync(
			new Uri($"{RoleAssignments}/{_world.TargetId}/revoke", UriKind.Relative), content: null);
	}

	[When(@"the Administrator removes their own role assignment")]
	public async Task WhenTheAdministratorRemovesTheirOwn()
	{
		_world.LastResponse = await _world.Actor!.Client.PostAsync(
			new Uri($"{RoleAssignments}/{_world.TargetId}/revoke", UriKind.Relative), content: null);
	}

	[When(@"^an? (Administrator|Super Administrator) lists the role assignments$")]
	public async Task WhenAnAdministratorLists(string who)
	{
		_ = who;
		_world.LastResponse = await (await _world.Administrator()).Client.GetAsync(new Uri(RoleAssignments, UriKind.Relative));
	}

	[When(@"the Super Administrator lists the role assignments")]
	public async Task WhenTheSuperAdministratorLists()
	{
		_world.LastResponse = await (await _world.Administrator()).Client.GetAsync(new Uri(RoleAssignments, UriKind.Relative));
	}

	[When(@"^the (Safety Officer|User) asks to (list the role assignments|assign a role to an email|remove a role assignment)$")]
	public async Task WhenAMemberAsksTo(string role,
										string action)
	{
		_ = role;

		// The row a removal would target exists before the request, so a refusal is judged on
		// authorization and not on a missing id.
		_world.TargetEmail = OfficerEmail;
		_world.TargetId = await _world.Seed(OfficerEmail, MemberRole.SafetyOfficer);
		_world.Before = await _world.Fingerprint();

		var client = _world.Actor!.Client;

		_world.LastResponse = action switch
		{
			"list the role assignments" => await client.GetAsync(new Uri(RoleAssignments, UriKind.Relative)),
			"assign a role to an email" => await client.PostAsJsonAsync(
				new Uri(RoleAssignments, UriKind.Relative),
				new { email = "someone.else@example.com", role = MemberRoles.CodeFor(MemberRole.SafetyOfficer) }),
			_ => await client.PostAsync(new Uri($"{RoleAssignments}/{_world.TargetId}/revoke", UriKind.Relative), content: null),
		};
	}

	[When(@"^the member next asks for (the report list|the role assignments list) with the token they already hold$")]
	public async Task WhenTheMemberNextAsksFor(string page)
	{
		_world.LastResponse = await _world.Actor!.Client.GetAsync(new Uri(PagePath(page), UriKind.Relative));
	}

	[When(@"the Administrator removes that role assignment")]
	public async Task WhenTheAdministratorRemovesIt()
	{
		var administrator = await _world.Administrator();
		_world.LastResponse = await administrator.Client.PostAsync(
			new Uri($"{RoleAssignments}/{_world.TargetId}/revoke", UriKind.Relative), content: null);
	}

	// --- Whens: signing in ---

	[When(@"that member signs in with credentials the members site accepts")]
	public async Task WhenThatMemberSignsIn()
	{
		_world.Actor = await _world.SignIn(_world.TargetEmail!);
	}

	[When(@"^(?:that|a) member signs in as ""(.*)"" with credentials the members site accepts$")]
	public async Task WhenThatMemberSignsInAs(string typed)
	{
		ArgumentNullException.ThrowIfNull(typed);

		// An Examples cell that keeps its own quotation marks, to show the spaces inside them.
		_world.Actor = await _world.SignIn(typed.Trim('"'));
	}

	[When(@"the Super Administrator signs in with credentials the members site accepts")]
	public async Task WhenTheSuperAdministratorSignsIn()
	{
		_world.Actor = await _world.SignIn(RoleAssignmentWorld.SuperAdministratorEmail);
	}

	// --- Whens: storage ---

	[Given(@"^a role assignment that is (active|revoked)$")]
	public async Task GivenAStoredAssignment(string state)
	{
		_world.TargetEmail = "stored.member@example.com";
		_world.TargetRevoked = state == "revoked";
		_world.TargetId = await _world.Seed(_world.TargetEmail, MemberRole.SafetyOfficer, revoked: _world.TargetRevoked);
		_world.Before = await _world.Fingerprint();
	}

	[When(@"the stored role assignment is erased")]
	public async Task WhenTheStoredAssignmentIsErased()
	{
		await _world.Run("DELETE FROM role_assignments WHERE id = @id", ("id", _world.TargetId!));
	}

	[When(@"^its (email address|role|granted time|granting token subject|revocation time|revoking token subject) is changed in storage$")]
	public async Task WhenAPartIsChanged(string part)
	{
		var (column, assignment, value) = part switch
		{
			"email address" => ("email", "email = @value", (object)"changed.member@example.com"),
			"role" => ("role", "role = @value", MemberRoles.CodeFor(MemberRole.Administrator)),
			"granted time" => ("granted_at", "granted_at = @value", DateTimeOffset.UtcNow.AddDays(-1)),
			"granting token subject" => ("granted_by_subject", "granted_by_subject = @value", "someone-else"),
			"revocation time" => ("revoked_at", "revoked_at = @value", DateTimeOffset.UtcNow.AddDays(1)),
			_ => ("revoked_by_subject", "revoked_by_subject = @value", "someone-else"),
		};

		_changedColumn = column;
		await _world.Run($"UPDATE role_assignments SET {assignment} WHERE id = @id", ("id", _world.TargetId!), ("value", value));
	}

	private string _changedColumn = string.Empty;

	// --- Thens: the API ---

	[Then(@"the role assignment is created")]
	public void ThenTheAssignmentIsCreated()
	{
		_world.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Then(@"^the role assignments list ""([^""]+)"" as (an Administrator|a Safety Officer)$")]
	public async Task ThenTheListHas(string email,
									 string role)
	{
		var listed = await Listed();
		var expected = MemberRoles.CodeFor(role == "an Administrator" ? MemberRole.Administrator : MemberRole.SafetyOfficer);

		listed.ShouldContain(row => row.Email == email && row.Role == expected);
	}

	[Then(@"^the role assignments no longer list ""([^""]+)""$")]
	public async Task ThenTheListLacks(string email)
	{
		(await Listed()).ShouldNotContain(row => row.Email == email);
	}

	[Then(@"the role assignments still list the Administrator")]
	public async Task ThenTheListStillHasTheAdministrator()
	{
		(await Listed()).ShouldContain(row => row.Email == _world.TargetEmail);
	}

	[Then(@"the removal is allowed")]
	public void ThenTheRemovalIsAllowed()
	{
		_world.LastResponse!.IsSuccessStatusCode.ShouldBeTrue($"The API answered {_world.LastResponse.StatusCode}.");
	}

	[Then(@"the removed role assignment keeps when it was revoked and the token subject of the Administrator who revoked it")]
	public async Task ThenTheRemovedAssignmentKeepsItsRevocation()
	{
		var revokedAt = await _world.Scalar("SELECT revoked_at FROM role_assignments WHERE id = @id", ("id", _world.TargetId!));
		var revokedBy = await _world.Scalar("SELECT revoked_by_subject FROM role_assignments WHERE id = @id", ("id", _world.TargetId!));

		revokedAt.ShouldNotBeNull();
		revokedBy.ShouldBe((await _world.Administrator()).Subject);
	}

	[Then(@"^the (assignment|removal) is refused as invalid$")]
	public void ThenTheRequestIsInvalid(string what)
	{
		_ = what;
		_world.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Then(@"^""([^""]+)"" still has exactly one active role assignment$")]
	public async Task ThenExactlyOneActive(string email)
	{
		var count = await _world.Scalar(
			"SELECT count(*) FROM role_assignments WHERE email = @email AND revoked_at IS NULL", ("email", email));

		count.ShouldBe(1L);
	}

	[Then(@"the Super Administrator is listed as a permanent Administrator")]
	public async Task ThenTheSuperAdministratorIsListed()
	{
		_world.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var body = JsonDocument.Parse(await _world.LastResponse.Content.ReadAsStringAsync());
		body.RootElement.GetProperty("superAdministrator").GetProperty("email").GetString()
			.ShouldBe(RoleAssignmentWorld.SuperAdministratorEmail);
	}

	[Then(@"the Super Administrator offers no removal")]
	public async Task ThenTheSuperAdministratorOffersNoRemoval()
	{
		// Not an assignment: the list carries it apart from the removable rows, and no
		// removal can name it, so the one way to remove it is to remove an id it lacks.
		using var body = JsonDocument.Parse(await _world.LastResponse!.Content.ReadAsStringAsync());
		var assignments = body.RootElement.GetProperty("assignments");

		assignments.EnumerateArray()
			.Select(row => row.GetProperty("email").GetString())
			.ShouldNotContain(RoleAssignmentWorld.SuperAdministratorEmail);

		var administrator = await _world.Administrator();
		using var removal = await administrator.Client.PostAsync(
			new Uri($"{RoleAssignments}/{TinyId.New()}/revoke", UriKind.Relative), content: null);
		removal.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Then(@"the role assignments are unchanged")]
	public async Task ThenTheAssignmentsAreUnchanged()
	{
		(await _world.Fingerprint()).ShouldBe(_world.Before);
	}

	[Then(@"^the member may open (the report list|the role assignments list)$")]
	public async Task ThenTheMemberMayOpen(string page)
	{
		using var response = await _world.Actor!.Client.GetAsync(new Uri(PagePath(page), UriKind.Relative));
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Then(@"^the role assignments do not list ""([^""]+)""$")]
	public async Task ThenTheListDoesNotHold(string email)
	{
		(await Listed()).ShouldNotContain(row => row.Email == email);
	}

	[Then(@"^an audit entry records the acting token subject, a (role-assigned|role-assignment-removed) action, that role assignment as the target, and the time$")]
	public async Task ThenAnAuditEntryRecordsTheChange(string action)
	{
		var id = _world.TargetId ?? await _world.ActiveIdOf(_world.TargetEmail!);
		var expected = action == "role-assigned" ? "assigned_role" : "revoked_role";
		var actor = (await _world.Administrator()).Subject;

		var found = await _world.Scalar(
			"""
			SELECT count(*) FROM audit_log
			WHERE actor_subject = @actor AND action = @action AND target_type = 'RoleAssignment'
			  AND target_id = @id AND occurred_at > now() - interval '5 minutes' AND occurred_at <= now() + interval '1 minute'
			""",
			("actor", actor), ("action", expected), ("id", id));

		found.ShouldBe(1L);
	}

	[Then(@"^no audit entry holds ""([^""]+)""$")]
	public async Task ThenNoAuditEntryHolds(string email)
	{
		var rows = await _world.Scalar(
			"SELECT count(*) FROM audit_log t WHERE t::text ILIKE '%' || @email || '%'", ("email", email));

		rows.ShouldBe(0L);
	}

	// --- Thens: signing in ---

	[Then(@"^the member has the (User|Safety Officer|Administrator) role$")]
	public void ThenTheMemberHasTheRole(string role)
	{
		_world.Actor!.Role.ShouldBe(MemberRoles.CodeFor(GlossaryNames.Role(role)));
	}

	// --- Thens: storage ---

	[Then(@"the change is refused")]
	public void ThenTheChangeIsRefused()
	{
		_world.Refusal.ShouldNotBeNull("The statement was not refused.");
		_world.Refusal.SqlState.ShouldBe("23000");

		// An active row's trigger names the column; a revoked row is refused whole (CON-DP-017).
		// Neither names a value: the email is personal data and must not reach a log.
		if (_world.TargetRevoked)
		{
			_world.Refusal.MessageText.ShouldBe("role_assignments rows cannot be changed once revoked");
		}
		else
		{
			_world.Refusal.MessageText.ShouldStartWith($"role_assignments.{_changedColumn} ");
		}

		_world.Refusal.MessageText.ShouldNotContain("@");
	}

	[Then(@"the role assignment is as it was")]
	public async Task ThenTheAssignmentIsAsItWas()
	{
		(await _world.Fingerprint()).ShouldBe(_world.Before);
	}

	private static string PagePath(string page)
	{
		return page == "the report list" ? "/api/admin/reports" : RoleAssignments;
	}

	private async Task Assign(string role,
							  string email)
	{
		_world.TargetEmail = email;
		var administrator = await _world.Administrator();
		_world.LastResponse = await administrator.Client.PostAsJsonAsync(
			new Uri(RoleAssignments, UriKind.Relative),
			new { email, role = MemberRoles.CodeFor(GlossaryNames.Role(role)) });
	}

	private async Task<List<Listing>> Listed()
	{
		using var response = await (await ListerClient()).GetAsync(new Uri(RoleAssignments, UriKind.Relative));
		response.EnsureSuccessStatusCode();

		var list = await response.Content.ReadFromJsonAsync<ListPayload>();
		return list!.Assignments ?? [];
	}

	// The Administrator who acted, or the Super Administrator when the actor was never an Administrator.
	private async Task<HttpClient> ListerClient()
	{
		return _world.Actor is { Role: "administrator" } actor
			? actor.Client
			: (await _world.SignIn(RoleAssignmentWorld.SuperAdministratorEmail)).Client;
	}

	private sealed record Listing(string Email, string Role);

	private sealed record ListPayload(List<Listing>? Assignments);
}
