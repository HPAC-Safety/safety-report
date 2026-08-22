using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Features.Anonymization;

/// <summary>
/// A report presented to the deterministic scrub: its fields, and the province
/// that a site is generalized to.
/// </summary>
public sealed record ScrubRequest
{
    /// <summary>Every field of the report, in the order they should be read.</summary>
    public required IReadOnlyList<ScrubField> Fields { get; init; }

    /// <summary>
    /// The province from the reporter's own structured answer. It is the region
    /// a <see cref="ScrubFieldKind.Location"/> field is generalized to; the
    /// scrub never derives a province from a site name, because doing so would
    /// be inferring a location rather than reading one.
    /// </summary>
    public Province Province { get; init; } = Province.NotAnswered;

    /// <summary>
    /// The date of the occurrence, if one was given. Narrowed to month and year
    /// — never published precisely.
    /// </summary>
    public DateOnly? OccurredOn { get; init; }

    /// <summary>
    /// The coarse time-of-day bucket for the occurrence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>bucket</b>, not the clock time, and that is the point. The
    /// reporter submits an actual wall-clock time and it is stored encrypted as
    /// Restricted data; deriving the bucket from it belongs to the reporting
    /// feature, which owns the boundaries. Stage 1 is handed the answer the same
    /// way it is handed the province, and its job is the invariant rather than
    /// the arithmetic: <b>whatever the field said, only the bucket travels
    /// onward.</b>
    /// </para>
    /// <para>
    /// Copying the boundaries here would put a second definition of "morning"
    /// in the codebase, and a drifted boundary publishes the wrong time of day
    /// about a real crash. When the reporting feature's
    /// <c>TimeOfDay.FromLocalTime</c> lands, the caller calls it and passes the
    /// result in; nothing here changes.
    /// </para>
    /// <para>
    /// The three empty-ish states are <b>not</b> interchangeable and stage 1
    /// must not flatten them:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>Unknown</c> — the form asked and the reporter did not answer. A
    ///     defined answer, published as such.
    ///   </description></item>
    ///   <item><description>
    ///     <c>NotAnswered</c> — there is no time question on the form at all.
    ///     Nothing to say, so the field is dropped rather than published as an
    ///     empty fact.
    ///   </description></item>
    ///   <item><description>
    ///     Midnight is <b>neither</b>. It is a real answer and buckets as
    ///     morning, which is exactly why the scrub never sees a clock time it
    ///     could mistake for a default.
    ///   </description></item>
    /// </list>
    /// </remarks>
    public Reporting.TimeOfDay TimeOfDay { get; init; } = Reporting.TimeOfDay.NotAnswered;
}
