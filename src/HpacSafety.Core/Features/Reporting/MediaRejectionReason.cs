namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Why an upload was refused. A code rather than a sentence: the edge localizes
/// it, so no user-facing string is written here. See AGENTS.md.
/// </summary>
public enum MediaRejectionReason
{
    /// <summary>Not a rejection.</summary>
    None = 0,

    /// <summary>Nothing was uploaded.</summary>
    Empty = 1,

    /// <summary>Larger than this deployment accepts.</summary>
    TooLarge = 2,

    /// <summary>The bytes are not any format this system recognises.</summary>
    UnrecognisedContent = 3,

    /// <summary>A recognised format, but not one this deployment accepts.</summary>
    UnacceptedMediaType = 4,

    /// <summary>The client claimed one format and uploaded another.</summary>
    DeclaredTypeMismatch = 5,
}
