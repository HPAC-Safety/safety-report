namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// What ingest concluded. Three states rather than two, because "accepted" and
/// "safe to look at" are not the same thing: a video is retained but has no
/// derivative until #65, and a reviewer must see nothing for it rather than
/// falling through to the original.
/// </summary>
public enum MediaIngestStatus
{
    /// <summary>Refused. The bytes stay in quarantine and expire; nothing was promoted.</summary>
    Rejected = 0,

    /// <summary>
    /// Accepted and retained, but this system cannot strip the format yet, so no
    /// derivative exists and nothing is viewable. Fails closed by design.
    /// </summary>
    AwaitingStripping = 1,

    /// <summary>Accepted, stripped, and viewable through a pre-signed GET.</summary>
    Stripped = 2,
}
