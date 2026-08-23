namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Which of a report's three media compartments a blob lives in. The
/// compartment is part of the key, so "is this safe to show a reviewer?" is
/// answerable from the key alone rather than from a database lookup that a
/// caller might forget. See ADR-0026.
/// </summary>
public enum MediaCompartment
{
    /// <summary>
    /// Where every upload lands, before anything has looked at it:
    /// <c>quarantine/&lt;report id&gt;/&lt;file&gt;</c>. Unverified bytes, expired
    /// automatically by a bucket lifecycle rule — a delete marker after a day,
    /// then the noncurrent version a day after that, because the bucket is
    /// versioned. See docs/data-handling.md.
    /// </summary>
    Quarantine = 0,

    /// <summary>
    /// The private source record: <c>&lt;report id&gt;/original/&lt;file&gt;</c>.
    /// Retained exactly as uploaded, never shown to anyone.
    /// </summary>
    Original = 1,

    /// <summary>
    /// The metadata-stripped derivative a reviewer is shown:
    /// <c>&lt;report id&gt;/stripped/&lt;file&gt;</c>. The only compartment
    /// <see cref="Features.Reporting.ReviewerMediaLink" /> will issue a URL for.
    /// </summary>
    Stripped = 2,
}
