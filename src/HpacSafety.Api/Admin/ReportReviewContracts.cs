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
///     its own audited request.
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
	IReadOnlyList<ReportAttachmentView> Attachments);

/// <summary>
///     One question as it was asked, with every value the reporter gave for it — one
///     for most questions, several for a multi-select, none for a skipped question.
/// </summary>
public sealed record ReportAnswerView(
	string QuestionKey,
	string LabelEn,
	string LabelFr,
	bool IsPrivate,
	IReadOnlyList<ReportAnswerValueView> Values);

/// <summary>One stored answer string and its second-language counterpart, when one exists.</summary>
/// <param name="Value">The reporter's own words, in <paramref name="Locale" />.</param>
/// <param name="Locale">The language <paramref name="Value" /> is written in.</param>
/// <param name="TranslatedValue">The other official language, once supplied.</param>
/// <param name="TranslationSource"><c>auto</c> or <c>human</c>, once supplied.</param>
public sealed record ReportAnswerValueView(
	string Value,
	string Locale,
	string? TranslatedValue,
	string? TranslationSource);

/// <summary>The bilingual summary pair with its shared provenance and approval.</summary>
public sealed record ReportSummaryView(
	string AiSummaryEn,
	string AiSummaryFr,
	string Model,
	string PromptVersion,
	DateTimeOffset GeneratedAt,
	DateTimeOffset UpdatedAt,
	string? ApprovedBySubject,
	DateTimeOffset? ApprovedAt);

/// <summary>An attachment's kind and whether it can be opened now.</summary>
/// <param name="Id">The attachment, for its view or download request.</param>
/// <param name="Kind"><c>image</c>, <c>video</c>, or <c>document</c>.</param>
/// <param name="State"><c>ready</c>, <c>processing</c>, or <c>failed</c>.</param>
public sealed record ReportAttachmentView(
	string Id,
	string Kind,
	string State);
