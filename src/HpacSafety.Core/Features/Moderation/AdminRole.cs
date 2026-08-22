using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Moderation;

/// <summary>
/// What an administrator may do. Authentication is upstream at
/// members.hpac.ca; roles are ours. See docs/authentication.md.
/// </summary>
public enum AdminRole
{
    /// <summary>Reviews, edits, approves, and rejects reports and summaries.</summary>
    SafetyOfficer = 0,

    /// <summary>Everything a safety officer may do, plus editing the question bank
    /// and the allowlist.</summary>
    Administrator = 1,
}
