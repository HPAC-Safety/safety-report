namespace HpacSafety.Api.Reports;

/// <summary>
///     The JSON report part of the one final multipart submission. See
///     <c>features/report-submission/README.md</c> for the full contract.
/// </summary>
/// <param name="Language">The reporter's UI locale — exactly <c>en-CA</c> or <c>fr-CA</c>.</param>
/// <param name="Answers">One entry for every answer-producing revision the client showed.</param>
public sealed record SubmitReportRequest(string? Language, IReadOnlyList<SubmitAnswerRequest>? Answers);

/// <summary>
///     One answer entry. Exactly one of <see cref="Value" />,
///     <see cref="OptionCodes" />, or <see cref="AttachmentPartIndexes" /> carries
///     data, matching the answered revision's type; the others stay null. Null or
///     empty in all three means the reporter skipped an optional question.
/// </summary>
/// <param name="QuestionRevisionId">The exact immutable revision this answers.</param>
/// <param name="Value">The answer, for every shape except multi-select and file upload.</param>
/// <param name="OptionCodes">The chosen labels, for a multi-select answer only.</param>
/// <param name="AttachmentPartIndexes">
///     Zero-based indexes into the multipart request's file parts, for a
///     file-upload answer only.
/// </param>
public sealed record SubmitAnswerRequest(
	string? QuestionRevisionId,
	string? Value,
	IReadOnlyList<string>? OptionCodes,
	IReadOnlyList<int>? AttachmentPartIndexes);

/// <summary>The opaque receipt a successful submission returns. Nothing else.</summary>
/// <param name="Id">The report's opaque identifier.</param>
/// <param name="Status">Always <c>submitted</c>.</param>
public sealed record SubmitReportResponse(string Id, string Status);
