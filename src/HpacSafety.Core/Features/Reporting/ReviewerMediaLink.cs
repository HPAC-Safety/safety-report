using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// The only sanctioned way to hand a safety officer a link to uploaded media.
/// <para>
/// <see cref="IBlobStore.CreateReadUrlAsync" /> will mint a URL for any key it is
/// given — the quarantined upload and the private original included. It is
/// generic storage and knows nothing about which bytes are safe to look at. This
/// does: only <see cref="MediaCompartment.Stripped" /> is ever issued.
/// </para>
/// <para>
/// The check is on the compartment, which is a parsed property of
/// <see cref="BlobKey" /> rather than a substring of it. That matters: the
/// layout puts the report id first, so a prefix match would have had to move
/// when the layout did, and a check that silently passes a differently-shaped
/// key is worse than one that fails. A video has no stripped compartment at all
/// until #65, so it is refused here by the same rule rather than a special case.
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
    /// A short-lived pre-signed GET for a stripped derivative. Throws for
    /// anything else, including the original and the quarantined upload.
    /// </summary>
    public Task<Uri> CreateViewUrlAsync(BlobKey key, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        if (!IsViewable(key))
        {
            throw new DomainRuleViolationException(
                "A reviewer may only be shown a stripped derivative, never the original upload.");
        }

        return _blobStore.CreateReadUrlAsync(key, lifetime, cancellationToken);
    }

    /// <summary>True when the key names a stripped derivative rather than an original or a quarantined upload.</summary>
    public static bool IsViewable(BlobKey key) => key.Compartment is MediaCompartment.Stripped;
}
