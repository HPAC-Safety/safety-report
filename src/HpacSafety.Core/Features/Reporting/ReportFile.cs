using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// An uploaded attachment. The original bytes stay private; for an image or
/// video, the EXIF-stripped derivative is what a reviewer sees, and media is
/// never attached to a published summary. A document has no derivative at all —
/// it is validated, malware-checked, and kept private. See
/// docs/data-handling.md.
/// </summary>
public class ReportFile
{
    /// <summary>Records an upload that has landed in the private bucket.</summary>
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private ReportFile()
    {
    }
#pragma warning restore CS8618

    public ReportFile(TinyId reportId, string blobKey, string contentType, long byteSize, DateTimeOffset uploadedAt)
    {
        Id = TinyId.New();
        ReportId = reportId;
        BlobKey = blobKey;
        ContentType = contentType;
        Kind = MediaType.TryParse(contentType, out var mediaType) ? mediaType.Kind switch
        {
            MediaKind.Video => AttachmentKind.Video,
            _ => AttachmentKind.Image,
        }
        : AttachmentKind.Document;
        ByteSize = byteSize;
        UploadedAt = uploadedAt;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The report this file belongs to.</summary>
    public TinyId ReportId { get; private init; }

    /// <summary>
    /// The file-upload answer this attachment belongs to, once it is linked.
    /// Every attachment belongs to exactly one file-upload answer on the same
    /// report — the answer identifies the exact question revision asked.
    /// </summary>
    public TinyId? ReportAnswerId { get; private set; }

    /// <summary>Whether this is an image, a video, or a private document.</summary>
    public AttachmentKind Kind { get; private init; }

    /// <summary>Key of the private original bytes.</summary>
    public string BlobKey { get; private init; }

    /// <summary>Key of the EXIF-stripped derivative a reviewer is shown. Documents never have one.</summary>
    public string? StrippedBlobKey { get; private set; }

    /// <summary>Content type as sniffed on ingest, never as the client claimed.</summary>
    public string ContentType { get; private init; }

    /// <summary>Size in bytes.</summary>
    public long ByteSize { get; private init; }

    /// <summary>When it was uploaded.</summary>
    public DateTimeOffset UploadedAt { get; private init; }

    /// <summary>When EXIF — GPS above all — was stripped.</summary>
    public DateTimeOffset? ExifStrippedAt { get; private set; }

    /// <summary>A safe, non-content error code recorded when processing this file failed.</summary>
    public string? ProcessingErrorCode { get; private set; }

    /// <summary>When this file was deleted along with its report, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>
    /// True until a stripped derivative exists. A file is not viewable before
    /// then — and a video has no derivative at all yet, so it stays true. See
    /// issue #65.
    /// <para>
    /// Both fields are checked, not just the timestamp: a row carrying a
    /// stripped-at time with no key would otherwise read as viewable.
    /// </para>
    /// </summary>
    public bool AwaitsStripping => ExifStrippedAt is null || StrippedBlobKey is null;

    /// <summary>
    /// The key of the only bytes a reviewer may be shown.
    /// <para>
    /// Reading this while <see cref="AwaitsStripping" /> throws rather than
    /// returning <see cref="BlobKey" />. Falling back to the original is the
    /// leak this whole feature exists to prevent, and a caller that asks for
    /// something to show when there is nothing safe to show has a bug worth
    /// failing loudly. It is the persisted counterpart of
    /// <see cref="MediaIngestOutcome.DerivativeKey" />.
    /// </para>
    /// </summary>
    // Qualified, because this entity has a string property named BlobKey that
    // shadows the type of the same name.
    public SharedKernel.BlobKey ViewableKey =>
        AwaitsStripping
            ? throw new DomainRuleViolationException("There is no stripped derivative for a reviewer to see.")
            : SharedKernel.BlobKey.Parse(StrippedBlobKey);

    /// <summary>Records the stripped derivative. Both facts are recorded together or not at all.</summary>
    public void RecordStripped(string strippedBlobKey, DateTimeOffset at)
    {
        var parsed = SharedKernel.BlobKey.Parse(strippedBlobKey);

        if (parsed.Compartment is not MediaCompartment.Stripped)
        {
            throw new DomainRuleViolationException("A derivative must live in the stripped compartment.");
        }

        StrippedBlobKey = parsed.Value;
        ExifStrippedAt = at;
    }

    /// <summary>Links this attachment to the file-upload answer it was submitted with.</summary>
    public void LinkToAnswer(TinyId reportAnswerId) => ReportAnswerId = reportAnswerId;

    /// <summary>Records that processing this file failed, with a safe non-content code.</summary>
    public void RecordProcessingFailure(string errorCode) => ProcessingErrorCode = errorCode;
}
