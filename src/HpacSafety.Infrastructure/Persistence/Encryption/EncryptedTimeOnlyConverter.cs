using System.Globalization;

using HpacSafety.Core.SharedKernel;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HpacSafety.Infrastructure.Persistence.Encryption;

/// <summary>
/// Encrypts a local wall-clock time on the way out and decrypts it on the way
/// back, so PostgreSQL never holds the precise time an occurrence happened.
/// </summary>
/// <remarks>
/// <para>
/// The exact time is Restricted, for the same reason the exact date is never
/// published: a province, a day, an aircraft type, an injury severity, and a
/// time of day together identify one person in a small flying community.
/// Adding the minute to that makes it certain. The coarse
/// <c>TimeOfDay</c> bucket lives in its own column, in the clear, and is the
/// only one anything downstream reads.
/// </para>
/// <para>
/// Stored as encrypted text rather than as <c>time</c>, because a Restricted
/// field is never stored in the clear. The cost is that the precise time cannot
/// be range-queried in SQL — nothing does, because analysis reads the bucket.
/// See ADR-0019.
/// </para>
/// </remarks>
public sealed class EncryptedTimeOnlyConverter : ValueConverter<TimeOnly, string>
{
    /// <summary>Round-trips a <see cref="TimeOnly"/> exactly, to the tick.</summary>
    private const string Round = "O";

    private EncryptedTimeOnlyConverter(IFieldCipher cipher)
        : base(
            time => cipher.Encrypt(time.ToString(Round, CultureInfo.InvariantCulture)),
            stored => TimeOnly.ParseExact(cipher.Decrypt(stored), Round, CultureInfo.InvariantCulture))
    {
    }

    /// <summary>Builds the converter over the cipher holding the key in use.</summary>
    /// <param name="cipher">The cipher. Never null.</param>
    public static EncryptedTimeOnlyConverter For(IFieldCipher cipher)
    {
        ArgumentNullException.ThrowIfNull(cipher);
        return new EncryptedTimeOnlyConverter(cipher);
    }
}
