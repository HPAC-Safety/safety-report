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
	///     Complete one uploaded file's attachment path — malware controls, and
	///     making a verified derivative available for reviewer preview. One message
	///     per file. See issue #81.
	/// </summary>
	ProcessAttachment = 1,

	/// <summary>
	///     Mechanically translate a report's answers into their second official
	///     language via <c>ITranslator</c>. One message per report; the Worker loads
	///     that report's own untranslated answers rather than the message carrying
	///     them. See ADR-0080.
	/// </summary>
	TranslateAnswers = 2
}
