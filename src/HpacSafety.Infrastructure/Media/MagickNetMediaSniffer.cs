using HpacSafety.Core.Features.Reporting;
using ImageMagick;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
/// An <b>Adapter</b> over Magick.NET that answers what a file really is.
/// <para>
/// It asks twice, and both answers have to agree. First a magic-number check
/// against the closed set of accepted formats, which is cheap and keeps content
/// this system will never accept away from an imaging library entirely. Then
/// Magick.NET parses the header and reports its own format. A file whose leading
/// bytes say JPEG but whose structure says otherwise is unrecognised, not a
/// JPEG. See ADR-0025.
/// </para>
/// </summary>
public sealed class MagickNetMediaSniffer : IMediaSniffer
{
    private const int HeaderLength = 16;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <inheritdoc />
    public async Task<MediaType?> SniffAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
        var bytes = buffered.ToArray();

        var claimed = FromMagicNumber(bytes);
        if (claimed is not { } expected)
        {
            return null;
        }

        try
        {
            buffered.Position = 0;
            var info = new MagickImageInfo(buffered);
            var parsed = MagickFormats.From(info.Format);

            return parsed == expected ? expected : null;
        }
        catch (MagickException)
        {
            // Not a failure — "I do not know what this is" is the answer, and the
            // caller rejects it. See IMediaSniffer.
            return null;
        }
    }

    private static MediaType? FromMagicNumber(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderLength)
        {
            return null;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return MediaType.Jpeg;
        }

        if (bytes[..8].SequenceEqual(PngSignature))
        {
            return MediaType.Png;
        }

        if (bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return MediaType.WebP;
        }

        return null;
    }
}
