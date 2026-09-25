namespace HpacSafety.Api.Reports;

/// <summary>
///     The one final submission's JSON body. See
///     <c>features/report-submission/README.md</c> for the full contract.
/// </summary>
/// <param name="Language">The reporter's UI locale — exactly <c>en-CA</c> or <c>fr-CA</c>.</param>
/// <param name="Answers">One entry for every answer-producing revision the client showed.</param>
public sealed record SubmitReportRequest(string? Language, IReadOnlyList<SubmitAnswerRequest>? Answers);

/// <summary>
///     One answer entry. Exactly one of <see cref="Value" />,
///     <see cref="Choices" />, or <see cref="Attachments" /> carries
///     data, matching the answered revision's type; the others stay null. Null or
///     empty in all three means the reporter skipped an optional question.
/// </summary>
/// <param name="QuestionRevisionId">The exact immutable revision this answers.</param>
/// <param name="Value">The answer, for every shape except multi-select and file upload.</param>
/// <param name="Choices">
///     The chosen choices' labels, in the reporter's language, for a multi-select
///     answer only. Never a choice code (ADR-0095).
/// </param>
/// <param name="Attachments">
///     This question's files, for a file-upload answer only: each the upload id
///     <c>POST /api/v1/uploads</c> returned and the file's name (ADR-0096,
///     ADR-0097).
/// </param>
public sealed record SubmitAnswerRequest(
	string? QuestionRevisionId,
	string? Value,
	IReadOnlyList<string>? Choices,
	IReadOnlyList<SubmitAttachmentRequest>? Attachments);

/// <summary>One file a file-upload answer claims.</summary>
/// <param name="UploadId">The id its upload returned.</param>
/// <param name="FileName">
///     The reporter's name for the file. Sanitized and kept only as a reviewer's
///     download name (ADR-0097); optional.
/// </param>
public sealed record SubmitAttachmentRequest(string? UploadId, string? FileName);

/// <summary>The opaque receipt a successful submission returns. Nothing else.</summary>
/// <param name="Id">The report's opaque identifier.</param>
/// <param name="Status">Always <c>submitted</c>.</param>
public sealed record SubmitReportResponse(string Id, string Status);
