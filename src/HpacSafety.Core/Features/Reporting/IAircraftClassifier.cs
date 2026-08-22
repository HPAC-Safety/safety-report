namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// Normalizes the reporter's own certification answer against the published
/// vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// The reporter's answer is the <b>only</b> source. There is no model-to-class
/// lookup table in this system, and an implementation never infers a class from
/// a make, a model, a narrative, or a pilot's rating.
/// <see cref="AircraftClass.NotDetermined"/> is a valid result and a reviewer
/// may correct it by hand. See docs/aircraft-classification.md.
/// </para>
/// <para>
/// The operation is synchronous on purpose: an implementation that had to await
/// anything would be reaching for a model or a lookup service, and both are
/// forbidden here. See ADR-0029.
/// </para>
/// </remarks>
public interface IAircraftClassifier
{
    /// <summary>
    /// Normalizes a certification answer, returning
    /// <see cref="AircraftClass.NotDetermined"/> rather than a guess.
    /// </summary>
    /// <param name="certificationAnswer">The reporter's answer, verbatim.</param>
    /// <param name="discipline">The aircraft type the reporter chose.</param>
    AircraftClassification Classify(string? certificationAnswer, Discipline discipline);
}
