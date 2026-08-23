using System.Security.Cryptography;
using System.Text;

using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
/// Stable identifiers for seeded rows, derived from a name rather than drawn at
/// random.
/// </summary>
/// <remarks>
/// A migration must produce the same rows on every database it is applied to,
/// and the same rows again when an idempotent SQL script is generated on one
/// machine and applied on another. <see cref="Guid.NewGuid"/> cannot do that.
/// The identifier is derived from a SHA-256 over a fixed namespace and the
/// name, so it is a stable function of the name and a seeded question keeps its
/// identifier for the life of the system. A hash is already unpredictable, so
/// the leading bytes are encoded straight into the eleven-character alphabet —
/// the result is an ordinary <see cref="TinyId"/>, indistinguishable from a
/// minted one, and the seed stays idempotent. See ADR-0020 and ADR-0034.
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
    /// A name unique within the seed — for example <c>question:province:1</c> or
    /// <c>question_option:province:1:alberta</c>.
    /// </param>
    public static TinyId For(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var bytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[Namespace.Length + bytes.Length];
        Namespace.CopyTo(input, 0);
        bytes.CopyTo(input, Namespace.Length);

        return TinyId.FromEntropy(SHA256.HashData(input));
    }
}
