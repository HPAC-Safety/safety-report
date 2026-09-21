using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using HpacSafety.Api.Authentication;
using HpacSafety.Core.Features.Moderation;

using Microsoft.IdentityModel.Tokens;

using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
/// The non-<c>@ui</c> authentication scenarios in
/// <c>features/moderation-authentication-and-publication/</c>.
/// </summary>
/// <remarks>
/// These validate a token with
/// <see cref="AuthenticationServiceCollectionExtensions.ValidationParametersFor"/>
/// — the same parameters the API registers, from one definition, so a scenario
/// asserts the validation this system actually performs rather than a copy that
/// could drift. The whole HTTP path, including the policies and the problem
/// shape, is covered by <c>HpacSafety.Api.Tests</c>. Every token here is
/// synthetic.
/// </remarks>
[Binding]
public sealed class AuthenticationSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

    private const string SigningKey = "hpac-safety-acceptance-signing-key-not-a-secret";
    private const string Audience = "hpac-safety-api";

    private static readonly HpacAuthenticationOptions Options = new()
    {
        Audience = Audience,
        DevelopmentSigningKey = SigningKey,
    };

    private string? _token;
    private ClaimsPrincipal? _principal;
    private bool _refused;

    [Given(@"a bearer token signed with a key the API does not trust")]
    public void GivenTokenSignedWithAnUnknownKey() =>
        _token = Forge(key: "an-entirely-different-signing-key-nobody-here-knows");

    [Given(@"a validly issued bearer token whose signature segment has been changed")]
    public void GivenTamperedSignature()
    {
        var segments = Forge().Split('.');
        segments[2] = (segments[2][0] == 'A' ? 'B' : 'A') + segments[2][1..];
        _token = string.Join('.', segments);
    }

    [Given(@"a bearer token whose expiry has passed")]
    public void GivenExpiredToken() =>
        _token = Forge(
            notBefore: DateTimeOffset.UtcNow.AddHours(-3),
            expires: DateTimeOffset.UtcNow.AddHours(-2));

    [Given(@"a bearer token issued for a different audience")]
    public void GivenWrongAudience() => _token = Forge(audience: "somebody-elses-api");

    [Given(@"a validly signed bearer token carrying no recognized role claim")]
    public void GivenNoRecognizedRole() => _token = Forge(roles: ["wing-commander"]);

    [Given(@"a validly signed bearer token carrying a name, an email, and a picture claim")]
    public void GivenChattyToken() => _token = Forge(extra:
    [
        new Claim("name", "A Synthetic Person"),
        new Claim("email", "synthetic@example.test"),
        new Claim("picture", "https://example.test/avatar.png"),
    ]);

    [When(@"it is presented to any authenticated endpoint")]
    [When(@"it is presented to the API")]
    [When(@"the API establishes the caller's identity")]
    public void WhenItIsValidated()
    {
        var parameters = AuthenticationServiceCollectionExtensions.ValidationParametersFor(
            Options, useDevelopmentIssuer: true);

        try
        {
            _principal = new JwtSecurityTokenHandler().ValidateToken(_token, parameters, out _);
            _refused = false;
        }
        catch (SecurityTokenException)
        {
            _principal = null;
            _refused = true;
        }
    }

    [Then(@"the API refuses the request")]
    public void ThenRefused() => _refused.ShouldBeTrue();

    [Then(@"it does not disclose why the token was refused")]
    public void ThenNoReasonDisclosed()
    {
        // The handler's reason never reaches the caller: the challenge writes
        // one fixed problem body. Asserted over HTTP in
        // AuthEndpointTests/RoleAuthorizationTests; here the point is only
        // that nothing this step can see carries it.
        _principal.ShouldBeNull();
    }

    [Then(@"the request is authenticated")]
    public void ThenAuthenticated()
    {
        _refused.ShouldBeFalse();
        _principal.ShouldNotBeNull();
    }

    [Then(@"the identity has the User role and no administrative capability")]
    public void ThenUserRole() =>
        MemberRoles.EffectiveRole(_principal!, Options.RoleClaimType).ShouldBe(MemberRole.User);

    [Then(@"it reads only the subject and the role claim")]
    public void ThenOnlySubjectAndRole()
    {
        var identity = MemberRoles.IdentityOf(_principal!, Options.RoleClaimType);

        identity.ShouldNotBeNull();
        identity.Subject.ShouldBe("dev:synthetic");

        // MemberIdentity is the whole of what this system carries forward, and
        // it has exactly two properties.
        typeof(MemberIdentity).GetProperties().Length.ShouldBe(2);
    }

    [Then(@"no other claim reaches domain code, a log, or the database")]
    public void ThenNothingElseCarried()
    {
        var identity = MemberRoles.IdentityOf(_principal!, Options.RoleClaimType)!;
        var carried = $"{identity.Subject}|{identity.Role}";

        carried.ShouldNotContain("Synthetic Person");
        carried.ShouldNotContain("synthetic@example.test");
        carried.ShouldNotContain("avatar.png");
    }

    private static string Forge(
        string key = SigningKey,
        string issuer = DevelopmentTokenIssuer.IssuerName,
        string audience = Audience,
        string[]? roles = null,
        Claim[]? extra = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "dev:synthetic") };
        claims.AddRange((roles ?? ["administrator"]).Select(role => new Claim("roles", role)));
        claims.AddRange(extra ?? []);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,

            // JwtSecurityToken wants DateTime. Convert at this boundary and
            // never carry one past it (ADR-0035).
            notBefore: (notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-1)).UtcDateTime,
            expires: (expires ?? DateTimeOffset.UtcNow.AddHours(1)).UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
