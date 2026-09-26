namespace HpacSafety.Core;

/// <summary>
///     Which of a report's media compartments a blob lives in. The
///     compartment is part of the key, so "is this safe to show a reviewer?" is
///     answerable from the key alone rather than from a database lookup that a
///     caller might forget. See ADR-0026.
/// </summary>
public enum MediaCompartment
{
	/// <summary>
	///     Where a reporter's upload waits, unvalidated, until a submission claims it:
	///     <c>quarantine/&lt;upload id&gt;</c>. It belongs to no report yet, and is
	///     expired automatically by a bucket lifecycle rule — a delete marker after a
	///     day, then the noncurrent version a day after that, because the bucket is
	///     versioned. See docs/data-handling.md, ADR-0096, and ADR-0126.
	/// </summary>
	Quarantine = 0,

	/// <summary>
	///     The private source record: <c>&lt;report id&gt;/original/&lt;file&gt;</c>.
	///     Retained exactly as uploaded, never shown to anyone.
	/// </summary>
	Original = 1,

	/// <summary>
	///     The metadata-stripped derivative a reviewer is shown:
	///     <c>&lt;report id&gt;/stripped/&lt;file&gt;</c>. The only compartment
	///     <see cref="Features.Reporting.ReviewerMediaLink" /> will issue a URL for.
	/// </summary>
	Stripped = 2,

	/// <summary>
	///     A staff-only private attachment on a report:
	///     <c>&lt;report id&gt;/private/&lt;attachment id&gt;</c> (ADR-0135). Stored
	///     exactly as a safety officer or administrator uploaded it, and signed a
	///     URL for only by <see cref="Features.PrivateAttachments.PrivateAttachmentLink" />.
	///     No reporter or public code path names it.
	/// </summary>
	Private = 3,
}
