namespace HpacSafety.Core.Features.Reporting;

/// <summary>What sort of thing a reporter uploaded.</summary>
public enum MediaKind
{
    /// <summary>A photo.</summary>
    Image = 0,

    /// <summary>A video. Accepted and retained, but not yet strippable — see issue #65.</summary>
    Video = 1,
}
