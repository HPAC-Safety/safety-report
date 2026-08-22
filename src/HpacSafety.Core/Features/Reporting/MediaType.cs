using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// A media format this system is willing to accept for an upload. The set is
/// deliberately closed and deliberately small: a format only belongs here once
/// this system can strip its metadata, because a file whose EXIF cannot be
/// removed has no derivative a reviewer may safely be shown.
/// See docs/data-handling.md and ADR-0025.
/// </summary>
public readonly record struct MediaType
{
    /// <summary>JPEG — the format that carries GPS EXIF most often.</summary>
    public static readonly MediaType Jpeg = new("image/jpeg", "jpg");

    /// <summary>PNG.</summary>
    public static readonly MediaType Png = new("image/png", "png");

    /// <summary>WebP.</summary>
    public static readonly MediaType WebP = new("image/webp", "webp");

    private MediaType(string contentType, string extension)
    {
        ContentType = contentType;
        Extension = extension;
    }

    /// <summary>The invariant content type, as stored and as served.</summary>
    public string ContentType { get; }

    /// <summary>The canonical file extension.</summary>
    public string Extension { get; }

    /// <summary>Every media type this system accepts.</summary>
    public static IReadOnlyList<MediaType> All { get; } = [Jpeg, Png, WebP];

    /// <summary>Parses a content type, throwing when it is not one this system accepts.</summary>
    public static MediaType Parse(string? candidate) =>
        TryParse(candidate, out var type)
            ? type
            : throw new DomainRuleViolationException($"'{candidate}' is not an accepted media type.");

    /// <summary>
    /// Parses a content type without throwing. Parameters such as
    /// <c>; charset=binary</c> are ignored and casing does not matter, because
    /// both vary between browsers.
    /// </summary>
    public static bool TryParse(string? candidate, out MediaType type)
    {
        type = default;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var essence = candidate.Split(';')[0].Trim();

        foreach (var known in All)
        {
            if (string.Equals(known.ContentType, essence, StringComparison.OrdinalIgnoreCase))
            {
                type = known;
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override string ToString() => ContentType;
}
