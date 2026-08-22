using System.Text;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Normalizes a reporter's free-text certification answer against the published
/// vocabulary. Deterministic, offline, and total: every input produces either a
/// vocabulary class or <see cref="AircraftClass.NotDetermined"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole implementation of <see cref="IAircraftClassifier"/> and it
/// is deliberately dull. It reads the reporter's answer and the aircraft type
/// they chose, and nothing else — no make, no model, no narrative, no pilot
/// rating, no model-to-class table, no language model. Where the answer does
/// not resolve, the result is <see cref="AircraftClass.NotDetermined"/>, which a
/// reviewer may correct by hand.
/// </para>
/// <para>
/// Two shapes it refuses on purpose, because guessing them would publish a
/// confident wrong fact about a real accident:
/// </para>
/// <list type="bullet">
///   <item><description><c>"EN B"</c> with no band. There is no plain EN-B in
///   the vocabulary, and the low/high split is the part that carries the safety
///   signal.</description></item>
///   <item><description>Answers in the LTF/DHV scheme. HPAC has not decided how
///   those bands map onto EN bands, so nothing here decides it either. See
///   ADR-0029.</description></item>
/// </list>
/// </remarks>
public sealed class VocabularyAircraftClassifier : IAircraftClassifier
{
    private static readonly string[] NonAnswers =
    [
        "n a", "na", "nil", "none", "no", "n", "unknown", "unsure", "not sure",
        "not applicable", "no answer", "no idea", "dont know", "don t know",
    ];

    /// <inheritdoc />
    public AircraftClassification Classify(string? certificationAnswer, Discipline discipline)
    {
        var text = Normalize(certificationAnswer);
        var markers = ReadMarkers(text, discipline);
        var isHangGlider = IsHangGlider(text, discipline);

        var aircraftClass = IsNonAnswer(text)
            ? AircraftClass.NotDetermined
            : isHangGlider ? ReadStructuralClass(text) : ReadCertificationClass(text);

        if (aircraftClass == AircraftClass.NotDetermined)
        {
            aircraftClass = StandInFor(markers, isHangGlider, discipline);
        }

        return new AircraftClassification(aircraftClass, markers);
    }

    /// <summary>
    /// Lower-cases and reduces every run of punctuation to a single space, so
    /// that "EN-B (low)", "en_b, low" and "  LOW   B " are the same answer. The
    /// result is padded with spaces at both ends so a phrase can be matched as a
    /// whole word without splitting.
    /// </summary>
    private static string Normalize(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return " ";
        }

        var normalized = new StringBuilder(answer.Length + 2);
        normalized.Append(' ');

        foreach (var character in answer)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
            else if (normalized[^1] != ' ')
            {
                normalized.Append(' ');
            }
        }

        if (normalized[^1] != ' ')
        {
            normalized.Append(' ');
        }

        return normalized.ToString();
    }

    private static bool IsNonAnswer(string text) =>
        Array.Exists(NonAnswers, nonAnswer => text.Equals($" {nonAnswer} ", StringComparison.Ordinal));

    private static bool Has(string text, params string[] phrases) =>
        Array.Exists(phrases, phrase => text.Contains($" {phrase} ", StringComparison.Ordinal));

    /// <summary>
    /// Markers come from the answer and from the aircraft type the reporter
    /// chose — both are the reporter's own words.
    /// </summary>
    private static AircraftMarker ReadMarkers(string text, Discipline discipline)
    {
        var markers = AircraftMarker.None;

        if (Has(text, "tandem", "biplace", "2 place", "two place"))
        {
            markers |= AircraftMarker.Tandem;
        }

        if (discipline == Discipline.MiniWing || Has(text, "mini wing", "miniwing", "mini"))
        {
            markers |= AircraftMarker.MiniWing;
        }

        if (discipline == Discipline.Speedflying ||
            Has(text, "speedwing", "speed wing", "speedflying", "speed flying", "speedriding", "speed riding"))
        {
            markers |= AircraftMarker.Speedwing;
        }

        return markers;
    }

    /// <summary>
    /// Hang gliders are not EN-rated, so an EN answer must never resolve for
    /// one. The discipline settles it; failing that, the structural vocabulary
    /// in the answer itself does.
    /// </summary>
    private static bool IsHangGlider(string text, Discipline discipline) =>
        discipline switch
        {
            Discipline.HangGliding => true,
            Discipline.Unknown => Has(text, "hang glider", "hang gliding", "hg", "topless", "rigid",
                "kingpost", "kingposted", "king post", "single surface", "double surface"),
            _ => false,
        };

    private static AircraftClass ReadStructuralClass(string text)
    {
        if (Has(text, "rigid"))
        {
            return AircraftClass.Rigid;
        }

        if (Has(text, "topless"))
        {
            return AircraftClass.Topless;
        }

        if (Has(text, "kingpost", "kingposted", "king post", "double surface"))
        {
            return AircraftClass.DoubleSurfaceKingposted;
        }

        return Has(text, "single surface", "single skin")
            ? AircraftClass.SingleSurface
            : AircraftClass.NotDetermined;
    }

    private static AircraftClass ReadCertificationClass(string text)
    {
        // The LTF/DHV scheme is a different scheme, not a spelling of this one.
        if (Has(text, "ltf", "dhv"))
        {
            return AircraftClass.NotDetermined;
        }

        if (Has(text, "uncertified", "un certified", "not certified", "no certification",
                "no cert", "uncert", "prototype", "proto"))
        {
            return AircraftClass.Uncertified;
        }

        if (Has(text, "ccc"))
        {
            return AircraftClass.Ccc;
        }

        var letter = ReadEnLetter(text);
        var low = Has(text, "low", "lo");
        var high = Has(text, "high", "hi");

        return letter switch
        {
            'a' => AircraftClass.EnA,
            'c' => AircraftClass.EnC,
            'd' => AircraftClass.EnD,

            // "EN-B" alone is not in the vocabulary, and an answer naming both
            // bands has not chosen one. Neither is resolved by picking a side.
            'b' when low && !high => AircraftClass.LowEnB,
            'b' when high && !low => AircraftClass.HighEnB,
            _ => AircraftClass.NotDetermined,
        };
    }

    /// <summary>
    /// The single EN letter the answer names, or <c>'\0'</c> when it names none
    /// or more than one.
    /// </summary>
    private static char ReadEnLetter(string text)
    {
        var found = '\0';

        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var letter = token switch
            {
                ['a' or 'b' or 'c' or 'd'] => token[0],
                ['e', 'n', 'a' or 'b' or 'c' or 'd'] => token[2],
                _ => '\0',
            };

            if (letter == '\0')
            {
                continue;
            }

            if (found != '\0' && found != letter)
            {
                return '\0';
            }

            found = letter;
        }

        return found;
    }

    /// <summary>
    /// With no certification class in the answer, a marker the reporter did give
    /// stands in as the class. The discipline is never invented to make that
    /// work: a tandem of an unstated discipline stays undetermined.
    /// </summary>
    private static AircraftClass StandInFor(AircraftMarker markers, bool isHangGlider, Discipline discipline)
    {
        if (markers.HasFlag(AircraftMarker.Tandem))
        {
            if (isHangGlider)
            {
                return AircraftClass.TandemHangGlider;
            }

            if (discipline != Discipline.Unknown)
            {
                return AircraftClass.TandemParaglider;
            }

            return AircraftClass.NotDetermined;
        }

        if (markers.HasFlag(AircraftMarker.MiniWing))
        {
            return AircraftClass.MiniWing;
        }

        return markers.HasFlag(AircraftMarker.Speedwing)
            ? AircraftClass.Speedwing
            : AircraftClass.NotDetermined;
    }
}
