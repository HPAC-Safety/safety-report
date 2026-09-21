using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

using HpacSafety.Api.Authentication;
using HpacSafety.Core.Features.Moderation;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace HpacSafety.Api.Tests.Authentication;

/// <summary>
/// The development token is genuinely <em>validated</em>, not merely minted.
/// </summary>
/// <remarks>
/// This is the suite that earns ADR-0066's claim. Every case below is refused
/// by the same middleware and the same validation parameters a production
/// deployment runs — only the issuer and the key differ.
/// </remarks>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class TokenValidationTests(ApiPostgresFixture fixture)
{
    private static readonly Uri Me = new("/api/auth/me", UriKind.Relative);

    private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

    [Fact]
    public async Task GivenDevelopmentToken_WhenAuthenticatedEndpointIsCalled_ThenSubjectAndRoleComeFromValidatedPrincipal()
    {
        // Given
        using var client = await SignedInClient.AsAsync(_factory, MemberRole.Administrator);

        // When
        using var response = await client.GetAsync(Me);
        var identity = await response.Content.ReadFromJsonAsync<MeResponse>();

        // Then — read back off the principal the handler built, not off the
        // request body the test sent.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        identity!.Subject.ShouldBe("dev:admin");
        identity.Role.ShouldBe("administrator");
    }

    [Fact]
    public async Task GivenNoToken_WhenAuthenticatedEndpointIsCalled_ThenApiRefuses()
    {
        // Given
        using var client = _factory.CreateClient();

        // When
        using var response = await client.GetAsync(Me);

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenTokenSignedWithUnknownKey_WhenPresented_ThenApiRefuses()
    {
        // Given — correctly shaped, correctly issued, wrong key
        var forged = Forge(key: "a-completely-different-signing-key-entirely");

        // When
        using var client = SignedInClient.Bearing(_factory, forged);
        using var response = await client.GetAsync(Me);

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenTokenWhoseSignatureWasAltered_WhenPresented_ThenApiRefuses()
    {
        // Given — a real token with one character of its signature changed
        var real = await SignedInClient.TokenForAsync(_factory, MemberRole.Administrator);
        var segments = real.Split('.');
        segments[2] = (segments[2][0] == 'A' ? 'B' : 'A') + segments[2][1..];
        var tampered = string.Join('.', segments);

        // When
        using var client = SignedInClient.Bearing(_factory, tampered);
        using var response = await client.GetAsync(Me);

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenExpiredToken_WhenPresented_ThenApiRefuses()
    {
        // Given — well past the thirty seconds of clock skew allowed
        var expired = Forge(
            expires: DateTimeOffset.UtcNow.AddHours(-2),
            notBefore: DateTimeOffset.UtcNow.AddHours(-3));

        // When
        using var client = SignedInClient.Bearing(_factory, expired);
        using var response = await client.GetAsync(Me);

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenTokenForAnotherAudience_WhenPresented_ThenApiRefuses()
    {
        // Given — a valid signature, for somebody else's API
        var elsewhere = Forge(audience: "some-other-api");

        // When
        using var client = SignedInClient.Bearing(_factory, elsewhere);
        using var response = await client.GetAsync(Me);

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenTokenFromAnotherIssuer_WhenPresented_ThenApiRefuses()
    {
        // Given — right key, right audience, wrong issuer
        var elsewhere = Forge(issuer: "https://not-this-system.example.test");

        // When
        using var client = SignedInClient.Bearing(_factory, elsewhere);
        using var response = await client.GetAsync(Me);

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenUnsignedAlgNoneToken_WhenPresented_ThenApiRefuses()
    {
        // Given — the classic downgrade: claims intact, signature removed
        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64Url($$"""
            {"sub":"dev:admin","aud":"hpac-safety-api","iss":"{{DevelopmentTokenIssuer.IssuerName}}","roles":"administrator","exp":{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}
            """);
        var unsigned = $"{header}.{payload}.";

        // When
        using var client = SignedInClient.Bearing(_factory, unsigned);
        using var response = await client.GetAsync(Me);

        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenTokenWithNoRecognizedRoleClaim_WhenPresented_ThenItAuthenticatesAsUser()
    {
        // Given — validly signed, but its role means nothing here
        var unknownRole = Forge(role: "wing-commander");

        // When
        using var client = SignedInClient.Bearing(_factory, unknownRole);
        using var response = await client.GetAsync(Me);
        var identity = await response.Content.ReadFromJsonAsync<MeResponse>();

        // Then — membership is proven, and no administrative capability
        // follows from it.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        identity!.Role.ShouldBe("user");
    }

    [Fact]
    public async Task GivenTokenCarryingSeveralRoleClaims_WhenPresented_ThenHighestWins()
    {
        // Given — a provider may emit the claim more than once
        var several = Forge(roles: ["user", "administrator", "safety_officer"]);

        // When
        using var client = SignedInClient.Bearing(_factory, several);
        using var response = await client.GetAsync(Me);
        var identity = await response.Content.ReadFromJsonAsync<MeResponse>();

        // Then
        identity!.Role.ShouldBe("administrator");
    }

    [Fact]
    public async Task GivenTokenCarryingNameAndEmailClaims_WhenPresented_ThenOnlySubjectAndRoleAreRead()
    {
        // Given — a provider will happily hand over a name and an address
        var chatty = Forge(extra:
        [
            new Claim("name", "A Synthetic Person"),
            new Claim("email", "synthetic@example.test"),
            new Claim("picture", "https://example.test/avatar.png"),
        ]);

        // When
        using var client = SignedInClient.Bearing(_factory, chatty);
        using var response = await client.GetAsync(Me);
        var body = await response.Content.ReadAsStringAsync();

        // Then — this system has no use for any of it, and the narrowest read
        // is the one that cannot leak.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldNotContain("Synthetic Person");
        body.ShouldNotContain("synthetic@example.test");
        body.ShouldNotContain("avatar.png");
    }

    private static string Base64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Builds a token this API would otherwise accept, varying exactly one
    /// thing so each test names the single reason it is refused.
    /// </summary>
    private static string Forge(
        string key = ApiPostgresFixture.SigningKey,
        string issuer = DevelopmentTokenIssuer.IssuerName,
        string audience = "hpac-safety-api",
        string? role = "administrator",
        string[]? roles = null,
        Claim[]? extra = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "dev:admin") };

        foreach (var value in roles ?? (role is null ? [] : new[] { role }))
        {
            claims.Add(new Claim("roles", value));
        }

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
