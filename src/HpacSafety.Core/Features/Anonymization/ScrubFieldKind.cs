namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// What the deterministic scrub does with a field. The kind describes the
/// <b>handling</b> a field needs, which is not the same thing as its sensitivity
/// tier: a launch site and a manufacturer are both Internal, yet one is
/// generalized to a province and the other is discarded outright.
/// </summary>
/// <remarks>
/// The default is <see cref="Other"/> — kept, but passed through every
/// identifier stage — so a field nobody has classified is still scrubbed. It is
/// never dropped silently, because a field that disappears without anyone
/// deciding it should is a lost safety lesson.
/// </remarks>
public enum ScrubFieldKind
{
    /// <summary>Free text with no special handling. Kept and scrubbed.</summary>
    Other = 0,

    /// <summary>The reporter's name. Dropped, and its tokens become "the reporter" in free text.</summary>
    ReporterName = 1,

    /// <summary>The pilot in command's name. Dropped, and its tokens become "the pilot" in free text.</summary>
    PilotName = 2,

    /// <summary>Phone, email, address, social handle. Dropped outright.</summary>
    ContactDetail = 3,

    /// <summary>HPAC member number, licence number, insurance number. Dropped outright.</summary>
    MemberIdentifier = 4,

    /// <summary>A launch, landing zone, club, or named landmark. Generalized to the province.</summary>
    Location = 5,

    /// <summary>Manufacturer, model, colour, serial. Dropped outright; the class comes from elsewhere.</summary>
    AircraftIdentity = 6,

    /// <summary>The reporter's own account. Kept and scrubbed — it is the safety lesson.</summary>
    Narrative = 7,
}
