using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Anonymization.Stages;

/// <summary>
/// First link in the chain. Reads the structured answers, then drops the ones
/// that identify somebody and generalizes the one that identifies somewhere.
/// </summary>
/// <remarks>
/// It runs first because those answers are also the best token list there is for
/// the free-text stages that follow: the reporter told us the pilot's name and
/// the launch's name, so the scrub does not have to guess at either when the
/// same words turn up in the narrative.
/// </remarks>
internal sealed class StructuredFieldStage : ScrubStage
{
    // Three, not two, for a name. A two-letter part would take the French name
    // particles — "de", "la", "du", "le" — out of every French narrative the
    // system ever scrubs, which costs far more than it buys: the full name and
    // the surname still match, and those are what identify somebody.
    private const int ShortestNameToken = 3;

    // Four for a place or an aircraft, because three-letter parts of those are
    // overwhelmingly ordinary words — "Air", "Sky", "Mont" — and deleting "air"
    // from a flying report deletes the report. The whole multi-word term is
    // always matched regardless of length.
    private const int ShortestPlaceToken = 4;

    private static readonly char[] TokenSeparators =
        [' ', '\t', '\n', '\r', '-', '\u2010', '\u2011', '\u2012', '\u2013', '\u2014', '\'', '\u2019'];

    protected override void Handle(ScrubDocument document)
    {
        Harvest(document);

        var surviving = new List<ScrubField>(document.Fields.Count);

        foreach (var field in document.Fields)
        {
            var kept = Rewrite(field, document.Province);

            if (kept is not null && !string.IsNullOrWhiteSpace(kept.Value))
            {
                surviving.Add(kept);
            }
        }

        document.Fields.Clear();
        document.Fields.AddRange(surviving);
    }

    /// <summary>
    /// Which fields survive, and in what shape. Names, contact details, member
    /// identifiers, and aircraft identity are dropped outright — there is no
    /// generalization of a phone number that is worth publishing.
    /// </summary>
    private static ScrubField? Rewrite(ScrubField field, Province province) => field.Kind switch
    {
        ScrubFieldKind.ReporterName
            or ScrubFieldKind.PilotName
            or ScrubFieldKind.ContactDetail
            or ScrubFieldKind.MemberIdentifier
            or ScrubFieldKind.AircraftIdentity
            or ScrubFieldKind.Unclassified => null,

        // The region is the province and nothing finer. With no province
        // answered there is nothing to generalize to, and a location nobody can
        // place is dropped rather than guessed at.
        ScrubFieldKind.Location => province is Province.NotAnswered
            ? null
            : field with { Value = EnumCode.Of(province) },

        _ => field,
    };

    private static void Harvest(ScrubDocument document)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in document.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Value))
            {
                continue;
            }

            switch (field.Kind)
            {
                // The role word comes from the field the name was given in, so
                // a reporter who is not the pilot reads as "the reporter".
                case ScrubFieldKind.ReporterName:
                    Collect(names, field.Value, document.Vocabulary.Reporter, ShortestNameToken, overwrite: false);
                    break;

                // The pilot wins a tie. When one name is in both answers the
                // reporter is the pilot, and a report about a crash reads better
                // — and no less safely — as "the pilot".
                case ScrubFieldKind.PilotName:
                    Collect(names, field.Value, document.Vocabulary.Pilot, ShortestNameToken, overwrite: true);
                    break;

                case ScrubFieldKind.Location:
                case ScrubFieldKind.AircraftIdentity:
                    foreach (var token in Tokens(field.Value, ShortestPlaceToken))
                    {
                        terms.Add(token);
                    }

                    break;

                // Whole value only, and deliberately. A contact detail the
                // reporter also typed into the narrative — "there's video on my
                // page, @sarahflies", "I gave them my number, 48213" — is
                // something no pattern catches, and we were handed the exact
                // string. Splitting it into words would be a different matter:
                // a street address would harvest "West" and delete the wind
                // direction from every sentence that mentions it.
                case ScrubFieldKind.ContactDetail:
                case ScrubFieldKind.MemberIdentifier:
                    var contact = field.Value.Trim();

                    if (contact.Length >= ShortestNameToken)
                    {
                        terms.Add(contact);
                    }

                    break;

                case ScrubFieldKind.Unclassified:
                case ScrubFieldKind.FreeText:
                case ScrubFieldKind.Narrative:
                default:
                    break;
            }
        }

        // Longest first, so "Mount Ferndale" is taken as a place before "Mount"
        // is taken on its own and leaves a stray "Ferndale" behind.
        document.Names.AddRange(names
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => new NameSubstitution(pair.Key, pair.Value)));

        document.Terms.AddRange(terms.OrderByDescending(term => term.Length));
    }

    private static void Collect(
        Dictionary<string, string> names,
        string value,
        string replacement,
        int shortest,
        bool overwrite)
    {
        foreach (var token in Tokens(value, shortest))
        {
            if (overwrite || !names.ContainsKey(token))
            {
                names[token] = replacement;
            }
        }
    }

    /// <summary>
    /// The whole answer, plus each part of it. People write "Sarah Whitlock" in
    /// one field and "Sarah" three paragraphs later, and they write
    /// "Sarah-Jane Whitlock" and then "Sarah" — so hyphens and apostrophes split
    /// as well as spaces, or half of a double-barrelled given name walks
    /// straight through.
    /// </summary>
    private static IEnumerable<string> Tokens(string value, int shortest)
    {
        var whole = value.Trim();

        // The minimum applies to the whole answer too, not only to its parts.
        // A reporter who types an initial into the name field would otherwise
        // hand us the token "A", and every standalone "a" in the narrative
        // would become "the pilot". The French case is worse: a name field
        // reading "Le" would eat "Le vent a tourné".
        if (whole.Length >= shortest)
        {
            yield return whole;
        }

        foreach (var part in whole.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = part.Trim(',', '.', ';', ':', '(', ')', '"');

            if (word.Length >= shortest && !string.Equals(word, whole, StringComparison.OrdinalIgnoreCase))
            {
                yield return word;
            }
        }
    }
}
