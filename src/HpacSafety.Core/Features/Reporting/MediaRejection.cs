namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Maps a rejection to the localization key the edge renders it with.
/// <para>
/// This returns a <b>key</b>, never a sentence. English and French are both
/// first-class here, so the wording lives in <c>locales/en-CA.json</c> and its
/// generated French counterpart, and no user-facing string is written in
/// <c>Core</c>. See AGENTS.md and docs/localization.md.
/// </para>
/// </summary>
public static class MediaRejection
{
    /// <summary>The prefix every upload-rejection key shares.</summary>
    public const string KeyPrefix = "upload.rejected.";

    /// <summary>The localization key for one rejection reason.</summary>
    public static string LocalizationKeyFor(MediaRejectionReason reason) => reason switch
    {
        MediaRejectionReason.Empty => KeyPrefix + "empty",
        MediaRejectionReason.TooLarge => KeyPrefix + "tooLarge",
        MediaRejectionReason.UnrecognisedContent => KeyPrefix + "unrecognisedContent",
        MediaRejectionReason.UnacceptedMediaType => KeyPrefix + "unacceptedMediaType",
        MediaRejectionReason.DeclaredTypeMismatch => KeyPrefix + "declaredTypeMismatch",
        MediaRejectionReason.None => throw new ArgumentOutOfRangeException(
            nameof(reason), "An accepted upload has no rejection to render."),
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };
}
