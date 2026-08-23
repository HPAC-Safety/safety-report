
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// One aircraft involved in an occurrence, recorded exactly as the reporter
/// answered. Make and model are private context — kept for HPAC's trend analysis
/// and never published. Nothing in <c>Core</c> classifies, normalizes, or
/// otherwise mutates these values; the summarizer determines a publishable
/// certification class from <see cref="CertificationAnswer"/> at
/// summarization time, under the rules in <c>prompts/</c>. See
/// docs/aircraft-classification.md.
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
