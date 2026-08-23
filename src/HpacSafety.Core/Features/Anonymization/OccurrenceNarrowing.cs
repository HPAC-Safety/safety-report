namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// Narrows a precise occurrence date to the coarse form that may be published.
/// </summary>
/// <remarks>
/// The time of day is narrowed too, but <b>the boundaries are deliberately not
/// here</b>. See <c>ScrubRequest.TimeOfDay</c>.
/// </remarks>
public static class OccurrenceNarrowing
{
    /// <summary>
    /// The month and year of an occurrence, or null when no date was given. The
    /// form is <c>yyyy-MM</c> and carries no locale, for the same reason the
    /// province is written as an invariant code: rendering "March 2026" or
    /// "mars 2026" is the edge's job, not the scrub's.
    /// </summary>
    public static string? MonthAndYear(DateOnly? occurredOn) =>
        occurredOn?.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
