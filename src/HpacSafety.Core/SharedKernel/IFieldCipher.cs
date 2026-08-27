namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Legacy application-encryption port retained only for the current-main
/// migration path. Issue #79 removes it in favor of managed encryption at rest
/// and TLS.
/// </summary>
/// <remarks>
/// <para>
/// This describes superseded current behavior, not a target domain rule. See
/// the superseded status on ADR-0019 and <c>../../../docs/data-and-persistence.md</c>.
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
