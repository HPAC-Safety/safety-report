namespace HpacSafety.Core.Values;

/// <summary>
/// Converts between an enum member and the invariant code it is stored as.
/// Domain values are stored as stable codes and localized only at the edge, so
/// <c>InjurySeverity.Serious</c> is written and read as <c>serious</c>.
/// </summary>
public static class EnumCode
{
    /// <summary>The invariant code for an enum member.</summary>
    public static string Of<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var code = new System.Text.StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                code.Append('_');
            }

            code.Append(char.ToLowerInvariant(name[i]));
        }

        return code.ToString();
    }

    /// <summary>
    /// Reads an invariant code back, ignoring separators so that <c>low_en_b</c>,
    /// <c>lowenb</c>, and <c>LowEnB</c> all resolve. Returns false rather than
    /// guessing when nothing matches.
    /// </summary>
    public static bool TryParse<TEnum>(string? code, out TEnum value) where TEnum : struct, Enum
    {
        value = default;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var wanted = Strip(code);

        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(Strip(candidate.ToString()), wanted, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        return false;
    }

    private static string Strip(string text) =>
        string.Concat(text.Where(char.IsAsciiLetterOrDigit)).ToLowerInvariant();
}
