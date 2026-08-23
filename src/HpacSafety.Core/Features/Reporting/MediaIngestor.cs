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
/// the private source record regardless, but it produces no derivative and the
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
    // Stream.CopyToAsync's own default. Reading in chunks this size is what
    // keeps the bound below tight rather than "the whole object, minus a
    // rounding error".
    private const int ReadBufferSize = 81920;

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
    /// the private source record — writing a stripped derivative alongside when the
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

        // Buffered rather than streamed past this point: the bytes are read three
        // more times - for the digest, for the sniff and for the strip - and
        // MediaPolicy.MaxByteSize is what bounds how much that costs.
        //
        // Getting the bytes into that buffer is a different question, and it is
        // the one a public upload endpoint cannot afford to get wrong: a
        // "download everything, then check Length" copy pulls an arbitrarily
        // large object fully into memory before an oversized upload is refused,
        // which is itself a denial-of-service surface. CopyBoundedAsync checks
        // the running total as bytes arrive and stops reading the source the
        // moment the limit is exceeded - the rest of an oversized object is
        // never requested at all.
        using var original = new MemoryStream();
        bool exceedsLimit;

        await using (var source = await _blobStore.OpenReadAsync(quarantineKey, cancellationToken).ConfigureAwait(false))
        {
            exceedsLimit = await CopyBoundedAsync(source, original, _policy.MaxByteSize, cancellationToken).ConfigureAwait(false);
        }

        if (exceedsLimit)
        {
            return MediaIngestOutcome.Rejected(MediaRejectionReason.TooLarge);
        }

        var byteSize = original.Length;

        if (byteSize <= 0)
        {
            return MediaIngestOutcome.Rejected(MediaRejectionReason.Empty);
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

    /// <summary>
    /// Copies <paramref name="source" /> into <paramref name="destination" />,
    /// stopping as soon as more than <paramref name="maxByteSize" /> bytes have
    /// been read rather than after the whole stream has been consumed.
    /// </summary>
    /// <returns><see langword="true" /> when the source exceeded the limit.</returns>
    private static async Task<bool> CopyBoundedAsync(
        Stream source,
        MemoryStream destination,
        long maxByteSize,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ReadBufferSize];
        long total = 0;

        while (true)
        {
            // maxByteSize + 1: a file of exactly the limit must still succeed,
            // and reading one byte past it is enough to know the limit was
            // exceeded without reading a whole extra chunk to find out.
            var toRead = (int)Math.Min(buffer.Length, maxByteSize + 1 - total);

            if (toRead <= 0)
            {
                return true;
            }

            var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return false;
            }

            total += read;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

}
