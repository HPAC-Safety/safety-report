using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     One row of the <c>public_report_media</c> view: an image or video a
///     published report shows (ADR-0117). The view, not this type, decides what is
///     public; a file that stops being public simply has no row.
/// </summary>
public sealed class PublicReportMedia
{
	/// <summary>The file's opaque identifier.</summary>
	public string Id { get; private init; } = string.Empty;

	/// <summary>The published report it belongs to.</summary>
	public string ReportId { get; private init; } = string.Empty;

	/// <summary>Image or video — never a document.</summary>
	public AttachmentKind Kind { get; private init; }

	/// <summary>The original's validated type, from which the derivative's is derived. Never returned.</summary>
	public string ContentType { get; private init; } = string.Empty;

	/// <summary>The verified derivative's key, for minting a link. Never returned.</summary>
	public string StrippedBlobKey { get; private init; } = string.Empty;

	/// <summary>When it was attached, which orders a report's media.</summary>
	public DateTimeOffset UploadedAt { get; private init; }
}
