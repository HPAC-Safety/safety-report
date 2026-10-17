using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     Makes the role assignments unreadable for a while by renaming the
///     <c>active_role_assignments</c> view, then puts it back. Safe because the
///     API tests share one collection and run one at a time.
/// </summary>
internal sealed class Unreadable : IAsyncDisposable
{
	private readonly WebApplicationFactory<Program> _host;

	private Unreadable(WebApplicationFactory<Program> host)
	{
		_host = host;
	}

	public static async Task<Unreadable> Begin(WebApplicationFactory<Program> host)
	{
		await Rename(host, "active_role_assignments", "active_role_assignments_hidden");
		return new Unreadable(host);
	}

	public async ValueTask DisposeAsync()
	{
		await Rename(_host, "active_role_assignments_hidden", "active_role_assignments");
	}

	private static async Task Rename(WebApplicationFactory<Program> host,
									 string from,
									 string to)
	{
		await using var scope = host.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

#pragma warning disable EF1002 // Fixed identifiers, no user input.
		await database.Database.ExecuteSqlRawAsync($"ALTER VIEW {from} RENAME TO {to}");
#pragma warning restore EF1002
	}
}
