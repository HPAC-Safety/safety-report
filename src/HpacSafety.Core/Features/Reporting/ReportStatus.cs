namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// The lifecycle of an occurrence report, stored as a stable invariant code.
/// </summary>
public enum ReportStatus
{
    Submitted = 0,
    Summarizing = 1,
    PendingReview = 2,

    /// <summary>
    /// The worker could not produce a summary. The report still reaches a human,
    /// with the error attached, so that it can never become invisible.
    /// </summary>
    SummaryFailed = 3,
    Approved = 4,
    Rejected = 5,
    Published = 6,
}
