using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.RoleAssignments;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>A role assignment's grant and revoke rules (ADR-0199, REQ-AUTH-012 to REQ-AUTH-020).</summary>
public class RoleAssignmentTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenAnEmailAndAssignableRole_WhenGranted_ThenItIsNormalisedAndActive()
	{
		// Given / When
		var assignment = RoleAssignment.Grant("  Pat@Example.CA ", MemberRole.SafetyOfficer, "dev:admin", Now);

		// Then
		assignment.Email.ShouldBe("pat@example.ca");
		assignment.IsActive.ShouldBeTrue();
	}

	[Theory]
	[InlineData("")]
	[InlineData("a@")]
	[InlineData("@example.ca")]
	[InlineData("noatsign.example.ca")]
	[InlineData("a@b@example.ca")]
	[InlineData("pat@exa mple.ca")]
	[InlineData("pät@example.ca")]
	[InlineData("pat@")]
	public void GivenAMalformedEmail_WhenGranted_ThenItIsRefused(string email)
	{
		// Given / When
		var grant = () => RoleAssignment.Grant(email, MemberRole.Administrator, "dev:admin", Now);

		// Then
		grant.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenAnEmailLongerThanTheLimit_WhenGranted_ThenItIsRefused()
	{
		// Given
		var email = new string('a', RoleAssignment.EmailMaxLength) + "@example.ca";

		// When
		var grant = () => RoleAssignment.Grant(email, MemberRole.Administrator, "dev:admin", Now);

		// Then
		grant.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenAnUnassignableRole_WhenGranted_ThenItIsRefused()
	{
		// Given / When
		var grant = () => RoleAssignment.Grant("pat@example.ca", MemberRole.User, "dev:admin", Now);

		// Then
		grant.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenNoEmail_WhenGranted_ThenItIsRefused()
	{
		// Given / When
		var grant = () => RoleAssignment.Grant(null!, MemberRole.Administrator, "dev:admin", Now);

		// Then
		grant.ShouldThrow<DomainRuleViolationException>();
	}

	[Fact]
	public void GivenARevokedAssignment_WhenRevokedAgain_ThenItIsRefusedAndKeepsItsFirstStamp()
	{
		// Given
		var assignment = RoleAssignment.Grant("pat@example.ca", MemberRole.Administrator, "dev:admin", Now);
		assignment.Revoke("dev:first", Now);

		// When
		var again = () => assignment.Revoke("dev:second", Now.AddHours(1));

		// Then
		again.ShouldThrow<DomainRuleViolationException>();
		assignment.IsActive.ShouldBeFalse();
		assignment.RevokedBySubject.ShouldBe("dev:first");
		assignment.RevokedAt.ShouldBe(Now);
	}
}
