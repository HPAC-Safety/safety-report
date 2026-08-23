using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
/// Recognises the two video containers this system accepts, by magic number
/// alone.
/// <para>
/// No library is involved, and that is the design rather than a shortcut.
/// ImageMagick will happily report MP4 as readable because it can shell out to a
/// delegate, and handing an attacker-supplied video to a delegate is a category
/// of vulnerability this system has no reason to be exposed to. Nothing here
/// decodes anything: it reads twelve bytes and returns a content type.
/// </para>
/// <para>
/// There is no stripper for either format yet, so a video is retained and never
/// shown — see <see cref="MediaType.StrippedForm" /> and issue #65.
/// </para>
/// </summary>
public sealed class VideoContainerSniffer : IMediaSniffer
{
    private const int HeaderLength = 12;

    /// <inheritdoc />
    public async Task<MediaType?> SniffAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var header = new byte[HeaderLength];
        var read = await content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);

        if (read < HeaderLength || !header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            return null;
        }

        return FromBrand(header.AsSpan(8, 4));
    }

    private static MediaType? FromBrand(ReadOnlySpan<byte> brand)
    {
        if (brand.SequenceEqual("qt  "u8))
        {
            return MediaType.QuickTime;
        }

        var isMp4 = brand.SequenceEqual("isom"u8)
            || brand.SequenceEqual("iso2"u8)
            || brand.SequenceEqual("iso4"u8)
            || brand.SequenceEqual("iso5"u8)
            || brand.SequenceEqual("iso6"u8)
            || brand.SequenceEqual("mp41"u8)
            || brand.SequenceEqual("mp42"u8)
            || brand.SequenceEqual("avc1"u8)
            || brand.SequenceEqual("M4V "u8)
            || brand.SequenceEqual("dash"u8);

        return isMp4 ? MediaType.Mp4 : null;
    }
}
