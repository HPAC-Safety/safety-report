using System.Net;
using HpacSafety.Api.Authentication;
using HpacSafety.Core.Features.Moderation;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
///     Verifying a development login against a stubbed members site — never
///     the real network, and no real credential is used. See ADR-0079.
/// </summary>
public sealed class MembersSiteCredentialSourceTests
{
	private const string LoginPageBody = """
										 <html><body>
										 <form action="/login" method="post">
										 <input type="hidden" name="authenticity_token" value="csrf-token-abc123" />
										 <input type="email" name="session[email]" />
										 <input type="password" name="session[password]" />
										 </form>
										 </body></html>
										 """;

	[Fact]
	public async Task GivenAnEmailOnTheAdministratorList_WhenTheMembersSiteAcceptsTheLogin_ThenAdministratorRole()
	{
		// Given
		var (source, _) = Source(LoginPage(), Redirect(), administratorEmails: ["chase.florell@gmail.com"]);

		// When
		var role = await source.Verify("chase.florell@gmail.com", "correct-password", CancellationToken.None);

		// Then
		role.ShouldBe(MemberRole.Administrator);
	}

	[Fact]
	public async Task GivenTheSameEmailInDifferentCase_WhenOnTheAdministratorList_ThenStillAdministratorRole()
	{
		// Given
		var (source, _) = Source(LoginPage(), Redirect(), administratorEmails: ["Chase.Florell@Gmail.com"]);

		// When
		var role = await source.Verify("chase.florell@gmail.com", "correct-password", CancellationToken.None);

		// Then
		role.ShouldBe(MemberRole.Administrator);
	}

	[Fact]
	public async Task GivenAnEmailOnTheSafetyOfficerList_WhenTheMembersSiteAcceptsTheLogin_ThenSafetyOfficerRole()
	{
		// Given
		var (source, _) = Source(LoginPage(), Redirect(), safetyOfficerEmails: ["officer@example.test"]);

		// When
		var role = await source.Verify("officer@example.test", "correct-password", CancellationToken.None);

		// Then
		role.ShouldBe(MemberRole.SafetyOfficer);
	}

	[Fact]
	public async Task GivenAnEmailOnNeitherList_WhenTheMembersSiteAcceptsTheLogin_ThenUserRole()
	{
		// Given
		var (source, _) = Source(LoginPage(), Redirect());

		// When
		var role = await source.Verify("nobody-special@example.test", "correct-password", CancellationToken.None);

		// Then
		role.ShouldBe(MemberRole.User);
	}

	[Fact]
	public async Task GivenCredentialsTheMembersSiteRejects_WhenVerified_ThenNull()
	{
		// Given — a re-rendered login page, the Rails shape for bad credentials
		var (source, _) = Source(LoginPage(), LoginPage());

		// When
		var role = await source.Verify("nobody-special@example.test", "wrong-password", CancellationToken.None);

		// Then
		role.ShouldBeNull();
	}

	[Fact]
	public async Task GivenThePostCarriesTheScrapedTokenAndCookie_WhenVerified_ThenTheMembersSiteSeesThem()
	{
		// Given
		var (source, transport) = Source(
			LoginPage(setCookie: "_hpac-rails-session=abc123; path=/; HttpOnly"), Redirect());

		// When
		await source.Verify("member@example.test", "correct-password", CancellationToken.None);

		// Then
		transport.Bodies[1].ShouldContain("authenticity_token=csrf-token-abc123");
		transport.Bodies[1].ShouldContain("session%5Bemail%5D=member%40example.test");
		transport.Requests[1].Headers.GetValues("Cookie").Single().ShouldBe("_hpac-rails-session=abc123");
	}

	[Fact]
	public async Task GivenTheLoginPageCannotBeFetched_WhenVerified_ThenMembersSiteUnavailable()
	{
		// Given
		var (source, _) = Source(new HttpResponseMessage(HttpStatusCode.InternalServerError), Redirect());

		// When / Then
		await Should.ThrowAsync<MembersSiteUnavailableException>(() =>
			source.Verify("member@example.test", "correct-password", CancellationToken.None));
	}

	[Fact]
	public async Task GivenTheLoginPageHasNoCsrfToken_WhenVerified_ThenMembersSiteUnavailable()
	{
		// Given — an unexpected page shape, not a credential problem
		var (source, _) = Source(
			new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html></html>") }, Redirect());

		// When / Then
		var cause = await Should.ThrowAsync<MembersSiteUnavailableException>(() =>
			source.Verify("member@example.test", "correct-password", CancellationToken.None));
		cause.Message.ShouldContain("CSRF");
	}

	[Fact]
	public async Task GivenTheMembersSiteCannotBeReached_WhenVerified_ThenMembersSiteUnavailable()
	{
		// Given
		var (source, _) = Source(new HttpRequestException("no route to host"), Redirect());

		// When / Then
		var cause = await Should.ThrowAsync<MembersSiteUnavailableException>(() =>
			source.Verify("member@example.test", "correct-password", CancellationToken.None));
		cause.Message.ShouldNotContain("correct-password");
	}

	[Fact]
	public async Task GivenThePostCannotReachTheMembersSite_WhenVerified_ThenMembersSiteUnavailable()
	{
		// Given — the GET succeeds, but the site drops off before the POST
		var (source, _) = Source(LoginPage(), new HttpRequestException("no route to host"));

		// When / Then
		var cause = await Should.ThrowAsync<MembersSiteUnavailableException>(() =>
			source.Verify("member@example.test", "correct-password", CancellationToken.None));
		cause.Message.ShouldNotContain("correct-password");
	}

	[Fact]
	public async Task GivenTheLoginPageTimesOut_WhenVerified_ThenMembersSiteUnavailable()
	{
		// Given
		var (source, _) = Source(new TaskCanceledException("the request timed out"), Redirect());

		// When / Then
		await Should.ThrowAsync<MembersSiteUnavailableException>(() =>
			source.Verify("member@example.test", "correct-password", CancellationToken.None));
	}

	[Fact]
	public async Task GivenThePostTimesOut_WhenVerified_ThenMembersSiteUnavailable()
	{
		// Given
		var (source, _) = Source(LoginPage(), new TaskCanceledException("the request timed out"));

		// When / Then
		await Should.ThrowAsync<MembersSiteUnavailableException>(() =>
			source.Verify("member@example.test", "correct-password", CancellationToken.None));
	}

	[Fact]
	public async Task GivenThePostAnswersWithAnUnexpectedStatus_WhenVerified_ThenMembersSiteUnavailable()
	{
		// Given — neither a redirect (success) nor a 200 (bad credentials)
		var (source, _) = Source(LoginPage(), new HttpResponseMessage(HttpStatusCode.InternalServerError));

		// When / Then
		await Should.ThrowAsync<MembersSiteUnavailableException>(() =>
			source.Verify("member@example.test", "correct-password", CancellationToken.None));
	}

	private static HttpResponseMessage LoginPage(string? setCookie = null)
	{
		var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(LoginPageBody) };

		if (setCookie is not null)
		{
			response.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
		}

		return response;
	}

	private static HttpResponseMessage Redirect()
	{
		var response = new HttpResponseMessage(HttpStatusCode.Found);
		response.Headers.Location = new Uri("/dashboard", UriKind.Relative);
		return response;
	}

	private static (MembersSiteCredentialSource Source, StubTransport Transport) Source(
		object getResult,
		object postResult,
		IReadOnlyList<string>? administratorEmails = null,
		IReadOnlyList<string>? safetyOfficerEmails = null)
	{
		var transport = new StubTransport(getResult, postResult);

		var options = Options.Create(new MembersSiteLoginOptions
		{
			AdministratorEmails = administratorEmails ?? [],
			SafetyOfficerEmails = safetyOfficerEmails ?? [],
		});

		return (new MembersSiteCredentialSource(new StubClientFactory(transport), options), transport);
	}

	/// <summary>Replays one canned response (or throws one exception) per call, in order.</summary>
	private sealed class StubTransport : HttpMessageHandler
	{
		private readonly Queue<object> _results;

		public StubTransport(params object[] results)
		{
			_results = new Queue<object>(results);
		}

		public List<HttpRequestMessage> Requests { get; } = [];
		public List<string> Bodies { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			Requests.Add(request);
			Bodies.Add(request.Content is null
				? string.Empty
				: await request.Content.ReadAsStringAsync(cancellationToken));

			var result = _results.Dequeue();

			return result switch
			{
				Exception failure => throw failure,
				HttpResponseMessage response => response,
				_ => throw new InvalidOperationException("Unexpected stubbed result."),
			};
		}
	}

	private sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient(handler, false) { BaseAddress = new Uri("https://members.hpac.ca") };
		}
	}
}
