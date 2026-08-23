namespace HpacSafety.Core.Features.Reporting;

/// <summary>The verdict on one uploaded file.</summary>
public readonly record struct MediaValidation
{
    private MediaValidation(bool isAccepted, MediaRejectionReason reason, MediaType type)
    {
        IsAccepted = isAccepted;
        RejectionReason = reason;
        Type = type;
    }

    /// <summary>True when the file may be ingested.</summary>
    public bool IsAccepted { get; }

    /// <summary>Why it was refused, or <see cref="MediaRejectionReason.None" />.</summary>
    public MediaRejectionReason RejectionReason { get; }

    /// <summary>The sniffed type. Meaningful only when accepted.</summary>
    public MediaType Type { get; }

    /// <summary>The file may be ingested, as the sniffed type.</summary>
    public static MediaValidation Accepted(MediaType type) => new(true, MediaRejectionReason.None, type);

    /// <summary>The file is refused.</summary>
    public static MediaValidation Rejected(MediaRejectionReason reason) => new(false, reason, default);
}
