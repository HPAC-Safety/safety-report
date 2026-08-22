namespace HpacSafety.Core.Enums;

/// <summary>Canadian provinces and territories. Publishable — a province is not
/// a site.</summary>
public enum Province
{
    NotAnswered = 0,
    NewfoundlandAndLabrador = 1,
    PrinceEdwardIsland = 2,
    NovaScotia = 3,
    NewBrunswick = 4,
    Quebec = 5,
    Ontario = 6,
    Manitoba = 7,
    Saskatchewan = 8,
    Alberta = 9,
    BritishColumbia = 10,
    Yukon = 11,
    NorthwestTerritories = 12,
    Nunavut = 13,

    /// <summary>The occurrence did not happen in Canada.</summary>
    OutsideCanada = 99,
}
