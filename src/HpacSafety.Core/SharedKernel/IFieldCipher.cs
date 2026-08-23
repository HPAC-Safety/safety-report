namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Encrypts and decrypts a single Restricted field on its way to and from
/// storage. Contact details — reporter and pilot name, phone, email, member
/// number — and the raw narrative are encrypted by the application, so the
/// database never holds their plaintext and a database backup is not a copy of
/// everyone's contact list.
/// </summary>
/// <remarks>
/// <para>
/// The port is declared here because <c>Core</c> owns the rule that Restricted
/// data is encrypted at rest (see <c>docs/data-handling.md</c>). The algorithm,
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
