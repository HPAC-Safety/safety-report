using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

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
/// The second-pass check over text the deterministic scrub has already been
/// through. Deliberately a separate stage: the scrub catches what it knows the
/// shape of, and this catches what it does not.
/// </summary>
public interface IPiiAuditor
{
    /// <summary>Audits text that is about to be shown or published.</summary>
    Task<PiiAuditResult> AuditAsync(string text, CancellationToken cancellationToken);
}
