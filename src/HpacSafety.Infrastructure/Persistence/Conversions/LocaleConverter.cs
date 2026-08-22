using HpacSafety.Core.SharedKernel;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HpacSafety.Infrastructure.Persistence.Conversions;

/// <summary>
/// Stores a <see cref="Locale"/> as its IETF code — <c>en-CA</c>, <c>fr-CA</c>.
/// </summary>
/// <remarks>
/// The code is what a reviewer reads in a row and what an export carries, and
/// <see cref="Locale.Parse"/> rejects anything the domain does not support, so
/// a hand-edited row cannot smuggle in a third language.
/// </remarks>
public sealed class LocaleConverter : ValueConverter<Locale, string>
{
    /// <summary>Creates the converter.</summary>
    public LocaleConverter()
        : base(locale => locale.Code, code => Locale.Parse(code))
    {
    }
}
