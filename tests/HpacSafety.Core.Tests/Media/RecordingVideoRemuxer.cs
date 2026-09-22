using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     Stands in for ffmpeg. It writes a marker rather than a container, so a test
///     can tell the derivative apart from the original without a demuxer, and it can
///     be told to produce nothing — the case where a video is retained unstripped
///     because the toolchain could not clean it (REQ-MED-015).
/// </summary>
internal sealed class RecordingVideoRemuxer(bool produces = true) : IVideoRemuxer
{
	public int Invocations { get; private set; }

	public async Task<bool> TryRemux(
		Stream source, Stream destination, MediaType type, CancellationToken cancellationToken)
	{
		Invocations++;

		if (!produces)
		{
			// Nothing is written, so a caller cannot mistake a partial write for
			// a derivative.
			return false;
		}

		using var buffer = new MemoryStream();
		await source.CopyToAsync(buffer, cancellationToken);
		var remuxed = "REMUXED:"u8.ToArray().Concat(buffer.ToArray()).ToArray();
		await destination.WriteAsync(remuxed, cancellationToken);
		return true;
	}
}
