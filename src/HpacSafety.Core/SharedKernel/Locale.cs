namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// One of the two official locales, <c>en-CA</c> and <c>fr-CA</c>. A value
/// object rather than a string so that "the locale a report was written in" and
/// "the locale a summary is in" cannot be handed a typo.
/// </summary>
public readonly record struct Locale
{
    /// <summary>Canadian English.</summary>
    public static readonly Locale EnCa = new("en-CA");

    /// <summary>Canadian French.</summary>
    public static readonly Locale FrCa = new("fr-CA");

    private Locale(string code) => Code = code;

    /// <summary>The invariant code, as stored.</summary>
    public string Code { get; }

    /// <summary>
    /// The other official locale. Every question and every summary exists as a
    /// pair, so "the other one" is a first-class idea here.
    /// </summary>
    public Locale Counterpart => Code == EnCa.Code ? FrCa : EnCa;

    /// <summary>Every locale this system supports.</summary>
    public static IReadOnlyList<Locale> All { get; } = [EnCa, FrCa];

    /// <summary>Parses an invariant code, throwing when it is not supported.</summary>
    public static Locale Parse(string code) =>
        TryParse(code, out var locale)
            ? locale
            : throw new DomainRuleViolationException($"'{code}' is not a supported locale.");

    /// <summary>Parses an invariant code without throwing.</summary>
    public static bool TryParse(string? code, out Locale locale)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                locale = candidate;
                return true;
            }
        }

        locale = default;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Code;
}
