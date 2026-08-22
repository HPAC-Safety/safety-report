namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// The words the scrub writes in place of a name, in the language the report was
/// filed in.
/// </summary>
/// <remarks>
/// <para>
/// A name found in a narrative is replaced by the <b>role</b> the structured
/// field it came from gives that person — "the pilot", "the reporter" — rather
/// than by a placeholder such as <c>[redacted]</c>. The scrubbed text still
/// reads as prose, so stage 2 summarization is not degraded by it. See
/// ADR-0028.
/// </para>
/// <para>
/// There is deliberately no locale lookup here: the caller supplies the
/// vocabulary, so adding a language is supplying two words rather than editing
/// the scrub. The words themselves are <b>HPAC terminology decided by a person,
/// not machine output</b>, and they are not to be re-translated.
/// </para>
/// <para>
/// <b>The French role words are always masculine, whoever was flying.</b> That
/// is not an oversight and not a default — it is the anonymising property. French
/// forces an article where English does not, and "la pilote" in a fifty-person
/// flying community narrows the field considerably: matching the article to the
/// person would put back the exact fact the scrub had just removed. Masculine is
/// the grammatical generic, so uniformity costs nothing linguistically and buys
/// the whole point. See ADR-0028.
/// </para>
/// </remarks>
public sealed class ScrubVocabulary
{
    /// <summary>Builds a vocabulary. Both words are required and neither may be blank.</summary>
    public ScrubVocabulary(string reporter, string pilot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reporter);
        ArgumentException.ThrowIfNullOrWhiteSpace(pilot);

        Reporter = reporter;
        Pilot = pilot;
    }

    /// <summary>The role words for a report filed in Canadian English.</summary>
    public static ScrubVocabulary EnglishCanada { get; } = new("the reporter", "the pilot");

    /// <summary>
    /// The role words for a report filed in Canadian French. <b>Masculine
    /// always</b>, regardless of who was flying — see the remarks on this type
    /// and ADR-0028. <c>déclarant</c> is the standard term for someone filing an
    /// official report and matches the institutional register of a safety
    /// authority.
    /// </summary>
    public static ScrubVocabulary FrenchCanada { get; } = new("le déclarant", "le pilote");

    /// <summary>Stands in for the reporter's name. Never varies with the person.</summary>
    public string Reporter { get; }

    /// <summary>Stands in for the pilot in command's name. Never varies with the person.</summary>
    public string Pilot { get; }
}
