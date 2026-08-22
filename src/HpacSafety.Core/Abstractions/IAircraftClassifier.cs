using HpacSafety.Core.Enums;

namespace HpacSafety.Core.Abstractions;

/// <summary>
/// Normalizes the reporter's own certification answer against the published
/// vocabulary.
/// </summary>
/// <remarks>
/// The reporter's answer is the <b>only</b> source. There is no model-to-class
/// lookup table in this system, and an implementation never infers a class from
/// a make or model. <see cref="AircraftClass.NotDetermined"/> is a valid result
/// and a reviewer may correct it by hand. See docs/aircraft-classification.md.
/// </remarks>
public interface IAircraftClassifier
{
    /// <summary>Normalizes a certification answer, or returns
    /// <see cref="AircraftClass.NotDetermined"/>.</summary>
    Task<AircraftClass> ClassifyAsync(string? certificationAnswer, Discipline discipline, CancellationToken cancellationToken);
}
