namespace HpacSafety.Core.Abstractions;

/// <summary>The outcome of an upstream credential check. Carries no credential.</summary>
/// <param name="Succeeded">Whether the credentials were accepted upstream.</param>
/// <param name="MemberIdentifier">Who they are, when they were accepted.</param>
public sealed record MemberAuthenticationResult(bool Succeeded, string? MemberIdentifier);

/// <summary>
/// Checks member credentials against members.hpac.ca.
/// </summary>
/// <remarks>
/// Credentials are never persisted, logged, cached, or included in an exception
/// message — by an implementation of this interface or by anything that calls
/// one. See docs/authentication.md.
/// </remarks>
public interface IMemberAuthenticator
{
    /// <summary>Verifies credentials upstream and returns who they belong to.</summary>
    Task<MemberAuthenticationResult> AuthenticateAsync(string memberIdentifier, string password, CancellationToken cancellationToken);
}
