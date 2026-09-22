namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     Produces the metadata-free derivative a reviewer is shown for a video, by
///     remuxing it — moving the compressed packets into a fresh container and
///     leaving every tag and every non-audiovisual track behind.
///     <para>
///         Deliberately not <see cref="IExifStripper" />. Images are decoded and
///         re-encoded by an imaging library; video must not be, and ADR-0025's
///         choice of Magick.NET explicitly excludes it. See ADR-0094.
///     </para>
///     <para>
///         A port: the implementation drives an external toolchain and lives in
///         <c>HpacSafety.Infrastructure</c>.
///     </para>
/// </summary>
public interface IVideoRemuxer
{
	/// <summary>
	///     Writes a remuxed copy of <paramref name="source" /> to
	///     <paramref name="destination" />, carrying the video and any audio stream
	///     and nothing else, and verifies the result before returning true.
	///     <para>
	///         Returns <see langword="false" /> when no verified derivative can be
	///         produced — the toolchain is absent, the remux fails, or the output
	///         still holds metadata or a track it should not. That is not an error:
	///         the original is retained and the reporter keeps their footage
	///         (REQ-MED-015). It throws only when something genuinely unexpected
	///         happens, which the caller treats as a processing failure.
	///     </para>
	///     <para>
	///         <paramref name="destination" /> is left empty when this returns
	///         false, so a caller cannot mistake a partial write for a derivative.
	///     </para>
	/// </summary>
	Task<bool> TryRemux(Stream source, Stream destination, MediaType type, CancellationToken cancellationToken);
}
