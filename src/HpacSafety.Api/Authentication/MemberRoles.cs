using System.Security.Claims;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;

namespace HpacSafety.Api.Authentication;

/// <summary>
///     Reads a <see cref="MemberRole" /> out of a validated principal's claims.
/// </summary>
/// <remarks>
///     The claim's <em>name</em> is configuration, because providers disagree about
///     where roles live. Its <em>values</em> are this repository's invariant codes,
///     produced by <see cref="EnumCode" /> exactly as they are everywhere else —
///     <c>user</c>, <c>safety_officer</c>, <c>administrator</c>. See ADR-0064.
/// </remarks>
public static class MemberRoles
{
    /// <summary>The invariant code for a role, as a token must spell it.</summary>
    public static string CodeFor(MemberRole role)
    {
        return EnumCode.Of(role);
    }

    /// <summary>
    ///     The highest role among a principal's claims, or <c>null</c> when it
    ///     carries none this system recognizes.
    /// </summary>
    /// <remarks>
    ///     A provider may emit the claim once or many times, so every value is
    ///     considered and the greatest wins — <see cref="MemberRole" /> is ordered
    ///     for exactly this. An unrecognized value is ignored rather than rejected:
    ///     a provider is free to carry roles that mean something to somebody else.
    /// </remarks>
    public static MemberRole? Highest(ClaimsPrincipal principal, string roleClaimType)
    {
        ArgumentNullException.ThrowIfNull(principal);

        MemberRole? highest = null;

        foreach (var claim in principal.FindAll(roleClaimType))
            if (EnumCode.TryParse<MemberRole>(claim.Value, out var role) && (highest is null || role > highest))
                highest = role;

        return highest;
    }

    /// <summary>
    ///     The role a validated principal acts with. A token that authenticated but
    ///     carries no recognized role is a <see cref="MemberRole.User" />:
    ///     membership is proven, and no administrative capability follows from it.
    /// </summary>
    public static MemberRole EffectiveRole(ClaimsPrincipal principal, string roleClaimType)
    {
        return Highest(principal, roleClaimType) ?? MemberRole.User;
    }

    /// <summary>
    ///     The identity a validated principal establishes, or <c>null</c> when it
    ///     carries no subject — which a token that passed validation should never
    ///     do, but the API reads rather than assumes.
    /// </summary>
    public static MemberIdentity? IdentityOf(ClaimsPrincipal principal, string roleClaimType)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirstValue("sub");

        return string.IsNullOrWhiteSpace(subject)
            ? null
            : new MemberIdentity(subject, EffectiveRole(principal, roleClaimType));
    }
}
