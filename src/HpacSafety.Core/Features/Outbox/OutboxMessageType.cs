namespace HpacSafety.Core.Features.Outbox;

/// <summary>
///     What kind of work an outbox message carries. Stored as a stable invariant
///     code, like every other domain enum. See <c>docs/data-and-persistence.md</c>.
/// </summary>
public enum OutboxMessageType
{
	/// <summary>Summarize a report: the Worker's one model call per report.</summary>
	SummarizeReport = 0,

	/// <summary>
	///     Complete one uploaded file's attachment path — a safe derivative for an
	///     image/video or a preserved original for a document, made available for
	///     reviewer preview or forced download. One message per file. There is no
	///     malware scan (ADR-0089). See issue #81.
	/// </summary>
	ProcessAttachment = 1,

	/// <summary>
	///     Mechanically translate a report's answers into their second official
	///     language via <c>ITranslator</c>. One message per report; the Worker loads
	///     that report's own untranslated answers rather than the message carrying
	///     them. See ADR-0080.
	/// </summary>
	TranslateAnswers = 2,

	/// <summary>
	///     Machine-translate one revision of a member's comment into the other
	///     official language. The payload is the revision's ID (ADR-0114).
	/// </summary>
	TranslateComment = 3,

	/// <summary>
	///     Machine-translate the missing language of one type-ahead value a reporter
	///     added, onto the value itself. The payload is the choice's ID (ADR-0129).
	/// </summary>
	TranslateChoice = 4,
}
