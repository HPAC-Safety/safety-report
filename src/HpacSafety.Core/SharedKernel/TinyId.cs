using System.Security.Cryptography;

namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// The identifier every row in this system carries: eleven characters over the
/// alphabet YouTube uses for a video id, drawn from a cryptographically secure
/// random source.
/// </summary>
/// <remarks>
/// <para>
/// One convention for every table, so there are no mixed-type joins and nothing
/// for a reader to remember. Eleven symbols over sixty-four is sixty-six bits.
/// </para>
/// <para>
/// It is chosen for what it does <b>not</b> say as much as for what it does.
/// A sequential key leaks how many reports there are and what order they
/// arrived in; a UUIDv7 leaks the moment a row was created. This system
/// deliberately narrows a published occurrence date to a month and a year so
/// that a report cannot be tied back to a moment — an identifier that carries a
/// timestamp would hand that back. A tiny id encodes nothing, is not
/// enumerable, and is short enough to sit inside a blob key or a URL without
/// looking like a mistake. See ADR-0034.
/// </para>
/// </remarks>
public readonly record struct TinyId
{
    /// <summary>How many characters an identifier has. Never more, never fewer.</summary>
    public const int Length = 11;

    /// <summary>
    /// The sixty-four symbols an identifier is built from — URL-safe base64's
    /// alphabet, and the one YouTube uses. Case-sensitive: <c>a</c> and
    /// <c>A</c> are different identifiers.
    /// </summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    private const int BitsPerSymbol = 6;

    private readonly string? _value;

    private TinyId(string value) => _value = value;

    /// <summary>The identifier as text. Empty for a default-constructed value.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Whether this is the default value rather than a real identifier. A
    /// persisted row never holds one.
    /// </summary>
    public bool IsEmpty => _value is null;

    /// <summary>Mints a new identifier from a cryptographically secure source.</summary>
    public static TinyId New() => FromEntropy(RandomNumberGenerator.GetBytes(Length));

    /// <summary>
    /// Derives an identifier from bytes that are already unpredictable — a
    /// hash, for instance, when the same input has to produce the same
    /// identifier on every machine.
    /// </summary>
    /// <param name="entropy">
    /// At least <see cref="Length"/> bytes. Only the low six bits of each of the
    /// first <see cref="Length"/> are read.
    /// </param>
    public static TinyId FromEntropy(ReadOnlySpan<byte> entropy)
    {
        if (entropy.Length < Length)
        {
            throw new ArgumentException(
                $"An identifier needs at least {Length} bytes to derive from, not {entropy.Length}.",
                nameof(entropy));
        }

        return new TinyId(string.Create(
            Length,
            // string.Create cannot close over a span, so the bytes are copied.
            entropy[..Length].ToArray(),
            static (span, source) =>
            {
                for (var i = 0; i < Length; i++)
                {
                    // 64 divides 256, so masking stays uniform.
                    span[i] = Alphabet[source[i] & ((1 << BitsPerSymbol) - 1)];
                }
            }));
    }

    /// <summary>Reads an identifier back from text.</summary>
    /// <param name="candidate">The text to read.</param>
    /// <exception cref="DomainRuleViolationException">
    /// The text is not exactly <see cref="Length"/> characters of
    /// <see cref="Alphabet"/>.
    /// </exception>
    public static TinyId Parse(string? candidate) =>
        TryParse(candidate, out var id)
            ? id
            : throw new DomainRuleViolationException(
                $"'{candidate}' is not an identifier. One is exactly {Length} characters of '{Alphabet}'.");

    /// <summary>Reads an identifier back from text, without throwing.</summary>
    /// <param name="candidate">The text to read.</param>
    /// <param name="id">The identifier, if the text was one.</param>
    public static bool TryParse(string? candidate, out TinyId id)
    {
        id = default;

        if (candidate is null || candidate.Length != Length)
        {
            return false;
        }

        foreach (var character in candidate)
        {
            if (!Alphabet.Contains(character, StringComparison.Ordinal))
            {
                return false;
            }
        }

        id = new TinyId(candidate);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
