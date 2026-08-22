using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// What ingest concluded about one uploaded file. It carries the facts a
/// <see cref="ReportFile" /> row is built from and nothing else — persistence is
/// the caller's job, not ingest's.
/// </summary>
public sealed class MediaIngestOutcome
{
    private readonly BlobKey _originalKey;
    private readonly BlobKey _derivativeKey;

    private MediaIngestOutcome(
        MediaIngestStatus status,
        MediaRejectionReason rejectionReason,
        MediaType contentType,
        long byteSize,
        string sha256,
        BlobKey originalKey,
        BlobKey derivativeKey,
        DateTimeOffset? strippedAt)
    {
        Status = status;
        RejectionReason = rejectionReason;
        ContentType = contentType;
        ByteSize = byteSize;
        Sha256 = sha256;
        _originalKey = originalKey;
        _derivativeKey = derivativeKey;
        StrippedAt = strippedAt;
    }

    /// <summary>What ingest concluded.</summary>
    public MediaIngestStatus Status { get; }

    /// <summary>True when the bytes were retained as the Restricted record.</summary>
    public bool IsAccepted => Status is not MediaIngestStatus.Rejected;

    /// <summary>True when the file is accepted but has no derivative yet, so nothing is viewable.</summary>
    public bool AwaitsStripping => Status is MediaIngestStatus.AwaitingStripping;

    /// <summary>True when a reviewer may be shown this file.</summary>
    public bool IsViewable => Status is MediaIngestStatus.Stripped;

    /// <summary>Why the upload was refused, or <see cref="MediaRejectionReason.None" />.</summary>
    public MediaRejectionReason RejectionReason { get; }

    /// <summary>The sniffed content type, never the declared one.</summary>
    public MediaType ContentType { get; }

    /// <summary>Size of the original in bytes.</summary>
    public long ByteSize { get; }

    /// <summary>Lowercase hex SHA-256 of the original bytes.</summary>
    public string Sha256 { get; }

    /// <summary>When the derivative was written, or <see langword="null" /> when there is none.</summary>
    public DateTimeOffset? StrippedAt { get; }

    /// <summary>Where the Restricted original was promoted to. Throws on a rejection.</summary>
    public BlobKey OriginalKey =>
        IsAccepted
            ? _originalKey
            : throw new DomainRuleViolationException("A refused upload was never promoted out of quarantine.");

    /// <summary>
    /// Where the stripped derivative lives. Reading this on anything but
    /// <see cref="MediaIngestStatus.Stripped" /> throws rather than returning a
    /// key — including for an accepted video, which has no derivative until #65.
    /// A caller that asks for something to show a reviewer when there is nothing
    /// safe to show has a bug worth failing loudly, and falling back to the
    /// original would be the leak.
    /// </summary>
    public BlobKey DerivativeKey =>
        IsViewable
            ? _derivativeKey
            : throw new DomainRuleViolationException("There is no stripped derivative for a reviewer to see.");

    /// <summary>The upload was refused. Its bytes stay in quarantine and expire.</summary>
    public static MediaIngestOutcome Rejected(MediaRejectionReason reason) =>
        new(MediaIngestStatus.Rejected, reason, default, 0, string.Empty, default, default, null);

    /// <summary>The upload was retained, but this system cannot strip the format yet.</summary>
    public static MediaIngestOutcome Retained(
        MediaType contentType,
        long byteSize,
        string sha256,
        BlobKey originalKey) =>
        new(MediaIngestStatus.AwaitingStripping, MediaRejectionReason.None, contentType, byteSize, sha256, originalKey, default, null);

    /// <summary>The upload was retained and a stripped derivative was written.</summary>
    public static MediaIngestOutcome Ingested(
        MediaType contentType,
        long byteSize,
        string sha256,
        BlobKey originalKey,
        BlobKey derivativeKey,
        DateTimeOffset strippedAt) =>
        new(MediaIngestStatus.Stripped, MediaRejectionReason.None, contentType, byteSize, sha256, originalKey, derivativeKey, strippedAt);
}
