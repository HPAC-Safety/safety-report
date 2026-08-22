using HpacSafety.Core.Enums;

namespace HpacSafety.Core.Reports;

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

    /// <summary>Records the normalized class.</summary>
    public void Classify(AircraftClass aircraftClass) => Class = aircraftClass;
}
