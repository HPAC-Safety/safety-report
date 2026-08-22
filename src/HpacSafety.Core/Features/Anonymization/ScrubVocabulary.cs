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
/// There is deliberately no locale lookup here and no default for a report filed
/// in French: the caller supplies the vocabulary, so adding a language is
/// supplying two words rather than editing the scrub. Inventing a French role
/// word is not something this code may do.
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

    /// <summary>Stands in for the reporter's name.</summary>
    public string Reporter { get; }

    /// <summary>Stands in for the pilot in command's name.</summary>
    public string Pilot { get; }
}
