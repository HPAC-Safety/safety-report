namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Removes embedded metadata — GPS above all — from an image, producing the
/// derivative a reviewer is shown. A crash photo identifies a person and a site
/// regardless of how clean the text is. See docs/data-handling.md.
/// <para>
/// A port: the implementation wraps an imaging library and lives in
/// <c>HpacSafety.Infrastructure</c>. See ADR-0025.
/// </para>
/// </summary>
public interface IExifStripper
{
    /// <summary>
    /// Writes <paramref name="source" /> to <paramref name="destination" /> with
    /// every metadata profile removed. Throws when the bytes cannot be read as
    /// <paramref name="type" /> — an image that cannot be stripped must not
    /// produce a derivative.
    /// </summary>
    Task StripAsync(Stream source, Stream destination, MediaType type, CancellationToken cancellationToken);
}
