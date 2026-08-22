using System.Text;

using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// A question's stable identity in text: lowercase, underscore-separated,
/// invariant. Keys are generated once and never change, because answers and
/// exports refer to them.
/// </summary>
public static class QuestionKey
{
    /// <summary>The key of the one question this system will not run without.</summary>
    public const string ConsentPublish = "consent_publish";

    /// <summary>
    /// Normalizes a candidate key. Rejects an empty result rather than inventing
    /// one, because a key nobody chose is a key nobody can find again.
    /// </summary>
    public static string Normalize(string candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var builder = new StringBuilder(candidate.Length);
        foreach (var character in candidate.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        var key = builder.ToString().Trim('_');

        return key.Length == 0
            ? throw new DomainRuleViolationException($"'{candidate}' does not reduce to a usable question key.")
            : key;
    }
}
