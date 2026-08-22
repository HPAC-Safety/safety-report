using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// The only sanctioned way to hand a safety officer a link to an uploaded photo.
/// <para>
/// <see cref="IBlobStore.CreateReadUrlAsync" /> will mint a URL for any key it is
/// given, the original included — it is generic storage and knows nothing about
/// which bytes are safe to look at. This does. A reviewer sees the stripped
/// derivative and never the original, so a key that is not under
/// <see cref="MediaIngestor.DerivativePrefix" /> is refused here rather than
/// relied on to be correct at the call site.
/// See docs/data-handling.md and ADR-0026.
/// </para>
/// </summary>
public sealed class ReviewerMediaLink
{
    private readonly IBlobStore _blobStore;

    /// <summary>Creates the link issuer.</summary>
    public ReviewerMediaLink(IBlobStore blobStore)
    {
        ArgumentNullException.ThrowIfNull(blobStore);
        _blobStore = blobStore;
    }

    /// <summary>
    /// A short-lived pre-signed GET for a stripped derivative. Throws when the
    /// key names anything else — including the original, which is Restricted and
    /// is never shown.
    /// </summary>
    public Task<Uri> CreateViewUrlAsync(BlobKey key, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        if (!IsDerivative(key))
        {
            throw new DomainRuleViolationException(
                "A reviewer may only be shown a stripped derivative, never the original upload.");
        }

        return _blobStore.CreateReadUrlAsync(key, lifetime, cancellationToken);
    }

    /// <summary>True when the key names a stripped derivative rather than an original.</summary>
    public static bool IsDerivative(BlobKey key) =>
        key.Value.StartsWith(MediaIngestor.DerivativePrefix + "/", StringComparison.Ordinal);
}
