using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     One row of the <c>public_report_media</c> view: an image or video a
///     published report shows, or a document it offers for download (ADR-0117,
///     ADR-0119). The view, not this type, decides what is public; a file that
///     stops being public simply has no row.
/// </summary>
public sealed class PublicReportMedia
{
	/// <summary>The file's opaque identifier.</summary>
	public string Id { get; private init; } = string.Empty;

	/// <summary>The published report it belongs to.</summary>
	public string ReportId { get; private init; } = string.Empty;

	/// <summary>Image, video, or document.</summary>
	public AttachmentKind Kind { get; private init; }

	/// <summary>
	///     The original's validated type: the derivative's is derived from it, and a
	///     document's coarse format is read from it. Never returned.
	/// </summary>
	public string ContentType { get; private init; } = string.Empty;

	/// <summary>An image or video's verified derivative key, for minting a link. Null for a document. Never returned.</summary>
	public string? StrippedBlobKey { get; private init; }

	/// <summary>A document's unchanged original key, for minting a download. Null for an image or video. Never returned.</summary>
	public string? DocumentBlobKey { get; private init; }

	/// <summary>When it was attached, which orders a report's media.</summary>
	public DateTimeOffset UploadedAt { get; private init; }
}
