using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// What ingest concluded about one uploaded file. It carries the facts a
/// <see cref="ReportFile" /> row is built from and nothing else — persistence is
/// the caller's job, not ingest's.
/// </summary>
public sealed class MediaIngestOutcome
{
    private readonly BlobKey _derivativeKey;

    private MediaIngestOutcome(
        bool isAccepted,
        MediaRejectionReason rejectionReason,
        MediaType contentType,
        long byteSize,
        string sha256,
        BlobKey derivativeKey,
        DateTimeOffset? strippedAt)
    {
        IsAccepted = isAccepted;
        RejectionReason = rejectionReason;
        ContentType = contentType;
        ByteSize = byteSize;
        Sha256 = sha256;
        _derivativeKey = derivativeKey;
        StrippedAt = strippedAt;
    }

    /// <summary>True when a stripped derivative exists and a reviewer may be shown it.</summary>
    public bool IsAccepted { get; }

    /// <summary>Why the upload was refused, or <see cref="MediaRejectionReason.None" />.</summary>
    public MediaRejectionReason RejectionReason { get; }

    /// <summary>The sniffed content type, never the declared one.</summary>
    public MediaType ContentType { get; }

    /// <summary>Size of the original in bytes.</summary>
    public long ByteSize { get; }

    /// <summary>Lowercase hex SHA-256 of the original bytes.</summary>
    public string Sha256 { get; }

    /// <summary>When the derivative was written.</summary>
    public DateTimeOffset? StrippedAt { get; }

    /// <summary>
    /// Where the stripped derivative lives. Reading this on a rejected outcome
    /// throws rather than returning a key: there is no derivative to show, and a
    /// caller that asks anyway has a bug worth failing loudly.
    /// </summary>
    public BlobKey DerivativeKey =>
        IsAccepted
            ? _derivativeKey
            : throw new DomainRuleViolationException("A rejected upload has no derivative for a reviewer to see.");

    /// <summary>The upload was refused.</summary>
    public static MediaIngestOutcome Rejected(MediaRejectionReason reason) =>
        new(false, reason, default, 0, string.Empty, default, null);

    /// <summary>The upload was accepted and a stripped derivative was written.</summary>
    public static MediaIngestOutcome Ingested(
        MediaType contentType,
        long byteSize,
        string sha256,
        BlobKey derivativeKey,
        DateTimeOffset strippedAt) =>
        new(true, MediaRejectionReason.None, contentType, byteSize, sha256, derivativeKey, strippedAt);
}
