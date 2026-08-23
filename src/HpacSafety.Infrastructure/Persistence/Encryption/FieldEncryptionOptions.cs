namespace HpacSafety.Infrastructure.Persistence.Encryption;

/// <summary>
/// Where the field-encryption key comes from. Bound from configuration section
/// <see cref="SectionName"/>.
/// </summary>
/// <remarks>
/// In development the key is a throwaway literal in
/// <c>appsettings.Development.json</c>. In production it is a Secrets Manager
/// reference resolved into configuration at start-up; it is never committed and
/// never logged. See ADR-0019.
/// </remarks>
public sealed class FieldEncryptionOptions
{
    /// <summary>The configuration section this binds from.</summary>
    public const string SectionName = "HpacSafety:FieldEncryption";

    /// <summary>
    /// The AES-256 key, base64-encoded, decoding to exactly 32 bytes.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}
