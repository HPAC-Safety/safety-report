using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     What the role-assignment scenarios share (REQ-AUTH-012 to REQ-AUTH-029,
///     ADR-0199, ADR-0200): one deployment on a database of its own, the members
///     who sign in to it, and the last outcome a shared Then step judges.
/// </summary>
/// <remarks>
///     <para>
///         A scenario's own database, because the emails in the scenarios are fixed
///         ("officer@example.com") and an email holds one active assignment at a
///         time: on the shared database a second scenario would meet the first one's
///         row. The host is built on first use, so every Given that configures it
///         comes first; the sign-in goes through the members-site transport, stubbed
///         — never the real network — which accepts any email address as a member.
///     </para>
///     <para>
///         Injected by Reqnroll, so <c>AdminSearchSteps</c> and
///         <c>ReporterImmutabilitySteps</c>, whose Then sentences these scenarios
///         reuse word for word, judge the outcome recorded here when it is the
///         scenario's.
///     </para>
/// </remarks>
public sealed class RoleAssignmentWorld
{
	/// <summary>The Super Administrator every deployment here is configured with.</summary>
	public const string SuperAdministratorEmail = "super.admin@example.com";

	private ScratchHost? _scratch;
	private WebApplicationFactory<Program>? _host;

	/// <summary>The last response a scenario step received.</summary>
	public HttpResponseMessage? LastResponse { get; set; }

	/// <summary>The last refusal the database raised.</summary>
	public PostgresException? Refusal { get; set; }

	/// <summary>The member whose token the scenario's When acts with.</summary>
	public SignedInMember? Actor { get; set; }

	/// <summary>The role assignments' fingerprint before the When.</summary>
	public string? Before { get; set; }

	/// <summary>The email address of the row the scenario is about.</summary>
	public string? TargetEmail { get; set; }

	/// <summary>The id of the row the scenario is about.</summary>
	public string? TargetId { get; set; }

	/// <summary>Whether the deployment configures the Super Administrator.</summary>
	public bool TargetRevoked { get; set; }

	/// <summary>Whether the deployment configures the Super Administrator.</summary>
	public bool SuperAdministratorConfigured { get; set; } = true;

	/// <summary>The booted deployment: outside Development, the interim issuer on, on this scenario's own database.</summary>
	public async Task<WebApplicationFactory<Program>> Host()
	{
		if (_host is not null)
		{
			return _host;
		}

		_scratch = await BootedApi.Scratch();
		_host = await BootedApi.ProductionShapedWithInterimIssuer(
			new MembersSiteAcceptsAnyEmail(),
			SuperAdministratorConfigured ? SuperAdministratorEmail : null,
			_scratch.Host);

		// Starts the host, which applies the migrations to the scratch database (ADR-0055).
		_ = _host.Services;

		return _host;
	}

	/// <summary>A client with the origin header every non-Development request carries (ADR-0159).</summary>
	public async Task<HttpClient> AnonymousClient()
	{
		var client = (await Host()).CreateClient();
		client.DefaultRequestHeaders.Add(BootedApi.ProductionOriginSecretHeader, BootedApi.ProductionOriginSecret);
		return client;
	}

	/// <summary>Signs a member in through the members site and returns what they hold.</summary>
	public async Task<SignedInMember> SignIn(string typedEmail)
	{
		using var anonymous = await AnonymousClient();
		using var response = await anonymous.PostAsJsonAsync(
			"/api/auth/token", new { username = typedEmail, password = "whatever-the-stub-accepts" });

		response.EnsureSuccessStatusCode();
		var token = await response.Content.ReadFromJsonAsync<TokenPayload>();

		var client = await AnonymousClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

		return new SignedInMember(
			client,
			token.Role,
			new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken).Subject);
	}

	/// <summary>The Administrator the scenario acts as unless it names another.</summary>
	public async Task<SignedInMember> Administrator()
	{
		return Actor ??= await SignIn(SuperAdministratorEmail);
	}

	/// <summary>Stores an assignment as it would be after an Administrator granted it.</summary>
	public async Task<string> Seed(string email,
								 MemberRole role,
								 bool revoked = false)
	{
		var id = TinyId.New().Value;
		await using var connection = await Open();
		await using var command = new NpgsqlCommand(
			"""
			INSERT INTO role_assignments (id, email, role, granted_at, granted_by_subject, revoked_at, revoked_by_subject)
			VALUES (@id, @email, @role, now(), 'seed', @revokedAt, @revokedBy)
			""",
			connection);
		command.Parameters.AddWithValue("id", id);
		command.Parameters.AddWithValue("email", email);
		command.Parameters.AddWithValue("role", MemberRoles.CodeFor(role));
		command.Parameters.AddWithValue("revokedAt", revoked ? DateTimeOffset.UtcNow : DBNull.Value);
		command.Parameters.AddWithValue("revokedBy", revoked ? "seed" : DBNull.Value);
		await command.ExecuteNonQueryAsync();

		return id;
	}

	/// <summary>The id of the email's active assignment.</summary>
	public async Task<string> ActiveIdOf(string email)
	{
		return (string)(await Scalar("SELECT id FROM role_assignments WHERE email = @email AND revoked_at IS NULL", ("email", email))
			?? throw new InvalidOperationException("That email has no active role assignment."));
	}

	/// <summary>Every role assignment, active or revoked, as one comparable string.</summary>
	public async Task<string> Fingerprint()
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand("SELECT t::text FROM role_assignments t ORDER BY id", connection);
		await using var reader = await command.ExecuteReaderAsync();
		var rows = new List<string>();

		while (await reader.ReadAsync())
		{
			rows.Add(reader.GetString(0));
		}

		return string.Join('\n', rows);
	}

	/// <summary>One value out of the scenario's database.</summary>
	public async Task<object?> Scalar(string sql,
									  params (string Name, object Value)[] parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);

		await using var connection = await Open();
		await using var command = new NpgsqlCommand(sql, connection);

		foreach (var (name, value) in parameters)
		{
			command.Parameters.AddWithValue(name, value);
		}

		var result = await command.ExecuteScalarAsync();
		return result is DBNull ? null : result;
	}

	/// <summary>Runs a statement the database may refuse; the refusal is kept in <see cref="Refusal" />.</summary>
	public async Task Run(string sql,
						  params (string Name, object Value)[] parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);
		Refusal = null;

		try
		{
			await using var connection = await Open();
			await using var command = new NpgsqlCommand(sql, connection);

			foreach (var (name, value) in parameters)
			{
				command.Parameters.AddWithValue(name, value);
			}

			await command.ExecuteNonQueryAsync();
		}
		catch (PostgresException refusal)
		{
			Refusal = refusal;
		}
	}

	/// <summary>Makes the role assignments unreadable, as a database outage would.</summary>
	public async Task MakeUnreadable()
	{
		await using var connection = await Open();
		await using var command = new NpgsqlCommand("DROP TABLE role_assignments CASCADE", connection);
		await command.ExecuteNonQueryAsync();
	}

	/// <summary>Drops the scenario's database.</summary>
	public async Task Release()
	{
		LastResponse?.Dispose();
		Actor?.Client.Dispose();

		if (_scratch is not null)
		{
			await _scratch.DisposeAsync();
			_scratch = null;
			_host = null;
		}
	}

	private async Task<NpgsqlConnection> Open()
	{
		var host = await Host();
		var connection = new NpgsqlConnection(host.Services.GetRequiredService<IConfiguration>()["ConnectionStrings:HpacSafety"]);
		await connection.OpenAsync();
		return connection;
	}

	/// <summary>A member who signed in: their client, the role the token carries, and its subject.</summary>
	public sealed record SignedInMember(HttpClient Client, string Role, string Subject);

	private sealed record TokenPayload(string AccessToken, string Role);

	/// <summary>The members site, accepting every email address it is shown and refusing a name that is not one.</summary>
	private sealed class MembersSiteAcceptsAnyEmail : HttpMessageHandler
	{
		private const string LoginPageBody = """
											 <html><body>
											 <form action="/login" method="post">
											 <input type="hidden" name="authenticity_token" value="csrf-token-abc123" />
											 </form>
											 </body></html>
											 """;

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
																	 CancellationToken cancellationToken)
		{
			if (request.Method != HttpMethod.Post || request.Content is null)
			{
				return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LoginPageBody) };
			}

			var form = await request.Content.ReadAsStringAsync(cancellationToken);

			// A form-encoded "@" is "%40"; a fixed account such as "admin" has none.
			return form.Contains("%40", StringComparison.Ordinal) || form.Contains('@', StringComparison.Ordinal)
				? new HttpResponseMessage(HttpStatusCode.Found)
				: new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LoginPageBody) };
		}
	}
}
