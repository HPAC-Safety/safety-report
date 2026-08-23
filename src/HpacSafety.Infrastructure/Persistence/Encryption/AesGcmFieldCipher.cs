using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using HpacSafety.Core.SharedKernel;

using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Persistence.Encryption;

/// <summary>
/// AES-256-GCM applied in the application, so PostgreSQL only ever holds
/// ciphertext for a Restricted field. See ADR-0019.
/// </summary>
/// <remarks>
/// <para>
/// GCM is authenticated: a value written under a different key, or altered in
/// the database, fails to decrypt rather than decrypting into something
/// plausible. That matters more here than performance — a silently mangled
/// contact detail is a wrong phone number attached to a real crash.
/// </para>
/// <para>
/// The stored form is <c>v1.</c> followed by base64 of
/// <c>nonce ‖ tag ‖ ciphertext</c>. The version prefix is what makes a future
/// key rotation or algorithm change readable rather than a guess.
/// </para>
/// </remarks>
public sealed class AesGcmFieldCipher : IFieldCipher
{
    /// <summary>The stored-format marker. Anything else is not ours.</summary>
    private const string Version = "v1.";

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key;

    /// <summary>Builds the cipher from the configured key.</summary>
    /// <exception cref="InvalidOperationException">
    /// The key is missing, is not base64, or is not 256 bits. The message names
    /// the setting and never the value.
    /// </exception>
    public AesGcmFieldCipher(FieldEncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            throw new InvalidOperationException(
                $"No field-encryption key is configured. Set '{FieldEncryptionOptions.SectionName}:Key' to a base64 256-bit key.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(options.Key);
        }
        catch (FormatException cause)
        {
            throw new InvalidOperationException(
                $"'{FieldEncryptionOptions.SectionName}:Key' is not valid base64.", cause);
        }

        if (key.Length != KeySize)
        {
            throw new InvalidOperationException(
                $"'{FieldEncryptionOptions.SectionName}:Key' must decode to {KeySize} bytes, not {key.Length}.");
        }

        _key = key;
        KeyId = KeyIdentifierFor(key);
    }

    /// <summary>Builds the cipher from options resolved by the container.</summary>
    public AesGcmFieldCipher(IOptions<FieldEncryptionOptions> options)
        : this((options ?? throw new ArgumentNullException(nameof(options))).Value)
    {
    }

    /// <inheritdoc />
    public string KeyId { get; }

    /// <inheritdoc />
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var envelope = new byte[NonceSize + TagSize + bytes.Length];
        var nonce = envelope.AsSpan(0, NonceSize);
        var tag = envelope.AsSpan(NonceSize, TagSize);
        var ciphertext = envelope.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, bytes, ciphertext, tag);

        return Version + Convert.ToBase64String(envelope);
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (!ciphertext.StartsWith(Version, StringComparison.Ordinal))
        {
            throw new FieldDecryptionException(
                "An encrypted field is not in the expected stored format. It was not written by this cipher.");
        }

        byte[] envelope;
        try
        {
            envelope = Convert.FromBase64String(ciphertext[Version.Length..]);
        }
        catch (FormatException cause)
        {
            throw new FieldDecryptionException("An encrypted field is not valid base64.", cause);
        }

        if (envelope.Length < NonceSize + TagSize)
        {
            throw new FieldDecryptionException("An encrypted field is too short to be a complete value.");
        }

        var plaintext = new byte[envelope.Length - NonceSize - TagSize];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(
                envelope.AsSpan(0, NonceSize),
                envelope.AsSpan(NonceSize + TagSize),
                envelope.AsSpan(NonceSize, TagSize),
                plaintext);
        }
        catch (CryptographicException cause)
        {
            throw new FieldDecryptionException(
                "An encrypted field could not be decrypted. It was written under a different key, or has been altered.",
                cause);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Four bytes of SHA-256 over the key. Enough to tell two keys apart —
    /// which is all a model cache or a log line needs — and nowhere near enough
    /// to recover one.
    /// </summary>
    private static string KeyIdentifierFor(byte[] key)
    {
        var digest = SHA256.HashData(key);
        return string.Create(
            8,
            digest,
            static (span, source) =>
            {
                for (var i = 0; i < 4; i++)
                {
                    source[i].TryFormat(span[(i * 2)..], out _, "x2", CultureInfo.InvariantCulture);
                }
            });
    }
}
