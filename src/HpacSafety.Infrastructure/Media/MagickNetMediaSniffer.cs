using HpacSafety.Core.Features.Reporting;
using ImageMagick;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
/// An <b>Adapter</b> over Magick.NET that answers what an image file really is.
/// <para>
/// It asks twice, and both answers have to agree. First a magic-number check
/// against the closed set of accepted image formats, which is cheap and keeps
/// content this system will never accept away from an imaging library entirely.
/// Then Magick.NET parses the header and reports its own format. A file whose
/// leading bytes say JPEG but whose structure says otherwise is unrecognised,
/// not a JPEG. See ADR-0025.
/// </para>
/// <para>
/// Images only. Video is <see cref="VideoContainerSniffer" />'s, deliberately,
/// so that no video is ever opened by an imaging library.
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

        if (FromMagicNumber(bytes) is not { } expected)
        {
            return null;
        }

        try
        {
            buffered.Position = 0;
            var info = new MagickImageInfo(buffered);

            return MagickFormats.From(info.Format) == expected ? expected : null;
        }
        catch (MagickException)
        {
            // Not a failure - "I do not know what this is" is the answer, and the
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

        // HEIC is an ISO base media container, like MP4: a length, then "ftyp",
        // then the brand that says which dialect it is.
        if (bytes[4..8].SequenceEqual("ftyp"u8) && IsHeicBrand(bytes[8..12]))
        {
            return MediaType.Heic;
        }

        return null;
    }

    private static bool IsHeicBrand(ReadOnlySpan<byte> brand) =>
        brand.SequenceEqual("heic"u8)
        || brand.SequenceEqual("heix"u8)
        || brand.SequenceEqual("heim"u8)
        || brand.SequenceEqual("heis"u8)
        || brand.SequenceEqual("hevc"u8)
        || brand.SequenceEqual("hevx"u8)
        || brand.SequenceEqual("mif1"u8)
        || brand.SequenceEqual("msf1"u8);
}
