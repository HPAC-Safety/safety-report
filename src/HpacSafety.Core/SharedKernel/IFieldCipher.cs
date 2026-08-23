namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Encrypts and decrypts one report value on its way to and from storage. The
/// entire answer-value column is encrypted regardless of question privacy, so
/// the database never holds plaintext narrative or contact details.
/// </summary>
/// <remarks>
/// <para>
/// The port is declared here because <c>Core</c> owns the rule that report
/// values are encrypted at rest (see <c>docs/data-handling.md</c>). The algorithm,
/// the key, and the wiring into EF Core are infrastructure and live in
/// <c>HpacSafety.Infrastructure</c>. See ADR-0019.
/// </para>
/// <para>
/// An implementation is authenticated: text that was tampered with, or that was
/// written under a different key, fails to decrypt rather than returning
/// plausible-looking rubbish.
/// </para>
/// </remarks>
public interface IFieldCipher
{
    /// <summary>
    /// A short, non-secret identifier for the key in use. It names the key, and
    /// never reveals it, so that two ciphers can be told apart — by a cache, by
    /// a log line, or by a future rotation — without the key being handled.
    /// </summary>
    string KeyId { get; }

    /// <summary>Encrypts <paramref name="plaintext"/> for storage.</summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/>.
    /// </summary>
    /// <exception cref="FieldDecryptionException">
    /// The text was not produced by this cipher, or was produced under a
    /// different key, or has been altered since it was written.
    /// </exception>
    string Decrypt(string ciphertext);
}
