namespace HpacSafety.Core.Administration;

/// <summary>
/// The moderation actions written to the audit log. In a non-punitive reporting
/// system, being able to show who saw what is part of keeping the promise.
/// </summary>
public enum AuditAction
{
    ViewedRawReport = 0,
    EditedSummary = 1,
    ApprovedSummary = 2,
    ApprovedReport = 3,
    RejectedReport = 4,
    PublishedReport = 5,
    CreatedQuestion = 10,
    RevisedQuestion = 11,
    ReorderedQuestions = 12,
    DeactivatedQuestion = 13,
    DeletedQuestion = 14,
    EditedQuestionTranslation = 15,
}
