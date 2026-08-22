using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Issues the pre-signed PUT a browser uploads through.
/// <para>
/// Every upload lands in <see cref="MediaCompartment.Quarantine" /> and nowhere
/// else. Minting the URL in one place is what makes that true: a caller cannot
/// accidentally hand out a URL that writes straight into a report's Restricted
/// record, because it never names the compartment. See ADR-0026.
/// </para>
/// <para>
/// The declared type is a <see cref="MediaType" /> rather than a string, so a
/// client cannot even ask for a format this system does not accept. It is still
/// only a declaration — the sniff on ingest is what decides.
/// </para>
/// </summary>
public sealed class MediaUploadSlot
{
    private readonly IBlobStore _blobStore;

    /// <summary>Creates the issuer.</summary>
    public MediaUploadSlot(IBlobStore blobStore)
    {
        ArgumentNullException.ThrowIfNull(blobStore);
        _blobStore = blobStore;
    }

    /// <summary>
    /// A short-lived URL the browser may PUT one file to, scoped to one
    /// quarantine key.
    /// </summary>
    public async Task<MediaUpload> CreateAsync(
        string reportId,
        string fileName,
        MediaType declaredType,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var key = BlobKey.For(reportId, MediaCompartment.Quarantine, fileName);
        var url = await _blobStore.CreateUploadUrlAsync(key, declaredType.ContentType, lifetime, cancellationToken)
            .ConfigureAwait(false);

        return new MediaUpload(key, url);
    }
}

/// <summary>An issued upload slot: where the bytes will land, and the URL that puts them there.</summary>
/// <param name="Key">The quarantine key the URL is signed for.</param>
/// <param name="Url">The pre-signed PUT.</param>
public readonly record struct MediaUpload(BlobKey Key, Uri Url);
