
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// One aircraft involved in an occurrence, recorded exactly as the reporter
/// answered. Make and model are Internal — kept for HPAC's own trend analysis
/// and never published. Nothing in <c>Core</c> classifies, normalizes, or
/// otherwise mutates these values; the summarizer determines a publishable
/// certification class from <see cref="CertificationAnswer"/> at
/// summarization time, under the rules in <c>prompts/</c>. See
/// docs/aircraft-classification.md.
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

    /// <summary>The reporter's certification answer, verbatim. Never mutated.</summary>
    public string? CertificationAnswer { get; private set; }
}
