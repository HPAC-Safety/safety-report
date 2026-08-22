
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// One aircraft involved in an occurrence. Make and model are Internal — kept
/// for HPAC's own trend analysis and never published. Only
/// <see cref="Class"/> is publishable, and it comes from the reporter's own
/// answer: nothing in this system infers a class from a model name.
/// </summary>
public class ReportAircraft
{
    /// <summary>Creates an aircraft record from what the reporter answered.</summary>
    public ReportAircraft(Guid reportId, Discipline discipline, string? manufacturer, string? model, string? certificationAnswer)
    {
        Id = Guid.NewGuid();
        ReportId = reportId;
        Discipline = discipline;
        Manufacturer = manufacturer;
        Model = model;
        CertificationAnswer = certificationAnswer;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>The report this aircraft belongs to.</summary>
    public Guid ReportId { get; private init; }

    /// <summary>What kind of aircraft it is.</summary>
    public Discipline Discipline { get; private set; }

    /// <summary>Internal tier. Never published.</summary>
    public string? Manufacturer { get; private set; }

    /// <summary>Internal tier. Never published.</summary>
    public string? Model { get; private set; }

    /// <summary>The reporter's certification answer, verbatim, before normalization.</summary>
    public string? CertificationAnswer { get; private set; }

    /// <summary>
    /// The published class. <see cref="AircraftClass.NotDetermined"/> until an
    /// <c>IAircraftClassifier</c> normalizes the reporter's answer, and a valid
    /// end state — a reviewer may correct it by hand, but nothing guesses it.
    /// </summary>
    public AircraftClass Class { get; private set; } = AircraftClass.NotDetermined;

    /// <summary>
    /// Qualifiers the reporter's answer carried alongside the class — tandem,
    /// mini wing, speedwing. A tandem is still a high EN-B, so the marker
    /// accompanies the class rather than replacing it. See ADR-0030.
    /// </summary>
    public AircraftMarker Markers { get; private set; } = AircraftMarker.None;

    /// <summary>The class and its markers, as one value.</summary>
    public AircraftClassification Classification => new(Class, Markers);

    /// <summary>
    /// Records what the reporter's answer normalized to. Also how a reviewer
    /// corrects a class by hand — the one other thing allowed to set it.
    /// </summary>
    public void Classify(AircraftClassification classification)
    {
        ArgumentNullException.ThrowIfNull(classification);

        Class = classification.Class;
        Markers = classification.Markers;
    }

    /// <summary>Records the normalized class, leaving its markers unchanged.</summary>
    public void Classify(AircraftClass aircraftClass) => Class = aircraftClass;
}
