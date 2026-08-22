using HpacSafety.Core.Features.Reporting;
using ImageMagick;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
/// The one place the domain's <see cref="MediaType" /> meets ImageMagick's
/// <see cref="MagickFormat" />. Keeping the mapping here means the SDK enum
/// never leaks into <c>Core</c>, and adding a format is one edit.
/// <para>
/// Video is absent on purpose. ImageMagick reports MP4 and MOV as readable
/// because it can shell out to a delegate for them, which is exactly the
/// behaviour this system does not want: video is sniffed by magic number alone
/// and never handed to an imaging library.
/// </para>
/// </summary>
internal static class MagickFormats
{
    public static MagickFormat For(MediaType type) =>
        type == MediaType.Jpeg ? MagickFormat.Jpeg
        : type == MediaType.Png ? MagickFormat.Png
        : type == MediaType.WebP ? MagickFormat.WebP
        : type == MediaType.Heic ? MagickFormat.Heic
        : throw new NotSupportedException($"No ImageMagick format is mapped for '{type}'.");

    public static MediaType? From(MagickFormat format) => format switch
    {
        MagickFormat.Jpeg or MagickFormat.Jpg or MagickFormat.Jpe => MediaType.Jpeg,
        MagickFormat.Png or MagickFormat.Png00 or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32
            or MagickFormat.Png48 or MagickFormat.Png64 => MediaType.Png,
        MagickFormat.WebP => MediaType.WebP,
        MagickFormat.Heic or MagickFormat.Heif => MediaType.Heic,
        _ => null,
    };
}
