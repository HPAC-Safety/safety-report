namespace HpacSafety.Core.Enums;

/// <summary>
/// Injury severity as the reporting form asks it. <see cref="Serious"/> and
/// <see cref="Fatality"/> escalate notification.
/// </summary>
public enum InjurySeverity
{
    NotAnswered = 0,
    None = 1,
    Minor = 2,
    Serious = 3,
    Fatality = 4,
    Unknown = 5,
}
