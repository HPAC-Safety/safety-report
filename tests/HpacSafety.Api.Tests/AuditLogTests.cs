using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Content-free audit rows for sign-in and question-bank actions — ADR-0092,
///     REQ-MOD-029, REQ-MOD-044, REQ-MOD-045, REQ-MOD-047.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class AuditLogTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Questions = new("/api/admin/questions", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenValidCredentials_WhenSignedIn_ThenAnAuditRowRecordsTheSubjectAndSucceededAction()
	{
		// Given
		var (username, password) = SignedInClient.CredentialsFor(MemberRole.Administrator);
		using var client = _factory.CreateClient();

		// When
		using var response = await client.PostAsJsonAsync("/api/auth/token", new { username, password });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entry = await database.AuditLog
			.Where(e => e.Action == AuditAction.SignedInSucceeded)
			.OrderByDescending(e => e.OccurredAt)
			.FirstAsync();

		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		entry.TargetType.ShouldBe("Authentication");
	}

	[Fact]
	public async Task GivenBadCredentials_WhenSignInIsRejected_ThenAnAuditRowRecordsFailureWithoutThePassword()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.PostAsJsonAsync(
			"/api/auth/token", new { username = "administrator", password = "not-the-real-password" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entry = await database.AuditLog
			.Where(e => e.Action == AuditAction.SignedInFailed)
			.OrderByDescending(e => e.OccurredAt)
			.FirstAsync();

		entry.ActorSubject.ShouldBe("administrator");
		entry.Detail.ShouldBeNull();
	}

	[Fact]
	public async Task GivenAQuestionIsCreated_ThenAnAuditRowRecordsTheActorAndTheQuestion()
	{
		// Given
		using var client = await SignedInClient.AsAsync(_factory, MemberRole.Administrator);
		var key = $"audit_{Guid.NewGuid():N}"[..30];

		// When
		using var created = await client.PostAsJsonAsync(
			Questions,
			new
			{
				key,
				type = "short_text",
				labelEn = "Synthetic",
				labelFr = "Synthétique",
				isRequired = false,
				isPrivate = true,
				isActive = true,
				allowsReporterAdditions = false,
				options = Array.Empty<object>(),
			});

		// Then
		created.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await created.Content.ReadFromJsonAsync<JsonElement>();
		var questionId = TinyId.Parse(body.GetProperty("id").GetString()!);

		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entry = await database.AuditLog
			.Where(e => e.Action == AuditAction.CreatedQuestion && e.TargetId == questionId)
			.SingleAsync();

		entry.TargetType.ShouldBe("Question");
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
	}
}
