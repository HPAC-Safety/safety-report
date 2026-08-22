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
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeoutMilliseconds)]
    internal static partial Regex Url { get; }

    /// <summary>
    /// A phone number in the formats reporters actually write: dashed, dotted,
    /// spaced, slashed, bracketed, bare, seven-digit local, and with a country
    /// code.
    /// </summary>
    /// <remarks>
    /// The separator class allows up to two characters and includes the
    /// typographic dashes a word processor substitutes for a hyphen on the way
    /// through a copy and paste — a number that survived because iOS turned the
    /// hyphen into a non-breaking one has still been published. The second
    /// alternative is the international form, anchored on a literal <c>+</c>:
    /// loose enough for any country's grouping, and a leading <c>+</c> in an
    /// accident narrative is a phone number essentially every time.
    /// </remarks>
    [GeneratedRegex(
        @"(?<!\d)(?:\+?1[\s.\-/‐-―]{0,2})?(?:\(\d{3}\)[\s.\-/‐-―]{0,2}|\d{3}[\s.\-/‐-―]{0,2})?\d{3}[\s.\-/‐-―]{0,2}\d{4}(?!\d)|\+\d[\d\s.\-/()‐-―]{6,20}\d(?!\d)",
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
    /// <remarks>
    /// The filler list matters more than it looks. People write "my HPAC number
    /// is 48213" and "HPAC ID 48213", not the tidy "HPAC #48213" a pattern gets
    /// written against, and a keyword group that cannot step over "is" or "my"
    /// misses the phrasing this rule exists for. The list is closed rather than
    /// "any word", so "another club member landed at 1500 feet" keeps its
    /// altitude.
    /// </remarks>
    [GeneratedRegex(
        @"\b(?:hpac|member(?:ship)?|membre|adh[ée]rent)\b(?:\s*(?:member(?:ship)?|membre|number|num[ée]ro|no\.?|n[o°]\.?|id|is|was|are|my|the|de|du|est|#|:))*\s*#?\s*\d{3,9}\b",
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
    /// <para>
    /// Three further forms of the same word, each of which was reaching the
    /// summarizer intact:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     A <b>trailing "s"</b>. "Whitlock's" was caught because an apostrophe
    ///     is not a word character; "the Whitlocks" was not, and it names the
    ///     same family. A name is never a prefix of a longer word here — the
    ///     lookahead still rejects "Marconi" for "Marc" — so the optional "s"
    ///     costs nothing.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Either Unicode normalization form.</b> A browser may submit "é" as
    ///     one code point or as "e" plus a combining acute, and the two are not
    ///     equal byte for byte. The token is composed before it is folded and
    ///     every letter may be followed by combining marks, so the two forms
    ///     match each other in both directions.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Whitespace that moved.</b> A run of spaces in the answer becomes
    ///     <c>\s*</c>, so a field reading "Halcyon 3" also finds "Halcyon3".
    ///   </description></item>
    /// </list>
    /// </remarks>
    internal static Regex Token(string token) => new(
        $@"(?<!\w){Fold(token)}s?(?!\w)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(TimeoutMilliseconds));

    private static string Fold(string token)
    {
        var normalized = token.Normalize(NormalizationForm.FormC);
        var pattern = new StringBuilder(normalized.Length * 12);
        var index = 0;

        while (index < normalized.Length)
        {
            if (char.IsWhiteSpace(normalized[index]))
            {
                while (index < normalized.Length && char.IsWhiteSpace(normalized[index]))
                {
                    index++;
                }

                pattern.Append(@"\s*");
                continue;
            }

            var equivalents = Equivalents.GetValueOrDefault(BaseLetter(normalized[index]));

            if (equivalents is null)
            {
                pattern.Append(Regex.Escape(normalized[index].ToString()));
            }
            else
            {
                // Trailing combining marks, so a decomposed narrative matches a
                // composed answer and the other way round.
                pattern.Append('[').Append(equivalents).Append(@"]\p{Mn}*");
            }

            index++;
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
