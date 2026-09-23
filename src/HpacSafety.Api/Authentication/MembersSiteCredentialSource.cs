using System.Net;
using System.Text.RegularExpressions;
using HpacSafety.Core.Features.Moderation;
using Microsoft.Extensions.Options;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     Verifies a development login against the live HPAC members site
///     (a Rails form-login application, not an identity provider), so a
///     developer can test the app as a real member without a fixed account.
///     See ADR-0079.
/// </summary>
/// <remarks>
///     <para>
///         Replicates a browser login: <c>GET /login</c> to collect the CSRF
///         token and session cookie, then <c>POST /login</c> with the
///         credentials, the token, and that cookie. A <c>302</c> off the login
///         page is success; a re-rendered <c>200</c> login page is bad
///         credentials. Anything else — a timeout, an unreachable host, a
///         missing CSRF token — is reported as <see cref="MembersSiteUnavailableException" />,
///         never as bad credentials.
///     </para>
///     <para>
///         Cookies are threaded through by hand, as local values, rather than
///         a <see cref="System.Net.CookieContainer" /> on the named
///         <see cref="HttpClient" />'s handler — that handler is pooled and
///         shared by <see cref="IHttpClientFactory" /> across concurrent
///         requests, so a container attached to it would leak one developer's
///         session cookie into another's login attempt.
///     </para>
///     <para>
///         The members site has no concept of the three roles this system
///         uses, and its own cookies are sealed with a secret we do not have.
///         Role therefore comes from <see cref="MembersSiteLoginOptions" />'s
///         email lists, never from anything the site returns.
///     </para>
/// </remarks>
public sealed partial class MembersSiteCredentialSource : IDevelopmentCredentialSource
{
	/// <summary>The named <see cref="HttpClient" /> this resolves.</summary>
	public const string HttpClientName = "hpac-members-login";

	private readonly IHttpClientFactory _clients;
	private readonly MembersSiteLoginOptions _options;

	/// <summary>Creates the credential source.</summary>
	public MembersSiteCredentialSource(IHttpClientFactory clients,
									   IOptions<MembersSiteLoginOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		_clients = clients;
		_options = options.Value;
	}

	/// <inheritdoc />
	public async Task<MemberRole?> Verify(string username,
										  string password,
										  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(username);
		ArgumentNullException.ThrowIfNull(password);

		using var client = _clients.CreateClient(HttpClientName);

		var (csrfToken, cookies) = await FetchLoginFormAsync(client, cancellationToken).ConfigureAwait(false);

		using var post = new HttpRequestMessage(HttpMethod.Post, "/login")
		{
			Content = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["session[email]"] = username,
				["session[password]"] = password,
				["authenticity_token"] = csrfToken,
			}),
		};
		post.Headers.TryAddWithoutValidation("Cookie", cookies);

		HttpResponseMessage response;

		try
		{
			response = await client.SendAsync(post, cancellationToken).ConfigureAwait(false);
		}
		catch (HttpRequestException cause)
		{
			throw new MembersSiteUnavailableException("The members site could not be reached.", cause);
		}
		catch (TaskCanceledException cause) when (!cancellationToken.IsCancellationRequested)
		{
			throw new MembersSiteUnavailableException("The members site did not respond in time.", cause);
		}

		using (response)
		{
			// A successful Rails login redirects off /login. A re-rendered
			// login page (200) means the credentials were not accepted.
			if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther)
			{
				return RoleFor(username);
			}

			if (response.StatusCode == HttpStatusCode.OK)
			{
				return null;
			}

			throw new MembersSiteUnavailableException(
				$"The members site answered {(int)response.StatusCode} to a login attempt.");
		}
	}

	private MemberRole RoleFor(string email)
	{
		if (_options.AdministratorEmails.Any(candidate => string.Equals(candidate, email, StringComparison.OrdinalIgnoreCase)))
		{
			return MemberRole.Administrator;
		}

		if (_options.SafetyOfficerEmails.Any(candidate => string.Equals(candidate, email, StringComparison.OrdinalIgnoreCase)))
		{
			return MemberRole.SafetyOfficer;
		}

		return MemberRole.User;
	}

	private static async Task<(string CsrfToken, string Cookies)> FetchLoginFormAsync(
		HttpClient client,
		CancellationToken cancellationToken)
	{
		HttpResponseMessage response;

		try
		{
			response = await client.GetAsync("/login", cancellationToken).ConfigureAwait(false);
		}
		catch (HttpRequestException cause)
		{
			throw new MembersSiteUnavailableException("The members site could not be reached.", cause);
		}
		catch (TaskCanceledException cause) when (!cancellationToken.IsCancellationRequested)
		{
			throw new MembersSiteUnavailableException("The members site did not respond in time.", cause);
		}

		using (response)
		{
			if (!response.IsSuccessStatusCode)
			{
				throw new MembersSiteUnavailableException(
					$"The members site answered {(int)response.StatusCode} to the login page request.");
			}

			var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var csrfMatch = AuthenticityTokenPattern().Match(body);

			if (!csrfMatch.Success)
			{
				throw new MembersSiteUnavailableException(
					"The members site's login page did not contain the expected CSRF token.");
			}

			var cookies = string.Join(
				"; ",
				response.Headers.TryGetValues("Set-Cookie", out var setCookies)
					? setCookies.Select(CookiePairFrom).OfType<string>()
					: []);

			return (csrfMatch.Groups[1].Value, cookies);
		}
	}

	private static string? CookiePairFrom(string setCookieHeader)
	{
		var separator = setCookieHeader.IndexOf(';');
		var pair = separator < 0 ? setCookieHeader : setCookieHeader[..separator];
		return string.IsNullOrWhiteSpace(pair) ? null : pair.Trim();
	}

	[GeneratedRegex("name=\"authenticity_token\"\\s+value=\"([^\"]+)\"")]
	private static partial Regex AuthenticityTokenPattern();
}
