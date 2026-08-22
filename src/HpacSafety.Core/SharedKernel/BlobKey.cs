namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// The name of one object in private storage. A value object rather than a
/// <c>string</c> because a key is attacker-influenced: it reaches a bucket in
/// production and a directory in development, and <c>../</c> means something
/// very different in the second. Parsing is the only way to make one, so an
/// unvalidated key cannot be handed to <see cref="IBlobStore" />.
/// See ADR-0026.
/// </summary>
public readonly record struct BlobKey
{
    /// <summary>The longest key this system will produce or accept.</summary>
    public const int MaxLength = 512;

    private BlobKey(string value) => Value = value;

    /// <summary>The key as stored.</summary>
    public string Value { get; }

    /// <summary>Parses a key, throwing when it breaks the rules.</summary>
    public static BlobKey Parse(string? candidate) =>
        TryParse(candidate, out var key)
            ? key
            : throw new DomainRuleViolationException($"'{candidate}' is not a valid blob key.");

    /// <summary>Parses a key without throwing.</summary>
    public static bool TryParse(string? candidate, out BlobKey key)
    {
        key = default;

        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength)
        {
            return false;
        }

        var segments = candidate.Split('/');
        foreach (var segment in segments)
        {
            if (!IsValidSegment(segment))
            {
                return false;
            }
        }

        key = new BlobKey(candidate);
        return true;
    }

    /// <summary>
    /// The same key under another prefix — how the EXIF-stripped derivative is
    /// named, so that original and derivative can never collide.
    /// </summary>
    public BlobKey WithPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        if (!IsValidSegment(prefix))
        {
            throw new DomainRuleViolationException($"'{prefix}' is not a valid blob key prefix.");
        }

        return Parse($"{prefix}/{Value}");
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsValidSegment(string segment)
    {
        // An empty segment is a leading slash, a trailing slash, or a double
        // slash; "." and ".." are the traversal that FileSystemBlobStore must
        // never see.
        if (segment.Length == 0 || segment == "." || segment == "..")
        {
            return false;
        }

        foreach (var character in segment)
        {
            var allowed = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                or '-' or '_' or '.';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
