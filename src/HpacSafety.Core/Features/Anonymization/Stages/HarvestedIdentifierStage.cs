using System.Text;
using System.Text.RegularExpressions;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Anonymization.Stages;

/// <summary>
/// Replaces everything harvested from the structured answers: a name becomes the
/// role word for the field it came from, and a launch, landing zone, aircraft
/// model, contact detail, or unclassified answer becomes
/// <see cref="ScrubMarker.Removed"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>One pass, one matcher.</b> Names and places used to be two stages, each
/// looping its tokens over the value and rewriting it every time. That let a
/// later token match inside text an earlier token had just written: a reporter
/// surnamed "Pilot" turned "the pilot" into "the the reporter", mislabelling who
/// was flying and handing stage 2 ungrammatical text — which is the entire
/// reason role words exist rather than placeholders. Every token is now one
/// branch of a single alternation, longest first, so a replacement is never
/// rescanned.
/// </para>
/// <para>
/// A place and an aircraft get no role word. "The pilot" is still a person doing
/// something in a sentence; there is no equivalent noun for a launch that does
/// not narrow it down. The aircraft is described by its certification class,
/// which comes from the reporter's own answer and never from the model name.
/// </para>
/// </remarks>
internal sealed class HarvestedIdentifierStage : ScrubStage
{
    private const string LeadGroup = "lead";
    private const string BranchPrefix = "t";

    protected override void Handle(ScrubDocument document)
    {
        var replacements = new List<string>();
        var branches = new List<string>();

        // Longest first: "Mount Ferndale" has to win over "Mount", or the
        // alternation takes the short branch and leaves "Ferndale" standing.
        var harvested = document.Names
            .Select(name => (name.Token, Replacement: name.Replacement))
            .Concat(document.Terms.Select(term => (Token: term, Replacement: ScrubMarker.Removed)))
            .OrderByDescending(entry => entry.Token.Length)
            .ToList();

        if (harvested.Count == 0)
        {
            return;
        }

        foreach (var (token, replacement) in harvested)
        {
            branches.Add($"(?<{BranchPrefix}{replacements.Count}>{ScrubPatterns.TokenBody(token)})");
            replacements.Add(replacement);
        }

        // The optional lead is a French elision — "d'Élise". Substituting the
        // name alone leaves "d'le pilote", which is not French.
        var matcher = ScrubPatterns.Alternation(
            $@"(?<{LeadGroup}>\b[dlDL]['’])?(?<!\w)(?:{string.Join("|", branches)})s?(?!\w)");

        document.RewriteValues(value => CollapseDeterminers(
            ScrubGuard.Replace(matcher, value, match => Substitute(match, replacements)),
            document.Vocabulary));
    }

    /// <summary>
    /// Removes a determiner the reporter wrote immediately before one the scrub
    /// wrote.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single alternation cannot rescan its own output, but the reporter's
    /// own prose can still supply the first half. A reporter surnamed "Pilot"
    /// makes the ordinary word "pilot" a token, so "The pilot then threw the
    /// reserve" became "The the reporter then threw" — ungrammatical, and
    /// exactly what role words exist to avoid.
    /// </para>
    /// <para>
    /// <b>The article the scrub wrote is the one that survives</b>, carrying the
    /// reporter's capitalisation. Keeping the reporter's instead would put
    /// "la pilote" back into a French summary, which is the whole point of the
    /// uniform masculine article — see ADR-0028.
    /// </para>
    /// </remarks>
    private static string CollapseDeterminers(string value, ScrubVocabulary vocabulary)
    {
        var written = new[] { Determiner(vocabulary.Reporter), Determiner(vocabulary.Pilot) }
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (written.Count == 0)
        {
            return value;
        }

        var pattern = ScrubPatterns.Alternation(
            $@"(?<![\w'’])(?<source>the|a|an|le|la|les|un|une)\s+(?<written>{string.Join("|", written.Select(Regex.Escape))})(?=\s)");

        return ScrubGuard.Replace(pattern, value, match =>
        {
            var word = match.Groups["written"].Value;

            return char.IsUpper(match.Groups["source"].Value[0])
                ? char.ToUpperInvariant(word[0]) + word[1..]
                : word;
        });
    }

    private static string Determiner(string roleWord)
    {
        var space = roleWord.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? roleWord[..space] : string.Empty;
    }

    private static bool StartsWithArticle(string replacement, string article) =>
        replacement.StartsWith(article + " ", StringComparison.OrdinalIgnoreCase);

    private static string Substitute(Match match, List<string> replacements)
    {
        var replacement = string.Empty;

        for (var branch = 0; branch < replacements.Count; branch++)
        {
            if (match.Groups[BranchPrefix + branch.ToString(System.Globalization.CultureInfo.InvariantCulture)].Success)
            {
                replacement = replacements[branch];
                break;
            }
        }

        var lead = match.Groups[LeadGroup];

        if (!lead.Success)
        {
            return replacement;
        }

        var elided = lead.Value[0];
        var capitalised = char.IsUpper(elided);

        // "de" + "le pilote" contracts to "du pilote".
        if (elided is 'd' or 'D' && StartsWithArticle(replacement, "le"))
        {
            var contracted = new StringBuilder(capitalised ? "Du " : "du ");
            return contracted.Append(replacement, 3, replacement.Length - 3).ToString();
        }

        // "l'" + "le pilote" is the article twice over. The role word carries
        // its own, so the elided one is absorbed rather than left as "l'le".
        if (elided is 'l' or 'L' && (StartsWithArticle(replacement, "le") || StartsWithArticle(replacement, "la")))
        {
            return capitalised
                ? char.ToUpperInvariant(replacement[0]) + replacement[1..]
                : replacement;
        }

        // Anything else is left as the reporter wrote it. Guessing further at
        // French grammar is how a deterministic pass starts inventing.
        return lead.Value + replacement;
    }
}

/// <summary>
/// Runs a replacement and refuses to let a timeout carry the report out with it.
/// </summary>
/// <remarks>
/// <see cref="RegexMatchTimeoutException"/> carries the subject text on its
/// <c>Input</c> property. That text is the raw narrative, and an exception that
/// reaches a log or an error response would take it along — the one thing
/// docs/data-handling.md says never happens. The report still fails, loudly, and
/// still reaches a human through <c>Report.FailSummarization</c>; it just fails
/// without the narrative attached.
/// </remarks>
internal static class ScrubGuard
{
    internal static string Replace(Regex pattern, string value, MatchEvaluator evaluator)
    {
        try
        {
            return pattern.Replace(value, evaluator);
        }
        catch (RegexMatchTimeoutException)
        {
            throw new DomainRuleViolationException(
                "The deterministic scrub timed out on this report and refused to continue. "
                + "The report is not scrubbed and must not be summarized.");
        }
    }
}
