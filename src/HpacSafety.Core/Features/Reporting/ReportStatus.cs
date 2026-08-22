using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// The lifecycle of an occurrence report. Stored as a stable invariant code and
/// localized only at the edge. See skills/incident-domain-model/SKILL.md.
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
