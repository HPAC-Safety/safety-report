using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Tests.Media;

/// <summary>An image library that cannot decode what it is given.</summary>
internal sealed class ThrowingExifStripper : IExifStripper
{
	public Task Strip(Stream source,
					  Stream destination,
					  MediaType type,
					  CancellationToken cancellationToken)
	{
		throw new InvalidDataException("Synthetic decode failure.");
	}
}
