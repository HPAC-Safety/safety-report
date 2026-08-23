using HpacSafety.Core.Features.Anonymization.Stages;

namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// Stage 1 of the anonymization pipeline: the deterministic scrub. No AI, no
/// network, no database, no configuration — it runs before any model sees a
/// report, because nothing should ask a language model to remove what a regular
/// expression removes reliably. See ADR-0003 and ADR-0027.
/// </summary>
/// <remarks>
/// <para>
/// The stages are a <b>chain of responsibility</b>, and the order is load
/// bearing:
/// </para>
/// <list type="number">
///   <item><description>structured answers — dropped or generalized, and harvested as tokens</description></item>
///   <item><description>email addresses, before anything can split one in half</description></item>
///   <item><description>URLs</description></item>
///   <item><description>membership identifiers, before the phone rule can claim their digits</description></item>
///   <item><description>phone numbers</description></item>
///   <item><description>
///     everything harvested from the structured answers — names to their role
///     word, launch, landing zone, make, model, contact details and
///     unclassified answers to a marker — in <b>one pass</b>, so a replacement
///     is never rescanned by a later token
///   </description></item>
/// </list>
/// <para>
/// The chain is assembled here and nowhere else. There is no way to construct a
/// scrub with a stage missing.
/// </para>
/// </remarks>
public sealed class DeterministicScrub
{
    private readonly ScrubVocabulary _vocabulary;
    private readonly ScrubStage _chain;

    /// <summary>
    /// Builds the scrub for one language's role words.
    /// </summary>
    /// <param name="vocabulary">The words that stand in for a name.</param>
    public DeterministicScrub(ScrubVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        _vocabulary = vocabulary;
        _chain = new StructuredFieldStage()
            .Then(new PatternStage(ScrubPatterns.Email))
            .Then(new PatternStage(ScrubPatterns.Url))
            .Then(new PatternStage(ScrubPatterns.MemberNumber))
            .Then(new PatternStage(ScrubPatterns.Phone))
            .Then(new HarvestedIdentifierStage());
    }

    /// <summary>Runs the whole chain over a report.</summary>
    /// <param name="request">The report's fields and the province a site is generalized to.</param>
    /// <returns>What survives, and the text stage 2 will summarize.</returns>
    public ScrubbedReport Scrub(ScrubRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var document = _chain.Scrub(new ScrubDocument(request, _vocabulary));
        return new ScrubbedReport(document.Fields);
    }
}
