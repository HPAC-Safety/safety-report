namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     One row of the <c>public_reports</c> view: a publishable report, carrying
///     exactly the public DTO's allowlist (CON-DP-011). The view, not this type,
///     decides what is public; a report that stops being publishable simply has
///     no row (#28).
/// </summary>
public sealed class PublicReport
{
	/// <summary>
	///     The report's opaque identifier, read as its string form so the feed's
	///     keyset cursor can compare it in SQL.
	/// </summary>
	public string Id { get; private init; } = string.Empty;

	/// <summary>The approved English summary.</summary>
	public string AiSummaryEn { get; private init; } = string.Empty;

	/// <summary>The approved French summary.</summary>
	public string AiSummaryFr { get; private init; } = string.Empty;

	/// <summary>When the report became public.</summary>
	public DateTimeOffset PublishedAt { get; private init; }

	/// <summary>Its comments that are neither deleted nor hidden (ADR-0114).</summary>
	public int CommentCount { get; private init; }

	/// <summary>
	///     When the report was submitted — the public feed's sort and keyset
	///     cursor key (#570). Never part of the public DTO; the feed displays
	///     <see cref="PublishedAt" /> instead.
	/// </summary>
	public DateTimeOffset SubmittedAt { get; private init; }

	/// <summary>
	///     How many rows this report has in <c>public_report_media</c> — the
	///     attachment count a public viewer sees (issue #427, decisions 1-2).
	/// </summary>
	public int PublicAttachmentCount { get; private init; }

	/// <summary>
	///     How many non-deleted <c>report_files</c> rows this report has, whatever
	///     their kind, state, or visibility — the attachment count a signed-in
	///     <c>SafetyOfficer</c>/<c>Administrator</c> sees. Never counts a staff-only
	///     private attachment (ADR-0135). See issue #427, decisions 1-2.
	/// </summary>
	public int FullAttachmentCount { get; private init; }

	/// <summary>
	///     The language the reporter wrote the report in, as a locale code
	///     (<c>en-CA</c> or <c>fr-CA</c>). Public only on a report's own page, so
	///     the page can say a summary was translated from it (#682, ADR-0176);
	///     the feed never carries it.
	/// </summary>
	public string Language { get; private init; } = string.Empty;
}
