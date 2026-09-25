namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The lifecycle of an occurrence report. Stored as a stable invariant code and
///     localized only at the edge. Once the Worker is done with a report it is
///     Pending, Published, or Unpublished (ADR-0125). See
///     skills/incident-domain-model/SKILL.md.
/// </summary>
public enum ReportStatus
{
	Submitted = 0,
	Summarizing = 1,

	/// <summary>A consented report has a summary pair waiting for a reviewer.</summary>
	Pending = 2,

	/// <summary>
	///     The worker could not produce a summary. The report still reaches a human,
	///     with the error attached, so that it can never become invisible.
	/// </summary>
	SummaryFailed = 3,
	Published = 6,

	/// <summary>
	///     Not public: a reviewer unpublished it, or its reporter did not consent to
	///     publication, in which case it stays here for good (REQ-DOM-015).
	/// </summary>
	Unpublished = 7,
}
