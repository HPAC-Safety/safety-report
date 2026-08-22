namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// What the scrub leaves behind where it removed something that has no role
/// word — a phone number, an email address, a URL, a member number, a launch
/// name, an aircraft model.
/// </summary>
/// <remarks>
/// A marker rather than nothing at all, for two reasons. It keeps the sentence
/// grammatical, so stage 2 is summarizing prose rather than fragments; and it is
/// visible, so a reviewer reading a scrubbed report can see that something was
/// taken out rather than wondering whether the reporter simply never said. It
/// carries no locale, which is why it is a bracketed token and not a word.
/// </remarks>
public static class ScrubMarker
{
    /// <summary>The marker itself.</summary>
    public const string Removed = "[removed]";
}
