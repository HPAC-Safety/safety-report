using System.Text.RegularExpressions;

namespace HpacSafety.Core.Features.Anonymization.Stages;

/// <summary>
/// A chain link that replaces everything one pattern matches with
/// <see cref="ScrubMarker.Removed"/>.
/// </summary>
/// <remarks>
/// One class rather than four near-identical ones. The variation between the
/// email, URL, phone, and member-number stages is the pattern and nothing else,
/// and a base class per pattern would be a layer rather than a pattern.
/// </remarks>
internal sealed class PatternStage : ScrubStage
{
    private readonly Regex _pattern;

    internal PatternStage(Regex pattern) => _pattern = pattern;

    protected override void Handle(ScrubDocument document) =>
        document.RewriteValues(value => _pattern.Replace(value, _ => ScrubMarker.Removed));
}
