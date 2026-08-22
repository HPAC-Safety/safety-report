using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// An uploaded photo or video. The original bytes stay Restricted; the
/// EXIF-stripped derivative is what a reviewer sees, and media is never attached
/// to a published summary. See docs/data-handling.md.
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

    public ReportFile(Guid reportId, string blobKey, string contentType, long byteSize, DateTimeOffset uploadedAt)
    {
        Id = Guid.NewGuid();
        ReportId = reportId;
        BlobKey = blobKey;
        ContentType = contentType;
        ByteSize = byteSize;
        UploadedAt = uploadedAt;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>The report this file belongs to.</summary>
    public Guid ReportId { get; private init; }

    /// <summary>Key of the original bytes. Restricted.</summary>
    public string BlobKey { get; private init; }

    /// <summary>Key of the EXIF-stripped derivative a reviewer is shown.</summary>
    public string? StrippedBlobKey { get; private set; }

    /// <summary>Content type as sniffed on ingest, never as the client claimed.</summary>
    public string ContentType { get; private init; }

    /// <summary>Size in bytes.</summary>
    public long ByteSize { get; private init; }

    /// <summary>When it was uploaded.</summary>
    public DateTimeOffset UploadedAt { get; private init; }

    /// <summary>When EXIF — GPS above all — was stripped.</summary>
    public DateTimeOffset? ExifStrippedAt { get; private set; }

    /// <summary>True until the stripped derivative exists. A file is not viewable before then.</summary>
    public bool AwaitsStripping => ExifStrippedAt is null;

    /// <summary>Records the stripped derivative.</summary>
    public void RecordStripped(string strippedBlobKey, DateTimeOffset at)
    {
        StrippedBlobKey = strippedBlobKey;
        ExifStrippedAt = at;
    }
}
