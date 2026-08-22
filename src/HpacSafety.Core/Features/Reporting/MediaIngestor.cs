using System.Security.Cryptography;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Turns bytes a browser dropped into quarantine into something this system is
/// willing to keep — and, where it can, into something a reviewer may safely be
/// shown.
/// <para>
/// The order is the point. Sniff, validate, then <i>promote</i>: nothing leaves
/// quarantine until this system has decided what it is. A refused upload is
/// simply never promoted, so it needs no delete — it expires where it landed,
/// through a bucket lifecycle rule. That is deliberate: no code path exists
/// which could later be pointed at a real report's media.
/// </para>
/// <para>
/// A format this system cannot strip is still promoted, because the original is
/// the Restricted record regardless, but it produces no derivative and the
/// outcome says so. It fails closed: there is nothing for a reviewer to open,
/// rather than a fall-through to the unstripped original.
/// </para>
/// <para>
/// It lives in <c>Core</c> and depends only on ports, so the rule "a reviewer
/// only ever sees stripped bytes" is provable in a plain unit test with no
/// bucket, no database and no imaging library.
/// </para>
/// </summary>
public sealed class MediaIngestor
{
    private readonly IBlobStore _blobStore;
    private readonly IMediaSniffer _sniffer;
    private readonly IExifStripper _stripper;
    private readonly MediaPolicy _policy;
    private readonly TimeProvider _clock;

    /// <summary>Creates an ingestor over the ports it needs.</summary>
    public MediaIngestor(
        IBlobStore blobStore,
        IMediaSniffer sniffer,
        IExifStripper stripper,
        MediaPolicy policy,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(blobStore);
        ArgumentNullException.ThrowIfNull(sniffer);
        ArgumentNullException.ThrowIfNull(stripper);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        _blobStore = blobStore;
        _sniffer = sniffer;
        _stripper = stripper;
        _policy = policy;
        _clock = clock;
    }

    /// <summary>
    /// Reads the quarantined upload, judges it, and on acceptance promotes it to
    /// the Restricted record — writing a stripped derivative alongside when the
    /// format allows one.
    /// </summary>
    public async Task<MediaIngestOutcome> IngestAsync(
        BlobKey quarantineKey,
        string? declaredContentType,
        CancellationToken cancellationToken)
    {
        if (quarantineKey.Compartment is not MediaCompartment.Quarantine)
        {
            throw new DomainRuleViolationException("Ingest reads from quarantine and nowhere else.");
        }

        // Buffered rather than streamed: the bytes are read three times - for the
        // digest, for the sniff and for the strip - and MediaPolicy.MaxByteSize
        // is what bounds how much that costs.
        using var original = new MemoryStream();

        await using (var source = await _blobStore.OpenReadAsync(quarantineKey, cancellationToken).ConfigureAwait(false))
        {
            await source.CopyToAsync(original, cancellationToken).ConfigureAwait(false);
        }

        var byteSize = original.Length;

        if (byteSize <= 0 || byteSize > _policy.MaxByteSize)
        {
            // Refused before anything decodes it: an oversized file is not worth
            // handing to an imaging library.
            return MediaIngestOutcome.Rejected(_policy.Validate(declaredContentType, null, byteSize).RejectionReason);
        }

        original.Position = 0;
        var sniffed = await _sniffer.SniffAsync(original, cancellationToken).ConfigureAwait(false);

        var verdict = _policy.Validate(declaredContentType, sniffed, byteSize);
        if (!verdict.IsAccepted)
        {
            return MediaIngestOutcome.Rejected(verdict.RejectionReason);
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(original.GetBuffer().AsSpan(0, (int)byteSize)));

        var originalKey = quarantineKey.In(MediaCompartment.Original);
        original.Position = 0;
        await _blobStore.WriteAsync(originalKey, original, verdict.Type.ContentType, cancellationToken).ConfigureAwait(false);

        if (verdict.Type.StrippedForm is not { } derivativeType)
        {
            // Retained, and deliberately not viewable. See #65.
            return MediaIngestOutcome.Retained(verdict.Type, byteSize, sha256, originalKey);
        }

        original.Position = 0;
        using var stripped = new MemoryStream();
        await _stripper.StripAsync(original, stripped, verdict.Type, cancellationToken).ConfigureAwait(false);

        var derivativeKey = quarantineKey.In(MediaCompartment.Stripped);
        stripped.Position = 0;
        await _blobStore.WriteAsync(derivativeKey, stripped, derivativeType.ContentType, cancellationToken).ConfigureAwait(false);

        return MediaIngestOutcome.Ingested(verdict.Type, byteSize, sha256, originalKey, derivativeKey, _clock.GetUtcNow());
    }
}
