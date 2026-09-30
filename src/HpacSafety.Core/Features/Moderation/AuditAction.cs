namespace HpacSafety.Core.Features.Moderation;

/// <summary>
///     The moderation actions written to the audit log. In a non-punitive reporting
///     system, being able to show who saw what is part of keeping the promise.
/// </summary>
public enum AuditAction
{
	ViewedRawReport = 0,
	EditedSummary = 1,
	ApprovedSummary = 2,
	ApprovedReport = 3,
	RejectedReport = 4,
	PublishedReport = 5,
	DeletedReport = 6,
	ReopenedReport = 7,
	UnpublishedReport = 8,

	/// <summary>
	///     A reviewer restored an earlier summary revision as a new one (ADR-0177).
	///     Like <see cref="EditedSummary" />, it records the subject and the report,
	///     never any text.
	/// </summary>
	RolledBackSummary = 9,

	CreatedQuestion = 10,
	RevisedQuestion = 11,
	ReorderedQuestions = 12,
	DeactivatedQuestion = 13,
	DeletedQuestion = 14,
	DeletedQuestionRevision = 15,
	SignedInSucceeded = 20,
	SignedInFailed = 21,
	ViewedAttachment = 22,
	HidComment = 30,
	DeletedComment = 31,
	HidMedia = 32,
	ShowedMedia = 33,
	ApprovedTypeAheadValue = 40,
	CorrectedTypeAheadValue = 41,
	RemovedTypeAheadValue = 42,
	MergedTypeAheadValue = 43,
	RelinkedTypeAheadValue = 44,
	RemovedPrivateNote = 50,
	DownloadedPrivateAttachment = 51,
	RemovedPrivateAttachment = 52,

	/// <summary>
	///     A reviewer downloaded the raw original of an image or video that has no
	///     stripped derivative yet (still processing, or failed) — decision 16,
	///     issue #427. Distinct from <see cref="ViewedAttachment" /> so an audit
	///     reader can spot a raw original, EXIF and GPS intact, without joining to
	///     the file's processing state.
	/// </summary>
	DownloadedOriginalMedia = 53,
}
