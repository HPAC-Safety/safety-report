using HpacSafety.Core;

namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     One row of the <c>search_admin_reports(query)</c> function: a live report
///     whose answers (private included), choice labels in both languages,
///     summary pair, private notes, member comments, or attachment file names
///     (public and private) match a reviewer's search text, with the rank it
///     matched at (REQ-MOD-130..133, ADR-0156). Read only through
///     <see cref="HpacSafetyDbContext.SearchAdminReports" />, never queried
///     directly.
/// </summary>
public sealed class AdminReportSearchMatch
{
	/// <summary>The report that matched.</summary>
	public TinyId ReportId { get; private init; }

	/// <summary>
	///     How well it matched: the greater of its full-text rank (English or
	///     French) and its trigram similarity to the query. Higher is a closer
	///     match; used only to order results, never shown.
	/// </summary>
	public double Rank { get; private init; }
}
