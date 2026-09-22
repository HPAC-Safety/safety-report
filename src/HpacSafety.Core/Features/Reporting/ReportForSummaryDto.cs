namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     Exactly what the Worker needs to summarize one report: the language to
///     summarize in, and every eligible answered field — labeled in that language,
///     against the exact revision it was answered under, and classified private or
///     not. Excludes consent, skipped answers, file-upload answers, and deleted
///     content; nothing here carries attachment bytes, storage keys, or admin/audit
///     data.
/// </summary>
public sealed record ReportForSummaryDto(TinyId ReportId, Locale Language, IReadOnlyList<ClassifiedReportField> Fields);
