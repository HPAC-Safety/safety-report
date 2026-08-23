using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
/// A <b>Chain of Responsibility</b> over the sniffers: each is asked in turn and
/// the first one that recognises the bytes answers.
/// <para>
/// The variation is real rather than invented — images are identified by parsing
/// them with an imaging library, video deliberately is not — and the chain is
/// what lets those two stay separate classes without the caller knowing there is
/// more than one. Order matters: images first, because HEIC and MP4 share the
/// ISO base media container and only the brand tells them apart.
/// </para>
/// </summary>
public sealed class MediaSnifferChain : IMediaSniffer
{
    private readonly IReadOnlyList<IMediaSniffer> _sniffers;

    /// <summary>Creates a chain over the sniffers, in the order they should be asked.</summary>
    public MediaSnifferChain(params IMediaSniffer[] sniffers)
    {
        ArgumentNullException.ThrowIfNull(sniffers);

        if (sniffers.Length == 0)
        {
            throw new ArgumentException("A sniffer chain with no links recognises nothing.", nameof(sniffers));
        }

        _sniffers = sniffers;
    }

    /// <summary>The chain this system runs: images through Magick.NET, then video by magic number.</summary>
    public static MediaSnifferChain Default() => new(new MagickNetMediaSniffer(), new VideoContainerSniffer());

    /// <inheritdoc />
    public async Task<MediaType?> SniffAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);

        foreach (var sniffer in _sniffers)
        {
            // Each link gets the stream from the start. A link that consumed it
            // would silently starve the next one, which is the kind of bug that
            // shows up as "video uploads stopped working" months later.
            buffered.Position = 0;

            if (await sniffer.SniffAsync(buffered, cancellationToken).ConfigureAwait(false) is { } recognised)
            {
                return recognised;
            }
        }

        return null;
    }
}
