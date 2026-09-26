using HpacSafety.Core.Features.PrivateAttachments;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
///     The configured cap on a staff private attachment, bound from
///     <c>HpacSafety:Media:PrivateAttachments</c> (ADR-0135). Separate from the
///     reporter caps in <see cref="MediaPolicyOptions" />: changing one never moves
///     the other, and changing this changes the cap with no code change.
/// </summary>
public sealed class PrivateAttachmentOptions
{
	/// <summary>The configuration section these options bind from.</summary>
	public const string SectionName = "HpacSafety:Media:PrivateAttachments";

	/// <summary>The default largest private attachment: 1 GB.</summary>
	public const long DefaultMaxByteSize = 1024L * 1024 * 1024;

	/// <summary>The largest private attachment this deployment accepts, in bytes.</summary>
	public long MaxByteSize { get; set; } = DefaultMaxByteSize;

	/// <summary>Builds the domain policy this deployment runs with.</summary>
	public PrivateAttachmentPolicy ToPolicy()
	{
		return new PrivateAttachmentPolicy(MaxByteSize);
	}
}
