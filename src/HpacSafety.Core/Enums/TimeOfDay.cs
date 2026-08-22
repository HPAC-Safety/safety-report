namespace HpacSafety.Core.Enums;

/// <summary>Coarse time of the occurrence. Deliberately coarse: an exact time
/// narrows a site and a day to one identifiable flight.</summary>
public enum TimeOfDay
{
    NotAnswered = 0,
    Morning = 1,
    MidDay = 2,
    Afternoon = 3,
    Evening = 4,
    Unknown = 5,
}
