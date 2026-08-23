using HpacSafety.Core.SharedKernel;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HpacSafety.Infrastructure.Persistence.Encryption;

/// <summary>
/// Legacy converter used by the current-main schema. Issue #79 removes it when
/// migrating to managed encryption at rest and TLS.
/// </summary>
/// <remarks>
/// A converter runs per value, not per row, so a column is either encrypted for
/// every row or for none. That is why the whole of <c>report_answers.value</c>
/// is encrypted rather than only rows whose question is private — see ADR-0019
/// and superseded ADR-0019. Privacy controls model input, not storage.
/// <para>
/// The cost is real and deliberate: an encrypted column cannot be searched,
/// sorted, or indexed by value in the database. A null stays null — EF Core does
/// not run a converter over one — so an unanswered question is still visibly
/// unanswered.
/// </para>
/// </remarks>
public sealed class EncryptedStringConverter : ValueConverter<string?, string>
{
    private EncryptedStringConverter(IFieldCipher cipher)
        : base(plaintext => cipher.Encrypt(plaintext!), stored => cipher.Decrypt(stored))
    {
    }

    /// <summary>Builds the converter over the cipher holding the key in use.</summary>
    /// <param name="cipher">The cipher. Never null.</param>
    public static EncryptedStringConverter For(IFieldCipher cipher)
    {
        ArgumentNullException.ThrowIfNull(cipher);
        return new EncryptedStringConverter(cipher);
    }
}
