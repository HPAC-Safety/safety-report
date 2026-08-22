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
    // Two, not three. Ng, Wu, Li, Vo and Ha are common surnames, and gating
    // the parts of an answer at three characters dropped them on the floor
    // entirely — "Sarah Ng" built matchers for "Sarah Ng" and "Sarah" and left
    // "Ng spiralled in" untouched.
    private const int ShortestNamePart = 2;

    // Four for a part of a place or an aircraft, because two- and
    // three-letter parts of those are overwhelmingly ordinary words — "Air",
    // "Sky" — and deleting "air" from a flying report deletes the report.
    private const int ShortestPlacePart = 4;

    // The whole answer is matched almost regardless of length, because a short
    // whole answer is a short surname or a short brand: "Ng", "Cox", "UP". One
    // character is the exception — that is an initial, it identifies nobody,
    // and matching it would turn every standalone "a" into "the pilot".
    private const int ShortestWholeAnswer = 2;

    // Applied to PARTS only, and only when the answer wrote them in lower case.
    // French name particles are conventionally lower case and surnames are
    // capitalised, which is the signal that separates "Marc de la Roche" — where
    // matching "de" would eat half of every French narrative — from "Thanh Le",
    // where "Le" is the surname and must be matched. A whole answer of "Le" is
    // a surname and is matched regardless.
    private static readonly HashSet<string> NameParticles = new(StringComparer.Ordinal)
    {
        "de", "du", "des", "la", "le", "les", "van", "von", "der", "den", "di",
        "da", "dos", "das", "el", "al", "bin", "ibn", "ter", "ten", "of", "the",
    };

    private static readonly char[] TokenSeparators =
        [' ', '\t', '\n', '\r', '-', '\u2010', '\u2011', '\u2012', '\u2013', '\u2014', '\'', '\u2019'];

    protected override void Handle(ScrubDocument document)
    {
        Harvest(document);

        var surviving = new List<ScrubField>(document.Fields.Count);

        foreach (var field in document.Fields)
        {
            var kept = Rewrite(field, document);

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
    private static ScrubField? Rewrite(ScrubField field, ScrubDocument document) => field.Kind switch
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
        ScrubFieldKind.Location => document.Province is Province.NotAnswered
            ? null
            : field with { Value = EnumCode.Of(document.Province) },

        // The clock never survives: the field's own value is replaced outright,
        // whatever it said. "Unknown" is kept because the form asked and the
        // reporter did not answer, which is a fact worth publishing;
        // "NotAnswered" means the form never asked, so there is nothing to say
        // and the field goes. Neither is midnight — midnight is a real answer
        // and buckets as morning, upstream of here.
        ScrubFieldKind.OccurrenceTime => document.TimeOfDay is TimeOfDay.NotAnswered
            ? null
            : field with { Value = EnumCode.Of(document.TimeOfDay) },

        // Month and year, the same rule the policy has always stated for dates.
        ScrubFieldKind.OccurrenceDate => OccurrenceNarrowing.MonthAndYear(document.OccurredOn) is { } monthAndYear
            ? field with { Value = monthAndYear }
            : null,

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
                    Collect(names, field.Value, document.Vocabulary.Reporter, ShortestNamePart, overwrite: false);
                    break;

                // The pilot wins a tie. When one name is in both answers the
                // reporter is the pilot, and a report about a crash reads better
                // — and no less safely — as "the pilot".
                case ScrubFieldKind.PilotName:
                    Collect(names, field.Value, document.Vocabulary.Pilot, ShortestNamePart, overwrite: true);
                    break;

                case ScrubFieldKind.Location:
                case ScrubFieldKind.AircraftIdentity:
                    foreach (var token in Tokens(field.Value, ShortestPlacePart))
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
                // Unclassified belongs here, not in the ignored group. Dropping
                // the field is only half of fail-closed: ADR-0027 justifies the
                // zero value with a "next of kin" question the role mapping
                // missed, and that name turns up in the narrative too.
                case ScrubFieldKind.Unclassified:
                case ScrubFieldKind.ContactDetail:
                case ScrubFieldKind.MemberIdentifier:
                    var contact = field.Value.Trim();

                    if (contact.Length >= ShortestNamePart)
                    {
                        terms.Add(contact);
                    }

                    break;

                case ScrubFieldKind.FreeText:
                case ScrubFieldKind.Narrative:
                case ScrubFieldKind.OccurrenceDate:
                case ScrubFieldKind.OccurrenceTime:
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

        if (whole.Length >= ShortestWholeAnswer)
        {
            yield return whole;
        }

        foreach (var part in whole.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = part.Trim(',', '.', ';', ':', '(', ')', '"');

            if (word.Length < shortest
                || string.Equals(word, whole, StringComparison.OrdinalIgnoreCase)
                || IsParticle(word, shortest))
            {
                continue;
            }

            yield return word;
        }
    }

    /// <summary>
    /// A lower-case name particle inside a longer name. Skipped as a part and
    /// matched as a whole answer, which is the difference between "Marc de la
    /// Roche" and a pilot whose surname is "Le".
    /// </summary>
    private static bool IsParticle(string word, int shortest) =>
        shortest == ShortestNamePart
        && char.IsLower(word[0])
        && NameParticles.Contains(word.ToLowerInvariant());
}
