using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// Stands in for the real stripper. It writes a marker rather than an image, so
/// a test can tell the derivative apart from the original without decoding it.
/// </summary>
internal sealed class RecordingExifStripper : IExifStripper
{
    public int Invocations { get; private set; }

    public async Task StripAsync(Stream source, Stream destination, MediaType type, CancellationToken cancellationToken)
    {
        Invocations++;
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        var stripped = "STRIPPED:"u8.ToArray().Concat(buffer.ToArray()).ToArray();
        await destination.WriteAsync(stripped, cancellationToken);
    }
}
