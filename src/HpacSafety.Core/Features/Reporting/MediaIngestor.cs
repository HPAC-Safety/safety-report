using System.Security.Cryptography;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Turns a file that has just landed in the private bucket into something a
/// reviewer may safely be shown.
/// <para>
/// The order is the point. Sniff, then validate, then strip, then write — and
/// nothing is written when validation fails, so a file this system could not
/// understand never acquires a derivative that looks reviewable. The original
/// bytes are never modified: they are the Restricted record, kept because a
/// summary can later be disputed. See docs/data-handling.md.
/// </para>
/// <para>
/// It lives in <c>Core</c> and depends only on ports, so the rule "a reviewer
/// only ever sees stripped bytes" is provable in a plain unit test with no
/// bucket, no database and no imaging library.
/// </para>
/// </summary>
public sealed class MediaIngestor
{
    /// <summary>The prefix the stripped derivative is stored under.</summary>
    public const string DerivativePrefix = "stripped";

    private readonly IBlobStore _blobStore;
    private readonly IMediaSniffer _sniffer;
    private readonly IExifStripper _stripper;
    private readonly MediaPolicy _policy;
    private readonly TimeProvider _clock;

    /// <summary>Creates an ingestor over the four ports it needs.</summary>
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
    /// Reads the original at <paramref name="originalKey" />, judges it, and on
    /// acceptance writes the stripped derivative alongside it.
    /// </summary>
    public async Task<MediaIngestOutcome> IngestAsync(
        BlobKey originalKey,
        string? declaredContentType,
        CancellationToken cancellationToken)
    {
        // Buffered rather than streamed: the bytes are read three times — for the
        // digest, for the sniff and for the strip — and MediaPolicy.MaxByteSize
        // is what bounds how much that costs.
        using var original = new MemoryStream();

        await using (var source = await _blobStore.OpenReadAsync(originalKey, cancellationToken).ConfigureAwait(false))
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

        original.Position = 0;
        using var stripped = new MemoryStream();
        await _stripper.StripAsync(original, stripped, verdict.Type, cancellationToken).ConfigureAwait(false);

        var derivativeKey = originalKey.WithPrefix(DerivativePrefix);
        stripped.Position = 0;
        await _blobStore.WriteAsync(derivativeKey, stripped, verdict.Type.ContentType, cancellationToken).ConfigureAwait(false);

        return MediaIngestOutcome.Ingested(
            verdict.Type,
            byteSize,
            Convert.ToHexStringLower(SHA256.HashData(original.GetBuffer().AsSpan(0, (int)byteSize))),
            derivativeKey,
            _clock.GetUtcNow());
    }
}
