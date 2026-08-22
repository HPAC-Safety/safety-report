namespace HpacSafety.Core.Features.Anonymization.Stages;

/// <summary>
/// Replaces a name found in free text with the role word for the structured
/// field it was given in.
/// </summary>
/// <remarks>
/// Reporters refer to themselves and to the pilot by name far more often than
/// anyone expects, and the resulting sentence has to survive: the scrubbed text
/// is what stage 2 summarizes, and "[redacted] spiralled in" summarizes worse
/// than "the pilot spiralled in". See ADR-0028.
/// </remarks>
internal sealed class NameStage : ScrubStage
{
    protected override void Handle(ScrubDocument document)
    {
        if (document.Names.Count == 0)
        {
            return;
        }

        // Built once for the whole document rather than once per field: the
        // token list is the same for every field, and a name matcher is not
        // free to compile.
        var matchers = document.Names
            .Select(name => (Pattern: ScrubPatterns.Token(name.Token), name.Replacement))
            .ToList();

        document.RewriteValues(value =>
        {
            foreach (var (pattern, replacement) in matchers)
            {
                value = pattern.Replace(value, _ => replacement);
            }

            return value;
        });
    }
}
