using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
///     The configured upload limits for a deployment.
///     <para>
///         <see cref="MediaPolicy" /> itself takes its maximum as a constructor argument
///         with no default, deliberately: a size limit nobody chose is a size limit
///         nobody owns. This is where the number HPAC chose lives.
///     </para>
/// </summary>
public sealed class MediaPolicyOptions
{
	/// <summary>
	///     The configurable 50 MB default applied independently to every accepted
	///     attachment format.
	/// </summary>
	public const long DefaultMaxByteSize = 50L * 1024 * 1024;

	/// <summary>
	///     The configurable default count of attachments a single report may carry.
	/// </summary>
	public const int DefaultMaxAttachmentCount = 5;

	/// <summary>The largest upload this deployment accepts, in bytes.</summary>
	public long MaxByteSize { get; set; } = DefaultMaxByteSize;

	/// <summary>
	///     The most attachments one submission may carry. A request-level bound —
	///     <see cref="MediaPolicy" /> judges one file at a time and knows nothing
	///     about how many its report has.
	/// </summary>
	public int MaxAttachmentCount { get; set; } = DefaultMaxAttachmentCount;

	/// <summary>Builds the domain policy this deployment runs with.</summary>
	public MediaPolicy ToPolicy()
	{
		return new MediaPolicy(MaxByteSize, MediaType.All);
	}
}
