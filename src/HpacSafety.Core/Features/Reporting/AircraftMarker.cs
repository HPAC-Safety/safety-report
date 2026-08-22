namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Qualifiers a reporter's answer can carry <b>alongside</b> a certification
/// class — a tandem is still a high EN-B, and a mini wing may hold an EN class
/// of its own. Markers are read from the reporter's own answer and from the
/// aircraft type they chose; nothing else sets them.
/// </summary>
/// <remarks>
/// The corresponding members of <see cref="AircraftClass"/> —
/// <see cref="AircraftClass.TandemParaglider"/>,
/// <see cref="AircraftClass.TandemHangGlider"/>,
/// <see cref="AircraftClass.MiniWing"/> and
/// <see cref="AircraftClass.Speedwing"/> — stand in as the class only when no
/// certification class could be determined. See ADR-0030.
/// </remarks>
[Flags]
public enum AircraftMarker
{
    /// <summary>No qualifier.</summary>
    None = 0,

    /// <summary>A tandem wing. Carried with the class, never instead of it.</summary>
    Tandem = 1,

    /// <summary>A mini wing.</summary>
    MiniWing = 2,

    /// <summary>A speedwing, or a wing flown speedflying.</summary>
    Speedwing = 4,
}
