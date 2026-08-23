using HpacSafety.Core.Features.Reporting;
using ImageMagick;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
/// An <b>Adapter</b> over Magick.NET that removes every embedded metadata
/// profile — EXIF, IPTC, XMP, ICC, and the free-text comment — from an image.
/// GPS above all: a crash photo identifies a person and a site regardless of how
/// clean the text is. See docs/data-handling.md and ADR-0025.
/// <para>
/// The read is pinned to the format the sniffer already agreed on, so
/// ImageMagick never guesses at a format and never reaches for a delegate to
/// handle one this system does not accept. The write uses
/// <see cref="MediaType.StrippedForm" />, which is how a HEIC upload becomes a
/// JPEG derivative every browser can render.
/// </para>
/// <para>
/// Constructing it checks that this runtime can actually decode everything the
/// deployment accepts, so a missing codec is a failure to start rather than a
/// stream of unexplained rejections. Construct it eagerly at startup.
/// </para>
/// </summary>
public sealed class MagickNetExifStripper : IExifStripper
{
    /// <summary>
    /// Creates the stripper and verifies the runtime's codecs, throwing
    /// <see cref="MissingImagingCodecException" /> when one this deployment needs
    /// is absent.
    /// </summary>
    public MagickNetExifStripper(IEnumerable<MediaType> acceptedTypes) =>
        ImagingCapabilities.EnsureCanDecode(acceptedTypes);

    /// <inheritdoc />
    public async Task StripAsync(Stream source, Stream destination, MediaType type, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (type.StrippedForm is not { } strippedForm)
        {
            throw new NotSupportedException($"There is no way to strip '{type}' yet. See issue #65.");
        }

        var readFormat = MagickFormats.For(type);
        var writeFormat = MagickFormats.For(strippedForm);

        using var image = new MagickImage();
        await image.ReadAsync(source, new MagickReadSettings { Format = readFormat }, cancellationToken).ConfigureAwait(false);

        // Carried across the re-encode so the derivative a reviewer sees is as
        // close to the original as stripping allows. The original bytes are kept
        // untouched regardless; they are the private source record.
        var quality = image.Quality;
        image.Strip();
        image.Quality = quality;

        await image.WriteAsync(destination, writeFormat, cancellationToken).ConfigureAwait(false);
    }
}
