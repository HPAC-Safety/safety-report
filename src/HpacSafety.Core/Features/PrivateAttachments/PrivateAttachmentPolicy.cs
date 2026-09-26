using System.Text.RegularExpressions;
using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Features.PrivateAttachments;

/// <summary>
///     What a staff private upload may be (ADR-0135): any type, above zero bytes and
///     at most <see cref="MaxByteSize" />. Separate from the reporter's
///     <see cref="MediaPolicy" />, because nothing downstream reads these bytes: they
///     are never sniffed, validated against an allowlist, or processed, only stored
///     and downloaded unchanged.
/// </summary>
public sealed partial class PrivateAttachmentPolicy
{
	/// <summary>The type signed for an upload whose declared type is empty or malformed.</summary>
	public const string FallbackContentType = "application/octet-stream";

	/// <summary>The longest content type kept, the width of the column that stores it.</summary>
	public const int ContentTypeMaxLength = 128;

	/// <summary>Creates the policy. The cap is explicit; there is no default here to inherit by accident.</summary>
	/// <param name="maxByteSize">The largest private attachment, in bytes.</param>
	public PrivateAttachmentPolicy(long maxByteSize)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxByteSize);
		MaxByteSize = maxByteSize;
	}

	/// <summary>The largest private attachment, in bytes.</summary>
	public long MaxByteSize { get; }

	/// <summary>Judges one size: above zero and within the cap.</summary>
	public MediaRejectionReason JudgeSize(long byteSize)
	{
		return byteSize <= 0
			? MediaRejectionReason.Empty
			: byteSize > MaxByteSize
				? MediaRejectionReason.TooLarge
				: MediaRejectionReason.None;
	}

	/// <summary>
	///     The type a private upload is signed and stored as: the declaration's
	///     essence, lower case and without parameters, when it is a well-formed
	///     <c>type/subtype</c>, and <see cref="FallbackContentType" /> otherwise.
	///     Any well-formed type is accepted; this only keeps a malformed one out of
	///     a signed header.
	/// </summary>
	public static string ContentTypeFor(string? declaredContentType)
	{
		if (string.IsNullOrWhiteSpace(declaredContentType))
		{
			return FallbackContentType;
		}

		var essence = UploadLink.Essence(declaredContentType);
		return essence.Length <= ContentTypeMaxLength && MimeType().IsMatch(essence) ? essence : FallbackContentType;
	}

	// RFC 9110 token characters on each side of one slash.
	[GeneratedRegex(@"^[a-z0-9!#$%&'*+.^_`|~-]+/[a-z0-9!#$%&'*+.^_`|~-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
	private static partial Regex MimeType();
}
