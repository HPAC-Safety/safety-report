using System.Security.Cryptography;
using System.Text;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
/// Stable identifiers for seeded rows, derived from a name rather than drawn at
/// random.
/// </summary>
/// <remarks>
/// A migration must produce the same rows on every database it is applied to,
/// and the same rows again when an idempotent SQL script is generated on one
/// machine and applied on another. <see cref="Guid.NewGuid"/> cannot do that.
/// This is RFC 4122 name-based generation with SHA-256 in place of SHA-1 — the
/// value is a stable function of the name, so a seeded question keeps its
/// identifier for the life of the system. See ADR-0020.
/// </remarks>
public static class SeedIds
{
    /// <summary>
    /// The namespace every seeded name hangs off. Changing it re-identifies
    /// every seeded row, which is a data migration, not an edit.
    /// </summary>
    private static readonly byte[] Namespace =
        Guid.Parse("6f2b7d0e-4a1c-4f7e-9c2a-1d5b3e8a7c40").ToByteArray(bigEndian: true);

    /// <summary>Derives the identifier for a seeded row from its name.</summary>
    /// <param name="name">
    /// A name unique within the seed — for example <c>question:province</c> or
    /// <c>option:province:alberta:fr-CA</c>.
    /// </param>
    public static Guid For(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var bytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[Namespace.Length + bytes.Length];
        Namespace.CopyTo(input, 0);
        bytes.CopyTo(input, Namespace.Length);

        var digest = SHA256.HashData(input).AsSpan(0, 16).ToArray();

        // Version 8, "custom", which is what a SHA-256 derivation honestly is.
        digest[6] = (byte)((digest[6] & 0x0F) | 0x80);
        digest[8] = (byte)((digest[8] & 0x3F) | 0x80);

        return new Guid(digest, bigEndian: true);
    }
}
