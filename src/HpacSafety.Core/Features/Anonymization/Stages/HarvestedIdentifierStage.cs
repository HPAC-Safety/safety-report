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

        document.RewriteValues(value => ScrubGuard.Replace(
            matcher,
            value,
            match => Substitute(match, replacements)));
    }

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

        // "de" + "le pilote" contracts to "du pilote". Only the article the
        // vocabulary actually carries is contracted; anything else is left as
        // the reporter wrote it, because guessing at French grammar is how a
        // scrub starts inventing.
        if (lead.Value[0] is 'd' or 'D' && replacement.StartsWith("le ", StringComparison.OrdinalIgnoreCase))
        {
            var contracted = new StringBuilder(char.IsUpper(lead.Value[0]) ? "Du " : "du ");
            return contracted.Append(replacement, 3, replacement.Length - 3).ToString();
        }

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
