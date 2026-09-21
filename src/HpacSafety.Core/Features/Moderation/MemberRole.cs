namespace HpacSafety.Core.Features.Moderation;

/// <summary>
/// What a signed-in member may do. Read from a claim on a validated token and
/// never persisted — this system stores no user records. See
/// docs/authentication.md.
/// </summary>
/// <remarks>
/// The values are ordered so that "the highest role wins" is a comparison. A
/// token carrying several recognized role claims resolves to the greatest one.
/// </remarks>
public enum MemberRole
{
    /// <summary>Proves HPAC membership. May submit a report, and nothing else.</summary>
    User = 0,

    /// <summary>Reviews, edits, approves, and rejects reports and summaries.</summary>
    SafetyOfficer = 1,

    /// <summary>Everything a safety officer may do, plus authoring the question bank.</summary>
    Administrator = 2,
}
