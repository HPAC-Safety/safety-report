using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Api.Tests.Authentication;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.RoleAssignments;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The manage-roles endpoints (ADR-0199, REQ-AUTH-012 to REQ-AUTH-020,
///     REQ-AUTH-026, CON-IF-011). Real tokens, a real database, no stubbed policy.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class RoleAssignmentEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Collection = new("/api/admin/role-assignments", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Theory]
	[InlineData("administrator")]
	[InlineData("safety_officer")]
	public async Task GivenAnAdministrator_WhenARoleIsAssigned_ThenItIsCreatedAndListed(string role)
	{
		// Given
		var email = UniqueEmail();
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		using var created = await admin.PostAsJsonAsync(Collection, new { email = email.ToUpperInvariant(), role }, TestContext.Current.CancellationToken);
		var list = await ListAsync(admin);

		// Then
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var listed = list.Assignments!.Single(row => row.Email == email);
		listed.Role.ShouldBe(role);
	}

	[Fact]
	public async Task GivenAnAssignment_WhenItIsRemoved_ThenItIsRevokedWithWhoAndWhenAndNeverDeleted()
	{
		// Given
		var email = UniqueEmail();
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);
		var id = await AssignAsync(admin, email, "safety_officer");

		// When
		using var removed = await admin.PostAsync(new Uri($"/api/admin/role-assignments/{id}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken);
		var list = await ListAsync(admin);

		// Then
		removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		list.Assignments!.ShouldNotContain(row => row.Email == email);

		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var row = await database.RoleAssignments.AsNoTracking().SingleAsync(assignment => assignment.Email == email, TestContext.Current.CancellationToken);
		row.RevokedAt.ShouldNotBeNull();
		row.RevokedBySubject.ShouldBe("dev:admin");
	}

	[Theory]
	[InlineData("safety_officer")]
	[InlineData("administrator")]
	public async Task GivenAnActiveAssignment_WhenTheSameEmailIsAssignedAgain_ThenItIsRefusedAndOnlyOneStays(string role)
	{
		// Given
		var email = UniqueEmail();
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);
		await AssignAsync(admin, email, "safety_officer");

		// When — also in another case
		using var again = await admin.PostAsJsonAsync(Collection, new { email = $" {email.ToUpperInvariant()} ", role }, TestContext.Current.CancellationToken);
		var list = await ListAsync(admin);

		// Then
		again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await CodeOf(again)).ShouldBe("duplicate");
		list.Assignments!.Count(row => row.Email == email).ShouldBe(1);
	}

	[Fact]
	public async Task GivenARevokedAssignment_WhenTheEmailIsAssignedAgain_ThenANewAssignmentIsCreated()
	{
		// Given
		var email = UniqueEmail();
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);
		var id = await AssignAsync(admin, email, "safety_officer");
		using var removed = await admin.PostAsync(new Uri($"/api/admin/role-assignments/{id}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken);

		// When
		using var again = await admin.PostAsJsonAsync(Collection, new { email, role = "administrator" }, TestContext.Current.CancellationToken);

		// Then
		again.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	[Theory]
	[InlineData("not an email", "administrator")]
	[InlineData("", "administrator")]
	[InlineData("someone@example.com", "user")]
	[InlineData("someone@example.com", "owner")]
	[InlineData("someone@example.com", null)]
	public async Task GivenAnInvalidRequest_WhenARoleIsAssigned_ThenItIsRefusedAsInvalid(string email,
																						   string? role)
	{
		// Given
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		using var response = await admin.PostAsJsonAsync(Collection, new { email, role }, TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await CodeOf(response)).ShouldBe(role is "user" or "owner" or null ? "invalid-role" : "invalid-email");
	}

	[Fact]
	public async Task GivenTheSuperAdministratorsEmail_WhenItIsAssigned_ThenItIsRefused()
	{
		// Given
		var superAdmin = UniqueEmail();
		await using var host = WithSuperAdmin(superAdmin);
		using var admin = await SignedInClient.As(host, MemberRole.Administrator);

		// When
		using var response = await admin.PostAsJsonAsync(Collection, new { email = superAdmin, role = "safety_officer" }, TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await CodeOf(response)).ShouldBe("super-administrator");
	}

	[Fact]
	public async Task GivenAnAdministratorByAssignment_WhenTheyRemoveTheirOwnAssignment_ThenItIsRefusedAndTheyStayListed()
	{
		// Given — an Administrator whose role comes from their own assignment.
		var email = UniqueEmail();
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);
		var id = await AssignAsync(admin, email, "administrator");
		using var self = SignedInClient.Bearing(_factory, DevelopmentTokenFor($"dev:{email}"));

		// When
		using var response = await self.PostAsync(new Uri($"/api/admin/role-assignments/{id}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken);
		var list = await ListAsync(admin);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await CodeOf(response)).ShouldBe("self");
		list.Assignments!.ShouldContain(row => row.Email == email);
	}

	[Fact]
	public async Task GivenAnUnknownOrMalformedId_WhenRemoved_ThenNotFound()
	{
		// Given
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		using var unknown = await admin.PostAsync(new Uri("/api/admin/role-assignments/AAAAAAAAAAA/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken);
		using var malformed = await admin.PostAsync(new Uri("/api/admin/role-assignments/nope/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken);

		// Then
		unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await CodeOf(unknown)).ShouldBe("not-found");
		malformed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Theory]
	[InlineData(MemberRole.User)]
	[InlineData(MemberRole.SafetyOfficer)]
	public async Task GivenAMemberBelowAdministrator_WhenTheyManageRoles_ThenEveryRequestIsForbiddenAndNothingChanges(MemberRole role)
	{
		// Given
		var email = UniqueEmail();
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);
		var id = await AssignAsync(admin, email, "safety_officer");
		using var member = await SignedInClient.As(_factory, role);

		// When
		using var list = await member.GetAsync(Collection, TestContext.Current.CancellationToken);
		using var assign = await member.PostAsJsonAsync(Collection, new { email = UniqueEmail(), role = "administrator" }, TestContext.Current.CancellationToken);
		using var revoke = await member.PostAsync(new Uri($"/api/admin/role-assignments/{id}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken);
		var after = await ListAsync(admin);

		// Then
		list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		assign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		revoke.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		after.Assignments!.ShouldContain(row => row.Email == email);
	}

	[Fact]
	public async Task GivenAnAdministratorByAssignment_WhenRolesAreListed_ThenOnlyTheirOwnRowIsSelf()
	{
		// Given
		var mine = UniqueEmail();
		var other = UniqueEmail();
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);
		await AssignAsync(admin, mine, "administrator");
		await AssignAsync(admin, other, "safety_officer");
		using var self = SignedInClient.Bearing(_factory, DevelopmentTokenFor($"dev:{mine}"));

		// When
		var list = await ListAsync(self);

		// Then
		list.Assignments!.Single(row => row.Email == mine).IsSelf.ShouldBeTrue();
		list.Assignments!.Single(row => row.Email == other).IsSelf.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenNoToken_WhenRolesAreListed_ThenUnauthorized()
	{
		// Given
		using var anonymous = _factory.CreateClient();

		// When
		using var response = await anonymous.GetAsync(Collection, TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenASuperAdministrator_WhenRolesAreListed_ThenTheyAreListedAndNormalised()
	{
		// Given
		var superAdmin = UniqueEmail();
		await using var host = WithSuperAdmin($" {superAdmin.ToUpperInvariant()} ");
		using var admin = await SignedInClient.As(host, MemberRole.Administrator);

		// When
		var list = await ListAsync(admin);

		// Then
		list.SuperAdministrator!.Email.ShouldBe(superAdmin);
		list.AssignmentsUnavailable.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenNoSuperAdministrator_WhenRolesAreListed_ThenNoneIsListed()
	{
		// Given
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		var list = await ListAsync(admin);

		// Then
		list.SuperAdministrator.ShouldBeNull();
	}

	[Fact]
	public async Task GivenRoleAssignmentsCannotBeRead_WhenTheSuperAdministratorListsThem_ThenTheyAreListedAlone()
	{
		// Given — a Super Administrator, holding a Development token, with the store down.
		var superAdmin = UniqueEmail();
		await using var host = WithSuperAdmin(superAdmin);
		using var client = SignedInClient.Bearing(host, DevelopmentTokenFor($"dev:{superAdmin}"));

		// When
		RoleAssignmentList list;
		await using (await Unreadable.Begin(host))
		{
			list = await ListAsync(client);
		}

		// Then
		list.SuperAdministrator!.Email.ShouldBe(superAdmin);
		list.Assignments.ShouldBeNull();
		list.AssignmentsUnavailable.ShouldBeTrue();
	}

	[Theory]
	[InlineData("\u212Aevin@example.com")]
	[InlineData("kevin@exampl\u00E9.com")]
	public async Task GivenANonAsciiEmail_WhenARoleIsAssigned_ThenItIsRefusedAsInvalid(string email)
	{
		// Given — U+212A (Kelvin sign) folds to k under culture-invariant lower-casing.
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		using var response = await admin.PostAsJsonAsync(Collection, new { email, role = "administrator" }, TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await CodeOf(response)).ShouldBe("invalid-email");
	}

	[Fact]
	public async Task GivenAnAssignmentForAnAsciiEmail_WhenAKelvinSignLookAlikeSignsIn_ThenItIsOnlyAUser()
	{
		// Given
		var local = Guid.NewGuid().ToString("n");
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);
		await AssignAsync(admin, $"{local}@example.com", "administrator");
		using var lookAlike = SignedInClient.Bearing(_factory, DevelopmentTokenFor($"dev:{local}@exam\u212Aple.com"));

		// When
		using var response = await lookAlike.GetAsync(Collection, TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData("/api/admin/role-assignments")]
	[InlineData("/api/admin/questions")]
	[InlineData("/api/admin/reports")]
	public async Task GivenAForgedRolesClaimAndAnUnassignedSubject_WhenAnAdminEndpointIsRead_ThenItIsForbidden(string path)
	{
		// Given — a validly signed token that claims administrator for a subject with no assignment.
		using var forged = SignedInClient.Bearing(_factory, DevelopmentTokenFor($"dev:{UniqueEmail()}", "administrator"));

		// When
		using var response = await forged.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenManyRacingAssignmentsOfOneEmail_WhenTheyRun_ThenOneWinsTheRestAreDuplicatesAndNoEmailIsLogged()
	{
		// Given — the losers of the race reach the insert's ON CONFLICT DO NOTHING (inserted == 0).
		var email = UniqueEmail();
		var logs = new CapturedLogs();
		await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddLogging(logging => logging.AddProvider(logs))));
		using var admin = await SignedInClient.As(host, MemberRole.Administrator);

		// When
		var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
			admin.PostAsJsonAsync(Collection, new { email, role = "safety_officer" }, TestContext.Current.CancellationToken)));

		// Then
		responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
		foreach (var loser in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
		{
			loser.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			(await CodeOf(loser)).ShouldBe("duplicate");
		}

		logs.Messages.ShouldNotContain(message => message.Contains(email, StringComparison.OrdinalIgnoreCase));

		foreach (var response in responses)
		{
			response.Dispose();
		}
	}

	[Fact]
	public async Task GivenADuplicateAndAnUnreadableStore_WhenRolesAreManaged_ThenNoEmailReachesALog()
	{
		// Given
		var email = UniqueEmail();
		var logs = new CapturedLogs();
		await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
			services.AddLogging(logging => logging.AddProvider(logs))));
		using var admin = await SignedInClient.As(host, MemberRole.Administrator);

		// When
		await AssignAsync(admin, email, "safety_officer");
		using var duplicate = await admin.PostAsJsonAsync(Collection, new { email, role = "administrator" }, TestContext.Current.CancellationToken);

		await using (await Unreadable.Begin(host))
		{
			using var unreadable = await admin.GetAsync(Collection, TestContext.Current.CancellationToken);
			unreadable.StatusCode.ShouldBe(HttpStatusCode.OK);
		}

		// Then
		duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		logs.Messages.ShouldNotBeEmpty();
		logs.Messages.ShouldNotContain(message => message.Contains(email, StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task GivenAssignAndRemove_WhenAudited_ThenTheAuditHoldsTheActorAndNeverTheEmail()
	{
		// Given
		var email = UniqueEmail();
		using var admin = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		var id = await AssignAsync(admin, email, "safety_officer");
		using var removed = await admin.PostAsync(new Uri($"/api/admin/role-assignments/{id}/revoke", UriKind.Relative), null, TestContext.Current.CancellationToken);

		// Then
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var target = TinyIdOf(id);
		var entries = await database.AuditLog.AsNoTracking().Where(entry => entry.TargetId == target).ToListAsync(TestContext.Current.CancellationToken);

		entries.Select(entry => entry.Action).ShouldBe([AuditAction.AssignedRole, AuditAction.RevokedRole], ignoreOrder: true);
		entries.ShouldAllBe(entry => entry.ActorSubject == "dev:admin" && entry.Detail == null);
	}

	private static async Task<string?> CodeOf(HttpResponseMessage response)
	{
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

		return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
	}

	private WebApplicationFactory<Program> WithSuperAdmin(string email)
	{
		return _factory.WithWebHostBuilder(builder => builder.UseSetting("HpacSafety:Authentication:SuperAdmin", email));
	}

	private static string UniqueEmail()
	{
		return $"{Guid.NewGuid():n}@example.com";
	}

	private static HpacSafety.Core.TinyId TinyIdOf(string id)
	{
		return HpacSafety.Core.TinyId.Parse(id);
	}

	private static async Task<string> AssignAsync(HttpClient admin,
												  string email,
												  string role)
	{
		using var response = await admin.PostAsJsonAsync(Collection, new { email, role }, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);

		var created = await response.Content.ReadFromJsonAsync<RoleAssignmentRow>(TestContext.Current.CancellationToken);
		return created!.Id;
	}

	private static async Task<RoleAssignmentList> ListAsync(HttpClient client)
	{
		using var response = await client.GetAsync(Collection, TestContext.Current.CancellationToken);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		return (await response.Content.ReadFromJsonAsync<RoleAssignmentList>(new JsonSerializerOptions(JsonSerializerDefaults.Web), TestContext.Current.CancellationToken))!;
	}

	/// <summary>A token signed with the Development key, for a subject of the test's choosing.</summary>
	private static string DevelopmentTokenFor(string subject,
											  string? rolesClaim = null)
	{
		List<Claim> claims = [new Claim("sub", subject)];

		if (rolesClaim is not null)
		{
			claims.Add(new Claim("roles", rolesClaim));
		}

		var token = new JwtSecurityToken(
			DevelopmentTokenIssuer.IssuerName,
			"hpac-safety-api",
			claims,
			DateTimeOffset.UtcNow.AddMinutes(-1).UtcDateTime,
			DateTimeOffset.UtcNow.AddHours(1).UtcDateTime,
			new SigningCredentials(
				new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiPostgresFixture.SigningKey)), SecurityAlgorithms.HmacSha256));

		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	private sealed record RoleAssignmentRow(string Id, string Email, string Role, DateTimeOffset GrantedAt, bool IsSelf);

	private sealed record SuperAdministratorRow(string Email);

	private sealed record RoleAssignmentList(
		SuperAdministratorRow? SuperAdministrator,
		IReadOnlyList<RoleAssignmentRow>? Assignments,
		bool AssignmentsUnavailable);
}
