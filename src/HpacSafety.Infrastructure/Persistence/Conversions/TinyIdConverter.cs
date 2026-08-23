using HpacSafety.Core.SharedKernel;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HpacSafety.Infrastructure.Persistence.Conversions;

/// <summary>
/// Stores a <see cref="TinyId"/> as eleven characters of fixed-width text.
/// </summary>
/// <remarks>
/// Not <c>uuid</c>: an identifier here is eleven characters over a sixty-four
/// symbol alphabet, and storing it as anything else would mean two
/// representations of the same thing. <see cref="TinyId.Parse"/> rejects
/// anything that is not one, so a hand-edited row cannot introduce a shape the
/// domain does not recognise. See ADR-0034.
/// </remarks>
public sealed class TinyIdConverter : ValueConverter<TinyId, string>
{
    /// <summary>Creates the converter.</summary>
    public TinyIdConverter()
        : base(id => id.Value, stored => TinyId.Parse(stored))
    {
    }
}
