using System.Net;
using System.Net.Http.Json;
using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     The role matrix, at every admin endpoint. Authoring the question bank is an
///     Administrator capability; proving membership is not enough.
/// </summary>
/// <remarks>
///     The API is the authorization boundary — never hidden markup, and never a
///     client-side route guard (ADR-0048). These tests are what make that true.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class RoleAuthorizationTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Theory]
	[InlineData(MemberRole.User)]
	[InlineData(MemberRole.SafetyOfficer)]
	public async Task GivenRoleBelowAdministrator_WhenAdminEndpointIsRead_ThenApiForbidsIt(MemberRole role)
	{
		// Given
		using var client = await SignedInClient.As(_factory, role);

		// When
		using var questions = await client.GetAsync(new Uri("/api/admin/questions", UriKind.Relative));

		// Then — 403, not 401: they are signed in, and it is still not theirs.
		questions.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenAdministrator_WhenAdminEndpointIsRead_ThenItSucceeds()
	{
		// Given
		using var client = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		using var questions = await client.GetAsync(new Uri("/api/admin/questions", UriKind.Relative));

		// Then
		questions.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Theory]
	[InlineData(MemberRole.User)]
	[InlineData(MemberRole.SafetyOfficer)]
	public async Task GivenRoleBelowAdministrator_WhenQuestionIsCreated_ThenApiForbidsIt(MemberRole role)
	{
		// Given
		using var client = await SignedInClient.As(_factory, role);

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri("/api/admin/questions", UriKind.Relative),
			new
			{
				key = $"forbidden_{Guid.NewGuid():n}"[..24],
				type = "short_text",
				labelEn = "Forbidden",
				labelFr = "Interdit",
				isPrivate = true,
				isRequired = false,
				isActive = true,
			});

		// Then — the write is refused before anything is persisted.
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenUserRole_WhenTranslationIsRequested_ThenApiForbidsIt()
	{
		// Given — translation is a drafting aid for authoring and review
		// (ADR-0062, ADR-0106), never for a member filing a report
		using var client = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri("/api/admin/translate", UriKind.Relative),
			new { texts = new[] { "A short question." }, from = "en-CA", to = "fr-CA" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[InlineData(MemberRole.SafetyOfficer)]
	[InlineData(MemberRole.Administrator)]
	public async Task GivenReviewerRole_WhenTranslationIsRequested_ThenApiAllowsIt(MemberRole role)
	{
		// Given — a reviewer drafts one summary language from the other (ADR-0106)
		using var client = await SignedInClient.As(_factory, role);

		// When
		using var response = await client.PostAsJsonAsync(
			new Uri("/api/admin/translate", UriKind.Relative),
			new { texts = new[] { "The pilot landed." }, from = "en-CA", to = "fr-CA" });

		// Then — allowed past authorization; whether a provider is configured is
		// a separate answer (503), never a 401 or 403
		response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
	}

	[Fact]
	public async Task GivenAnyRole_WhenMemberEndpointIsCalled_ThenItSucceeds()
	{
		// Given — membership is what /me requires, not privilege
		using var user = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await user.GetAsync(new Uri("/api/auth/me", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GivenForbiddenRequest_WhenApiRefuses_ThenProblemNamesInsufficientRoleWithoutSayingWhichWouldDo()
	{
		// Given
		using var client = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await client.GetAsync(new Uri("/api/admin/questions", UriKind.Relative));
		var body = await response.Content.ReadAsStringAsync();

		// Then — a stable machine code the web application can branch on, and
		// nothing about which role would have been enough.
		body.ShouldContain("https://hpac.ca/problems/insufficient-role");
		body.ShouldNotContain("administrator");
	}
}
