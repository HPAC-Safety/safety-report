namespace HpacSafety.Core.Features.Reporting;

/// <summary>Something the auditor believes identifies a person.</summary>
/// <param name="Kind">What kind of identifier it looks like.</param>
/// <param name="Excerpt">The offending fragment, for a reviewer's eyes only.</param>
public sealed record PiiFinding(string Kind, string Excerpt);

/// <summary>The verdict on one piece of text.</summary>
/// <param name="IsClean">True when nothing identifying was found.</param>
/// <param name="Findings">Everything found, empty when clean.</param>
public sealed record PiiAuditResult(bool IsClean, IReadOnlyList<PiiFinding> Findings);

/// <summary>
/// A summary-only check for identifying text. It deliberately cannot accept
/// report content or private context: it evaluates what a public reader could
/// learn from the candidate summary itself.
/// </summary>
public interface IPiiAuditor
{
    /// <summary>Audits text that is about to be shown or published.</summary>
    Task<PiiAuditResult> AuditAsync(string text, CancellationToken cancellationToken);
}
