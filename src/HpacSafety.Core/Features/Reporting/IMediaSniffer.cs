namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Decides what a file actually is by reading its bytes. Declared content types
/// come from the client and are not evidence of anything.
/// <para>
/// A port: the implementation reads image formats through an imaging library and
/// lives in <c>HpacSafety.Infrastructure</c>.
/// </para>
/// </summary>
public interface IMediaSniffer
{
    /// <summary>
    /// The format the bytes really are, or <see langword="null" /> when they are
    /// not a format this system recognises. Never throws for unrecognised input —
    /// "I do not know what this is" is an answer, not a failure.
    /// </summary>
    Task<MediaType?> SniffAsync(Stream content, CancellationToken cancellationToken);
}
