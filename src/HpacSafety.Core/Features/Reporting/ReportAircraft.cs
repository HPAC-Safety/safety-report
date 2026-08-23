
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Legacy typed aircraft record from the audited schema. The target model stores
/// these values as ordinary revision-bound answers with no specialized
/// processing; issue #79 removes this projection.
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

    /// <summary>Private context. Never published.</summary>
    public string? Manufacturer { get; private set; }

    /// <summary>Private context. Never published.</summary>
    public string? Model { get; private set; }

    /// <summary>The reporter's certification answer, verbatim. Never mutated.</summary>
    public string? CertificationAnswer { get; private set; }
}
