namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// What this deployment accepts as an upload, and the order the checks run in.
/// <para>
/// The client's <c>Content-Type</c> is evidence, never authority: the sniffed
/// type decides, and a file claiming one format while containing another is
/// refused outright rather than quietly reclassified. A mismatch is a signal,
/// and silently accepting it would throw the signal away.
/// </para>
/// </summary>
public sealed class MediaPolicy
{
    /// <summary>Creates a policy. Both limits are explicit; there is no default size here to inherit by accident.</summary>
    public MediaPolicy(long maxByteSize, IReadOnlyCollection<MediaType> acceptedTypes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxByteSize);
        ArgumentNullException.ThrowIfNull(acceptedTypes);

        MaxByteSize = maxByteSize;
        AcceptedTypes = acceptedTypes;
    }

    /// <summary>The largest upload this deployment accepts, in bytes.</summary>
    public long MaxByteSize { get; }

    /// <summary>The formats this deployment accepts.</summary>
    public IReadOnlyCollection<MediaType> AcceptedTypes { get; }

    /// <summary>Judges one upload against the policy.</summary>
    public MediaValidation Validate(string? declaredContentType, MediaType? sniffed, long byteSize)
    {
        if (byteSize <= 0)
        {
            return MediaValidation.Rejected(MediaRejectionReason.Empty);
        }

        if (byteSize > MaxByteSize)
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

        if (!MediaType.TryParse(declaredContentType, out var declared) || declared != actual)
        {
            return MediaValidation.Rejected(MediaRejectionReason.DeclaredTypeMismatch);
        }

        return MediaValidation.Accepted(actual);
    }
}
