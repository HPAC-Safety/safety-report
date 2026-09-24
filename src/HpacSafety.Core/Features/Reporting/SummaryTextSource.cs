namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     How one language of a summary pair was produced (ADR-0106). Recorded per
///     language, so a later reviewer can see which text a person wrote.
/// </summary>
public enum SummaryTextSource
{
	/// <summary>The Worker's one anonymized model call wrote it.</summary>
	Generated = 0,

	/// <summary>A reviewer typed or edited it.</summary>
	Human = 1,

	/// <summary>A reviewer accepted a machine translation of the other language.</summary>
	Machine = 2,
}
