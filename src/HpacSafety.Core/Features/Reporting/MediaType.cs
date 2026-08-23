using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// A media format this system accepts for an upload. The set is closed and every
/// member carries the one fact that matters downstream: what its stripped
/// derivative is, or that it does not have one yet.
/// <para>
/// A format without a stripped form is still accepted and still retained — the
/// original is the private source record either way — but it produces no derivative,
/// so <see cref="ReviewerMediaLink" /> has nothing to issue a URL for and a
/// reviewer sees nothing rather than something unsafe. See
/// docs/data-handling.md and ADR-0025.
/// </para>
/// </summary>
public readonly record struct MediaType
{
    /// <summary>JPEG — the format that carries GPS EXIF most often.</summary>
    public static readonly MediaType Jpeg = new("image/jpeg", "jpg", MediaKind.Image);

    /// <summary>PNG.</summary>
    public static readonly MediaType Png = new("image/png", "png", MediaKind.Image);

    /// <summary>WebP.</summary>
    public static readonly MediaType WebP = new("image/webp", "webp", MediaKind.Image);

    /// <summary>
    /// HEIC — an iPhone's default, and a common carrier of GPS. Its derivative is
    /// a JPEG: the runtime imaging library can decode HEIC but not encode it, and
    /// a reviewer needs something every browser renders anyway.
    /// </summary>
    public static readonly MediaType Heic = new("image/heic", "heic", MediaKind.Image);

    /// <summary>MP4. Accepted and retained; no derivative until #65.</summary>
    public static readonly MediaType Mp4 = new("video/mp4", "mp4", MediaKind.Video);

    /// <summary>QuickTime, an iPhone's video default. Accepted and retained; no derivative until #65.</summary>
    public static readonly MediaType QuickTime = new("video/quicktime", "mov", MediaKind.Video);

    private MediaType(string contentType, string extension, MediaKind kind)
    {
        ContentType = contentType;
        Extension = extension;
        Kind = kind;
    }

    /// <summary>The invariant content type, as stored and as served.</summary>
    public string ContentType { get; }

    /// <summary>The canonical file extension.</summary>
    public string Extension { get; }

    /// <summary>Photo or video.</summary>
    public MediaKind Kind { get; }

    /// <summary>Every media type this system accepts.</summary>
    public static IReadOnlyList<MediaType> All { get; } = [Jpeg, Png, WebP, Heic, Mp4, QuickTime];

    /// <summary>The image formats, which are the ones a derivative can be made from today.</summary>
    public static IReadOnlyList<MediaType> Strippable { get; } = [Jpeg, Png, WebP, Heic];

    /// <summary>
    /// What this type's stripped derivative is written as, or <see langword="null" />
    /// when this system cannot strip it yet. HEIC becomes JPEG; every other image
    /// keeps its own format; video has no answer until #65.
    /// </summary>
    public MediaType? StrippedForm =>
        Kind == MediaKind.Video ? null
        : ContentType == Heic.ContentType ? Jpeg
        : this;

    /// <summary>True when ingest can produce a derivative a reviewer may see.</summary>
    public bool CanBeStripped => StrippedForm is not null;

    /// <summary>Parses a content type, throwing when it is not one this system accepts.</summary>
    public static MediaType Parse(string? candidate) =>
        TryParse(candidate, out var type)
            ? type
            // Not echoed: the declared content type is a raw client header, and
            // a header is not something to interpolate into a message a log will
            // later hold. The caller already knows what it passed.
            : throw new DomainRuleViolationException("The declared content type is not one this system accepts.");

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
