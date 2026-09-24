namespace HpacSafety.Api.Admin;

/// <summary>
///     One row of the admin report list: state and timing only, never answer or
///     summary text (REQ-MOD-030).
/// </summary>
/// <param name="Id">The report.</param>
/// <param name="SubmittedAt">When it was received.</param>
/// <param name="Status">Its workflow status, as a lowercase code such as <c>pending_review</c>.</param>
/// <param name="Language">The locale it was written in.</param>
/// <param name="Consent">Publication consent: <c>yes</c>, <c>no</c>, or <c>unanswered</c>.</param>
/// <param name="IsStuck">Still Submitted or Summarizing more than a day after submission.</param>
public sealed record ReportListItem(
	string Id,
	DateTimeOffset SubmittedAt,
	string Status,
	string Language,
	string Consent,
	bool IsStuck);

/// <summary>
///     Everything a reviewer needs to judge one report, and nothing more
///     (REQ-MOD-031). No storage key or link: an attachment is opened only through
///     its own audited request. <c>Version</c> is the opaque value every review
///     command sends back (ADR-0105); <c>RejectionNote</c> is reviewer-only
///     (REQ-MOD-058); <c>PublishedAt</c> is set while the report is public.
/// </summary>
public sealed record ReportDetail(
	string Id,
	DateTimeOffset SubmittedAt,
	string Status,
	string Language,
	string Consent,
	bool IsStuck,
	string? SummaryError,
	IReadOnlyList<ReportAnswerView> Answers,
	ReportSummaryView? Summary,
	IReadOnlyList<ReportAttachmentView> Attachments,
	string Version,
	string? RejectionNote,
	DateTimeOffset? PublishedAt);

/// <summary>A review command that carries nothing but the version the reviewer loaded.</summary>
public sealed record ReviewCommand(string? Version);

/// <summary>
///     Both texts of the pair, saved together, with the version the reviewer loaded.
///     <c>SourceEn</c>/<c>SourceFr</c> are <c>human</c> (the default) or
///     <c>machine</c> for a language filled by an accepted translation (ADR-0108).
/// </summary>
public sealed record SaveSummaryPairRequest(
	string? Version,
	string? AiSummaryEn,
	string? AiSummaryFr,
	string? SourceEn = null,
	string? SourceFr = null);

/// <summary>A rejection, with an optional reviewer-only note.</summary>
public sealed record RejectReportRequest(string? Version, string? Note);

/// <summary>
///     One question as it was asked, with every value the reporter gave for it — one
///     for most questions, several for a multi-select, none for a skipped question.
///     <c>Type</c> is the question's type code, so a reader can show a stored date,
///     time, or yes/no in their own language (REQ-MOD-075).
/// </summary>
public sealed record ReportAnswerView(
	string QuestionKey,
	string LabelEn,
	string LabelFr,
	string Type,
	bool IsPrivate,
	IReadOnlyList<ReportAnswerValueView> Values);

/// <summary>One stored answer string and its second-language counterpart, when one exists.</summary>
/// <param name="Value">The reporter's own words, in <paramref name="Locale" />.</param>
/// <param name="Locale">The language <paramref name="Value" /> is written in.</param>
/// <param name="TranslatedValue">
///     The other official language, once supplied. Always null for an answer that
///     never has one, such as a name, an email, or a date (ADR-0110).
/// </param>
/// <param name="TranslationSource"><c>auto</c>, <c>human</c>, or <c>choice</c>, once supplied.</param>
public sealed record ReportAnswerValueView(
	string Value,
	string Locale,
	string? TranslatedValue,
	string? TranslationSource);

/// <summary>The bilingual summary pair with its shared provenance and approval.</summary>
/// <remarks>
///     <c>SourceEn</c> and <c>SourceFr</c> say how each language was produced:
///     <c>generated</c>, <c>human</c>, or <c>machine</c> (ADR-0108).
/// </remarks>
public sealed record ReportSummaryView(
	string AiSummaryEn,
	string AiSummaryFr,
	string Model,
	string PromptVersion,
	DateTimeOffset GeneratedAt,
	DateTimeOffset UpdatedAt,
	string? ApprovedBySubject,
	DateTimeOffset? ApprovedAt,
	string SourceEn,
	string SourceFr);

/// <summary>An attachment's kind and whether it can be opened now.</summary>
/// <param name="Id">The attachment, for its view or download request.</param>
/// <param name="Kind"><c>image</c>, <c>video</c>, or <c>document</c>.</param>
/// <param name="State"><c>ready</c>, <c>processing</c>, or <c>failed</c>.</param>
public sealed record ReportAttachmentView(
	string Id,
	string Kind,
	string State);
