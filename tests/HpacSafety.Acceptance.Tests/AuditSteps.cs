using System.Net.Http.Json;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The sign-in audit scenarios — ADR-0092, REQ-MOD-044, REQ-MOD-045. Detailed
///     coverage of every audited action lives in <c>HpacSafety.Api.Tests</c>; this
///     proves the feature file's sentences are true against the booted host.
/// </summary>
[Binding]
public sealed class AuditSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private HttpResponseMessage? _response;
	private string? _attemptedUsername;

	[Given(@"a member signs in with valid credentials")]
	public async Task GivenValidCredentials()
	{
		var host = await BootedApi.FactoryAsync();
		using var client = host.CreateClient();
		_response = await client.PostAsJsonAsync("/api/auth/token", new { username = "admin", password = "admin" });
	}

	[When(@"the sign-in succeeds")]
	public void WhenSignInSucceeds()
	{
		_response!.IsSuccessStatusCode.ShouldBeTrue();
	}

	[Then(@"an audit entry records the token subject, a sign-in-succeeded action, and the time")]
	public async Task ThenAuditRecordsSuccess()
	{
		var entry = await LatestAsync(AuditAction.SignedInSucceeded);
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		entry.OccurredAt.ShouldNotBe(default);
	}

	[Then(@"it never records the credentials")]
	public async Task ThenNoCredentialsRecorded()
	{
		var entry = await LatestAsync(AuditAction.SignedInSucceeded);
		entry.Detail.ShouldBeNull();
	}

	[Given(@"a sign-in attempt uses credentials that are not valid")]
	public async Task GivenInvalidCredentials()
	{
		var host = await BootedApi.FactoryAsync();
		using var client = host.CreateClient();
		_attemptedUsername = $"nobody_{Guid.NewGuid():n}"[..24];
		_response = await client.PostAsJsonAsync(
			"/api/auth/token", new { username = _attemptedUsername, password = "not-the-real-password" });
	}

	[When(@"the attempt is rejected")]
	public void WhenAttemptIsRejected()
	{
		_response!.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Then(@"an audit entry records a sign-in-failed action and the time")]
	public async Task ThenAuditRecordsFailure()
	{
		var entry = await LatestAsync(AuditAction.SignedInFailed);
		entry.OccurredAt.ShouldNotBe(default);
	}

	[Then(@"it never records the attempted credentials")]
	public async Task ThenNoAttemptedCredentialsRecorded()
	{
		var entry = await LatestAsync(AuditAction.SignedInFailed);
		entry.Detail.ShouldBeNull();
	}

	[Then(@"the actor is recorded as the attempted identity rather than left blank")]
	public async Task ThenActorIsAttemptedIdentity()
	{
		var entry = await LatestAsync(AuditAction.SignedInFailed);
		entry.ActorSubject.ShouldBe(_attemptedUsername);
	}

	private static async Task<AuditLogEntry> LatestAsync(AuditAction action)
	{
		var host = await BootedApi.FactoryAsync();
		using var scope = host.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		return await database.AuditLog
			.Where(entry => entry.Action == action)
			.OrderByDescending(entry => entry.OccurredAt)
			.FirstAsync();
	}
}
