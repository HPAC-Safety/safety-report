using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HpacSafety.Core.Features.Anonymization.Stages;

/// <summary>
/// The compiled patterns the regex stages use. Source-generated, so they are
/// built at compile time and carry no package reference — <c>HpacSafety.Core</c>
/// depends on nothing, and that has to stay true.
/// </summary>
/// <remarks>
/// Every pattern carries a match timeout. These run over text a member of the
/// public typed into a form, and a pattern that can be made to backtrack for
/// minutes is a denial of service with a friendly face.
/// </remarks>
internal static partial class ScrubPatterns
{
    private const int TimeoutMilliseconds = 250;

    private static readonly Dictionary<char, string> Equivalents = BuildEquivalents();

    /// <summary>An email address.</summary>
    [GeneratedRegex(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,}",
        RegexOptions.CultureInvariant,
        TimeoutMilliseconds)]
    internal static partial Regex Email { get; }

    /// <summary>
    /// A URL, with or without a scheme. The second alternative catches the bare
    /// host form people actually type — <c>club.example.ca/tracks</c> — which a
    /// scheme-only pattern would miss entirely.
    /// </summary>
    [GeneratedRegex(
        @"(?:https?://|www\.)[^\s<>()\[\]]+|(?<![@\w.])(?:[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?\.)+[a-z]{2,24}(?![\w.])(?:/[^\s<>()\[\]]*)?",
        RegexOptions.CultureInvariant,
        TimeoutMilliseconds)]
    internal static partial Regex Url { get; }

    /// <summary>
    /// A North American phone number in the formats reporters actually write:
    /// dashed, dotted, spaced, bracketed, bare, and with a country code. The
    /// separator class includes the typographic dashes a word processor
    /// substitutes for a hyphen on the way through a copy and paste.
    /// </summary>
    [GeneratedRegex(
        @"(?<!\d)(?:\+?1[\s.\-‐-―]?)?(?:\(\d{3}\)[\s.\-‐-―]?|\d{3}[\s.\-‐-―]?)\d{3}[\s.\-‐-―]?\d{4}(?!\d)",
        RegexOptions.CultureInvariant,
        TimeoutMilliseconds)]
    internal static partial Regex Phone { get; }

    /// <summary>
    /// A membership identifier written out in a narrative. It is keyed on the
    /// word rather than on a digit shape on purpose: HPAC has not published a
    /// member-number format, and a bare run of digits in a flying report is far
    /// more likely to be an altitude than an identifier. Stripping every number
    /// would take the safety lesson with it.
    /// </summary>
    [GeneratedRegex(
        @"\b(?:hpac|member(?:ship)?|membre|adh[ée]rent)\b(?:\s*(?:member(?:ship)?|membre|number|num[ée]ro|no\.?|n[o°]\.?|#|:))*\s*#?\s*\d{3,9}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeoutMilliseconds)]
    internal static partial Regex MemberNumber { get; }

    /// <summary>
    /// Builds a whole-token matcher for a name or a place taken from a
    /// structured answer.
    /// </summary>
    /// <remarks>
    /// Matching is <b>accent-insensitive as well as case-insensitive</b>. A
    /// reporter who types "Renée" into the name field and "Renee" three
    /// paragraphs down has not stopped being identifiable, and in a bilingual
    /// system that spelling drift is the norm rather than the exception. Every
    /// letter becomes the class of every letter sharing its unaccented base, so
    /// the field spelling and the narrative spelling match in both directions.
    /// </remarks>
    internal static Regex Token(string token) => new(
        $@"(?<!\w){Fold(token)}(?!\w)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(TimeoutMilliseconds));

    private static string Fold(string token)
    {
        var pattern = new StringBuilder(token.Length * 8);

        foreach (var character in token)
        {
            var equivalents = Equivalents.GetValueOrDefault(BaseLetter(character));

            if (equivalents is null)
            {
                pattern.Append(Regex.Escape(character.ToString()));
            }
            else
            {
                pattern.Append('[').Append(equivalents).Append(']');
            }
        }

        return pattern.ToString();
    }

    /// <summary>The unaccented letter a character decomposes to, or the character itself.</summary>
    private static char BaseLetter(char character)
    {
        var decomposed = char.ToLowerInvariant(character)
            .ToString()
            .Normalize(NormalizationForm.FormD);

        foreach (var part in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(part) != UnicodeCategory.NonSpacingMark)
            {
                return part;
            }
        }

        return character;
    }

    /// <summary>
    /// Groups the Latin letters by the unaccented letter they decompose to, so
    /// "e" also matches "é", "è", "ê", and "ë". Built once from Unicode itself
    /// rather than from a hand-written table that would be wrong the first time
    /// somebody's name needed a letter nobody thought of.
    /// </summary>
    private static Dictionary<char, string> BuildEquivalents()
    {
        var groups = new Dictionary<char, SortedSet<char>>();

        for (var character = 'A'; character <= 'ɏ'; character++)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            var root = BaseLetter(lower);

            if (root is < 'a' or > 'z')
            {
                continue;
            }

            if (!groups.TryGetValue(root, out var equivalents))
            {
                equivalents = [];
                groups[root] = equivalents;
            }

            equivalents.Add(lower);
        }

        return groups.ToDictionary(group => group.Key, group => string.Concat(group.Value));
    }
}
