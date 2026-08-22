
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
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
    private ReportAircraft()
    {
    }

    public ReportAircraft(TinyId reportId, Discipline discipline, string? manufacturer, string? model, string? certificationAnswer)
    {
        Id = TinyId.New();
        ReportId = reportId;
        Discipline = discipline;
        Manufacturer = manufacturer;
        Model = model;
        CertificationAnswer = certificationAnswer;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>The report this aircraft belongs to.</summary>
    public TinyId ReportId { get; private init; }

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
