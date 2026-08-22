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
}
