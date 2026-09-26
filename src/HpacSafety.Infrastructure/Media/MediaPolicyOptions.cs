using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
///     The configured upload limits for a deployment, bound from
///     <c>HpacSafety:Media:Policy</c>.
///     <para>
///         <see cref="MediaPolicy" /> itself takes its limits as a constructor argument
///         with no default, deliberately: a size limit nobody chose is a size limit
///         nobody owns. This is where the numbers HPAC chose live — one per kind
///         (ADR-0126).
///     </para>
/// </summary>
public sealed class MediaPolicyOptions
{
	/// <summary>The configuration section these options bind from.</summary>
	public const string SectionName = "HpacSafety:Media:Policy";

	/// <summary>
	///     The single limit ADR-0126 replaced. A deployment still setting it would
	///     believe it had a limit it no longer has, so it refuses to start instead.
	/// </summary>
	public const string RetiredMaxByteSizeKey = "MaxByteSize";

	/// <summary>The default largest video: 250 MB.</summary>
	public const long DefaultMaxVideoByteSize = 250L * 1024 * 1024;

	/// <summary>The default largest image: 25 MB.</summary>
	public const long DefaultMaxImageByteSize = 25L * 1024 * 1024;

	/// <summary>The default largest document: 25 MB.</summary>
	public const long DefaultMaxDocumentByteSize = 25L * 1024 * 1024;

	/// <summary>
	///     The configurable default count of attachments a single report may carry.
	/// </summary>
	public const int DefaultMaxAttachmentCount = 5;

	/// <summary>The largest video this deployment accepts, in bytes.</summary>
	public long MaxVideoByteSize { get; set; } = DefaultMaxVideoByteSize;

	/// <summary>The largest image this deployment accepts, in bytes.</summary>
	public long MaxImageByteSize { get; set; } = DefaultMaxImageByteSize;

	/// <summary>The largest document this deployment accepts, in bytes.</summary>
	public long MaxDocumentByteSize { get; set; } = DefaultMaxDocumentByteSize;

	/// <summary>
	///     The most attachments one submission may carry. A request-level bound —
	///     <see cref="MediaPolicy" /> judges one file at a time and knows nothing
	///     about how many its report has.
	/// </summary>
	public int MaxAttachmentCount { get; set; } = DefaultMaxAttachmentCount;

	/// <summary>Builds the domain policy this deployment runs with.</summary>
	public MediaPolicy ToPolicy()
	{
		return new MediaPolicy(
			new MediaSizeLimits(MaxImageByteSize, MaxVideoByteSize, MaxDocumentByteSize),
			MediaType.All);
	}
}
