using HpacSafety.Api.Authentication;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>Reading a sign-in name out of a token subject (ADR-0199, CON-IF-011).</summary>
public sealed class RoleSubjectTests
{
	[Theory]
	[InlineData("dev:admin", true)]
	[InlineData("member:pat@example.ca", false)]
	[InlineData("auth0|123", false)]
	[InlineData("", false)]
	[InlineData(null, false)]
	public void GivenASubject_WhenAskedIfDevelopment_ThenOnlyTheDevPrefixCounts(string? subject, bool expected)
	{
		// Given / When
		var result = RoleSubject.IsDevelopmentSubject(subject);

		// Then
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("dev:Admin", true, false, "admin")]
	[InlineData("dev:Admin", false, true, null)]
	[InlineData("member: Pat@Example.CA ", false, true, "pat@example.ca")]
	[InlineData("member:pat@example.ca", true, false, null)]
	[InlineData("member:", false, true, null)]
	[InlineData("member:pät@example.ca", false, true, null)]
	[InlineData("   ", true, true, null)]
	[InlineData(null, true, true, null)]
	[InlineData("auth0|123", true, true, null)]
	public void GivenASubject_WhenTheSignInNameIsRead_ThenEachPrefixCountsOnlyUnderItsIssuer(string? subject,
																							   bool development,
																							   bool interim,
																							   string? expected)
	{
		// Given / When
		var name = RoleSubject.SignInNameOf(subject, development, interim);

		// Then
		name.ShouldBe(expected);
	}
}
