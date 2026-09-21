namespace HpacSafety.Api.Authentication;

/// <summary>
/// The three authorization policies. An endpoint names one; nothing checks a
/// role by hand.
/// </summary>
/// <remarks>
/// These are the whole authorization surface. The API is the boundary — never
/// hidden markup, and never a client-side route guard (ADR-0048).
/// </remarks>
public static class HpacPolicies
{
    /// <summary>
    /// Any authenticated member, of any role. Proves HPAC membership and
    /// nothing more — this is what submitting a report will require.
    /// </summary>
    public const string Member = "hpac:member";

    /// <summary>
    /// A SafetyOfficer or an Administrator: the review queue, private report
    /// material, summary editing, and publication.
    /// </summary>
    public const string Reviewer = "hpac:reviewer";

    /// <summary>
    /// An Administrator: question revisions and shared choice lists, on top of
    /// every Reviewer capability.
    /// </summary>
    public const string Administrator = "hpac:administrator";
}
