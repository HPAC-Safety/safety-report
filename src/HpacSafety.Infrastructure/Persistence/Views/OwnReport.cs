namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     One row of the <c>own_reports</c> view: a report that is not deleted, carries a
///     receipt, and is not currently public — what the browser holding the receipt
///     may see of it (ADR-0196). The view, not this type, decides who may see what;
///     a row is only ever read together with a receipt hash that matches.
/// </summary>
public sealed class OwnReport
{
	/// <summary>The report's opaque identifier.</summary>
	public string Id { get; private init; } = string.Empty;

	/// <summary>
	///     The SHA-256 of the receipt that opens this row. Only ever compared in
	///     SQL; never returned.
	/// </summary>
	public string ReceiptHash { get; private init; } = string.Empty;

	/// <summary>
	///     When the report was submitted. Shown only to the holder: the public feed
	///     still never exposes it (ADR-0153).
	/// </summary>
	public DateTimeOffset SubmittedAt { get; private init; }

	/// <summary>
	///     Whether the reporter consented to publication. False means the report is
	///     for good unpublished and never has a summary.
	/// </summary>
	public bool ForPublication { get; private init; }

	/// <summary>The language the reporter wrote the report in, as a locale code.</summary>
	public string Language { get; private init; } = string.Empty;

	/// <summary>
	///     The latest live summary revision's English text, approved or not; null
	///     before the Worker has made one, and always null without publication
	///     consent.
	/// </summary>
	public string? AiSummaryEn { get; private init; }

	/// <summary>The latest live summary revision's French text; null when English is.</summary>
	public string? AiSummaryFr { get; private init; }

	/// <summary>How many files this report has in <c>own_report_media</c>.</summary>
	public int AttachmentCount { get; private init; }
}
