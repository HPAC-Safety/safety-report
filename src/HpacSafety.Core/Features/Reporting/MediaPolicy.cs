namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     What this deployment accepts as an upload, and the order the checks run in.
///     <para>
///         The client's <c>Content-Type</c> is evidence, never authority: the sniffed
///         type decides, and a file claiming one format while containing another is
///         refused outright rather than quietly reclassified. A mismatch is a signal,
///         and silently accepting it would throw the signal away.
///     </para>
///     <para>
///         A file is judged twice (ADR-0126). <see cref="JudgeDeclaration" /> runs
///         before an upload URL is minted, on nothing but what the browser declares.
///         <see cref="Validate" /> runs once the bytes exist, and holds the real size
///         to the limit of the kind the bytes <i>are</i>, not the kind they were
///         declared as.
///     </para>
/// </summary>
public sealed class MediaPolicy
{
	/// <summary>Creates a policy. Every limit is explicit; there is no default size here to inherit by accident.</summary>
	public MediaPolicy(MediaSizeLimits limits,
					   IReadOnlyCollection<MediaType> acceptedTypes)
	{
		ArgumentNullException.ThrowIfNull(limits);
		ArgumentNullException.ThrowIfNull(acceptedTypes);

		Limits = limits;
		AcceptedTypes = acceptedTypes;
	}

	/// <summary>The largest upload this deployment accepts of each kind, in bytes.</summary>
	public MediaSizeLimits Limits { get; }

	/// <summary>The formats this deployment accepts.</summary>
	public IReadOnlyCollection<MediaType> AcceptedTypes { get; }

	/// <summary>
	///     Judges what a browser says it is about to upload, before anything exists to
	///     sniff: an accepted type, and a size above zero and within that type's kind.
	/// </summary>
	/// <param name="declaredContentType">The browser's declared type — evidence, checked again at claim.</param>
	/// <param name="byteSize">The exact size the browser declared.</param>
	public MediaValidation JudgeDeclaration(string? declaredContentType,
											long byteSize)
	{
		if (!MediaType.TryParse(declaredContentType, out var declared)
			|| !AcceptedTypes.Contains(declared))
		{
			return MediaValidation.Rejected(MediaRejectionReason.UnacceptedMediaType);
		}

		if (byteSize <= 0)
		{
			return MediaValidation.Rejected(MediaRejectionReason.Empty);
		}

		return byteSize > Limits.For(declared.Kind)
			? MediaValidation.Rejected(MediaRejectionReason.TooLarge)
			: MediaValidation.Accepted(declared);
	}

	/// <summary>Judges one stored file against the policy.</summary>
	public MediaValidation Validate(string? declaredContentType,
									MediaType? sniffed,
									long byteSize)
	{
		if (byteSize <= 0)
		{
			return MediaValidation.Rejected(MediaRejectionReason.Empty);
		}

		if (byteSize > Limits.Largest)
		{
			return MediaValidation.Rejected(MediaRejectionReason.TooLarge);
		}

		if (sniffed is not { } actual)
		{
			return MediaValidation.Rejected(MediaRejectionReason.UnrecognisedContent);
		}

		if (!AcceptedTypes.Contains(actual))
		{
			return MediaValidation.Rejected(MediaRejectionReason.UnacceptedMediaType);
		}

		// The detected kind's limit, before the declaration is compared: a file
		// declared as a video but really a 30 MB image is too large for what it
		// is, whatever it claimed to be (REQ-SUB-075).
		if (byteSize > Limits.For(actual.Kind))
		{
			return MediaValidation.Rejected(MediaRejectionReason.TooLarge);
		}

		if (!MediaType.TryParse(declaredContentType, out var declared)
			|| declared != actual)
		{
			return MediaValidation.Rejected(MediaRejectionReason.DeclaredTypeMismatch);
		}

		return MediaValidation.Accepted(actual);
	}
}
