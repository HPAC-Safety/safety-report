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
/// handle one this system does not accept.
/// </para>
/// </summary>
public sealed class MagickNetExifStripper : IExifStripper
{
    /// <inheritdoc />
    public async Task StripAsync(Stream source, Stream destination, MediaType type, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var format = MagickFormats.For(type);

        using var image = new MagickImage();
        await image.ReadAsync(source, new MagickReadSettings { Format = format }, cancellationToken).ConfigureAwait(false);

        // Carried across the re-encode so the derivative a reviewer sees is as
        // close to the original as stripping allows. The original bytes are kept
        // untouched regardless; they are the Restricted record.
        var quality = image.Quality;
        image.Strip();
        image.Quality = quality;

        await image.WriteAsync(destination, format, cancellationToken).ConfigureAwait(false);
    }
}
