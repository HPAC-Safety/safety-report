using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Counts calls so a scenario can assert that an image decoder never touched a
///     video (ADR-0025, ADR-0094), while still behaving like a stripper for the
///     image paths that legitimately use one.
/// </summary>
internal sealed class RecordingImageStripper : IExifStripper
{
	public int Invocations { get; private set; }

	public async Task Strip(Stream source,
							Stream destination,
							MediaType type,
							CancellationToken cancellationToken)
	{
		Invocations++;
		using var buffer = new MemoryStream();
		await source.CopyToAsync(buffer, cancellationToken);
		await destination.WriteAsync("STRIPPED:"u8.ToArray().Concat(buffer.ToArray()).ToArray(), cancellationToken);
	}
}
