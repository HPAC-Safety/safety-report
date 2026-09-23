namespace HpacSafety.Core.Features.Reporting;

/// <summary>What sort of thing a reporter uploaded.</summary>
public enum MediaKind
{
	/// <summary>A photo.</summary>
	Image = 0,

	/// <summary>A video. Accepted and retained, but not yet strippable — see issue #65.</summary>
	Video = 1,

	/// <summary>
	///     A document. Accepted, validated, and retained as the private original — it
	///     never has a stripped derivative at all. There is no malware scan
	///     (ADR-0089); format validation is the only gate. See issue #310.
	/// </summary>
	Document = 2,
}
