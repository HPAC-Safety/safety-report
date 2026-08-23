using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Tests.Media;

internal sealed class StubMediaSniffer(MediaType? result) : IMediaSniffer
{
    public Task<MediaType?> SniffAsync(Stream content, CancellationToken cancellationToken) =>
        Task.FromResult(result);
}
