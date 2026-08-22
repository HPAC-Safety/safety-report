using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// Narrows a precise occurrence date and time to the coarse forms that may be
/// published: month and year, and a <see cref="TimeOfDay"/> bucket.
/// </summary>
/// <remarks>
/// <para>
/// The reporter now submits an actual date and time and the anonymizer derives
/// the bucket, rather than the reporter picking one. That is strictly better
/// data for HPAC's own analysis and strictly more dangerous to publish: a
/// precise time, plus a province, plus an aircraft type is another aggregation
/// that identifies one person even though no field alone does. The same reason
/// the date is narrowed to month and year applies to the clock.
/// </para>
/// <para>
/// <b>An absent time is a defined state, never a zero.</b> The time input is
/// optional — a reporter who does not remember still files — and a missing time
/// yields <see cref="TimeOfDay.Unknown"/>. Treating <c>default(TimeOnly)</c> as
/// midnight and publishing "Morning" would be a fabricated fact in a summary
/// about a real crash.
/// </para>
/// <para>
/// <b>Provisional:</b> the boundaries live here only until the schema work that
/// owns <see cref="TimeOfDay"/> ships its own mapping from a wall-clock time.
/// When it does, this delegates to that and stops carrying its own copy — a
/// second copy of a boundary is a boundary that will drift.
/// </para>
/// </remarks>
public static class OccurrenceNarrowing
{
    /// <summary>Morning runs up to this hour.</summary>
    private static readonly TimeOnly MidDayStart = new(11, 0);

    /// <summary>Mid-day runs up to this hour.</summary>
    private static readonly TimeOnly AfternoonStart = new(14, 0);

    /// <summary>Afternoon runs up to this hour; everything later is evening.</summary>
    private static readonly TimeOnly EveningStart = new(17, 0);

    /// <summary>
    /// The bucket a wall-clock time falls in, or <see cref="TimeOfDay.Unknown"/>
    /// when no time was given.
    /// </summary>
    public static TimeOfDay Bucket(TimeOnly? occurredAt) => occurredAt switch
    {
        null => TimeOfDay.Unknown,
        var time when time.Value < MidDayStart => TimeOfDay.Morning,
        var time when time.Value < AfternoonStart => TimeOfDay.MidDay,
        var time when time.Value < EveningStart => TimeOfDay.Afternoon,
        _ => TimeOfDay.Evening,
    };

    /// <summary>
    /// The month and year of an occurrence, or null when no date was given. The
    /// form is <c>yyyy-MM</c> and carries no locale, for the same reason the
    /// province is written as an invariant code: rendering "March 2026" or
    /// "mars 2026" is the edge's job, not the scrub's.
    /// </summary>
    public static string? MonthAndYear(DateOnly? occurredOn) =>
        occurredOn?.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
