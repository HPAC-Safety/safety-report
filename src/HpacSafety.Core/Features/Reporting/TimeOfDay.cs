using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>Coarse time of the occurrence. Deliberately coarse: an exact time
/// narrows a site and a day to one identifiable flight.</summary>
/// <remarks>
/// The reporter gives a real clock time (#68) and this is derived from it by
/// <see cref="TimeOfDayBuckets"/>. Reports carried over from Typeform were
/// answered on this same scale directly, so historical and derived values are
/// comparable — which is why the vocabulary did not change when the form did.
/// </remarks>
public enum TimeOfDay
{
    /// <summary>
    /// No time question was on the form at all. A defined state, never a zero
    /// that logic mistakes for midnight — see ADR-0016 on missing roles.
    /// </summary>
    NotAnswered = 0,

    /// <summary>Before 11:00, local wall clock.</summary>
    Morning = 1,

    /// <summary>From 11:00 up to but not including 14:00, local wall clock.</summary>
    MidDay = 2,

    /// <summary>From 14:00 up to but not including 17:00, local wall clock.</summary>
    Afternoon = 3,

    /// <summary>From 17:00 onwards, local wall clock.</summary>
    Evening = 4,

    /// <summary>
    /// The reporter filed without a time. #68 makes the time optional so that
    /// somebody who does not remember still files, and this is what that means
    /// — distinct from <see cref="NotAnswered"/>, and never a silent midnight.
    /// </summary>
    Unknown = 5,
}

/// <summary>
/// Where a clock time becomes a <see cref="TimeOfDay"/>, and the only place the
/// boundaries are written down.
/// </summary>
/// <remarks>
/// <para>
/// The reporter submits an actual date and time; the coarse bucket is derived,
/// never asked for. Both the projection onto <see cref="Report"/> and the
/// report projection and summarization input preparation call this, so "when does the afternoon start"
/// has exactly one answer in this system rather than one per caller.
/// </para>
/// <para>
/// The time is a <b>local wall clock</b> reading, because "morning" is what the
/// clock at the site said. See ADR-0019.
/// </para>
/// </remarks>
public static class TimeOfDayBuckets
{
    /// <summary>The first moment that is no longer <see cref="TimeOfDay.Morning"/>.</summary>
    public static readonly TimeOnly MidDayStarts = new(11, 0);

    /// <summary>The first moment that is no longer <see cref="TimeOfDay.MidDay"/>.</summary>
    public static readonly TimeOnly AfternoonStarts = new(14, 0);

    /// <summary>The first moment that is no longer <see cref="TimeOfDay.Afternoon"/>.</summary>
    public static readonly TimeOnly EveningStarts = new(17, 0);

    extension(TimeOfDay)
    {
        /// <summary>
        /// The bucket a local wall-clock time falls in. Always one of the four
        /// real ones — a time that was given is never <c>Unknown</c>.
        /// </summary>
        /// <param name="localTime">The clock reading at the site.</param>
        public static TimeOfDay FromLocalTime(TimeOnly localTime) =>
            localTime < MidDayStarts ? TimeOfDay.Morning
            : localTime < AfternoonStarts ? TimeOfDay.MidDay
            : localTime < EveningStarts ? TimeOfDay.Afternoon
            : TimeOfDay.Evening;

        /// <summary>
        /// The bucket a local wall-clock time falls in, for a time that may not
        /// have been given. Absent is <see cref="TimeOfDay.Unknown"/> — the
        /// reporter filed without one — and never midnight.
        /// </summary>
        /// <param name="localTime">The clock reading at the site, if there is one.</param>
        public static TimeOfDay FromLocalTime(TimeOnly? localTime) =>
            localTime is { } given ? TimeOfDay.FromLocalTime(given) : TimeOfDay.Unknown;
    }
}
