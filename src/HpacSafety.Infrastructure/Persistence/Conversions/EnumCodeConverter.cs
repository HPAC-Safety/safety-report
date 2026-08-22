using HpacSafety.Core.SharedKernel;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HpacSafety.Infrastructure.Persistence.Conversions;

/// <summary>
/// Stores a domain enum as its invariant code — <c>high_en_b</c>, not <c>3</c>.
/// </summary>
/// <remarks>
/// Domain values are stored as invariant codes and localized only at the edge,
/// so a row is readable without the enum beside it and a reordered enum cannot
/// silently reinterpret history. See ADR-0019.
/// </remarks>
/// <typeparam name="TEnum">The domain enum.</typeparam>
public sealed class EnumCodeConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    /// <summary>Creates the converter.</summary>
    public EnumCodeConverter()
        : base(value => EnumCode.Of(value), code => Parse(code))
    {
    }

    private static TEnum Parse(string code) =>
        EnumCode.TryParse<TEnum>(code, out var value)
            ? value
            : throw new DomainRuleViolationException(
                $"'{code}' is not a {typeof(TEnum).Name}. A stored code that no longer names a domain value needs a data migration, not a guess.");
}
