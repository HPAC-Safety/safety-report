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
}
