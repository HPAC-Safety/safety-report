using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Tests.Media;

internal sealed class StubMediaSniffer(MediaType? result) : IMediaSniffer
{
	public Task<MediaType?> Sniff(Stream content,
								  CancellationToken cancellationToken)
	{
		return Task.FromResult(result);
	}
}
