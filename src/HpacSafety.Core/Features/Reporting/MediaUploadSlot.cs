using System.Security.Cryptography;
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
/// <b>The file name is minted here, never taken from the client.</b> A camera
/// roll name is Restricted data in its own right — <c>mt-7-tandem-dave.jpg</c>
/// names a site and a person — and a key ends up in bucket access logs, in
/// CloudTrail, and in every pre-signed URL. The reporter's name for the file is
/// of no use to this system, so it is not carried. See
/// docs/anonymization-policy.md on small-community identifiability.
/// </para>
/// <para>
/// The declared type is a <see cref="MediaType" /> rather than a string, so a
/// client cannot even ask for a format this system does not accept. It is still
/// only a declaration — the sniff on ingest is what decides.
/// </para>
/// </summary>
public sealed class MediaUploadSlot
{
    // The tiny-id alphabet, so a minted file name looks like every other
    // identifier here. Random rather than sequential: a key should carry no
    // information at all.
    private const string NameAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    private const int NameLength = 11;

    private readonly IBlobStore _blobStore;

    /// <summary>Creates the issuer.</summary>
    public MediaUploadSlot(IBlobStore blobStore)
    {
        ArgumentNullException.ThrowIfNull(blobStore);
        _blobStore = blobStore;
    }

    /// <summary>
    /// A short-lived URL the browser may PUT one file to, scoped to one
    /// quarantine key under one report.
    /// <para>
    /// <b>Caller's responsibility:</b> this takes <paramref name="reportId" /> on
    /// trust. It is a capability check, not an identity check — whoever calls
    /// this must already have established that the caller owns that report. See
    /// docs/data-handling.md.
    /// </para>
    /// </summary>
    public async Task<MediaUpload> CreateAsync(
        string reportId,
        MediaType declaredType,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var fileName = $"{RandomNumberGenerator.GetString(NameAlphabet, NameLength)}.{declaredType.Extension}";
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
