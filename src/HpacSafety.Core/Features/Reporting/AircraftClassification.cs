using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// What the reporter's certification answer normalized to: a class, and any
/// markers that accompany it.
/// </summary>
/// <remarks>
/// Every component comes from the reporter's own answer and the aircraft type
/// they chose. Nothing is inferred from a make, a model, a narrative, or a
/// pilot rating, and <see cref="AircraftClass.NotDetermined"/> is a valid
/// result a reviewer may correct by hand. See docs/aircraft-classification.md.
/// </remarks>
/// <param name="Class">The class the answer normalized to.</param>
/// <param name="Markers">Qualifiers carried alongside the class.</param>
public sealed record AircraftClassification(AircraftClass Class, AircraftMarker Markers = AircraftMarker.None)
{
    /// <summary>Nothing in the answer normalized to the vocabulary.</summary>
    public static AircraftClassification Undetermined { get; } =
        new(AircraftClass.NotDetermined, AircraftMarker.None);

    /// <summary>Whether a class was determined at all.</summary>
    public bool IsDetermined => Class != AircraftClass.NotDetermined;

    /// <summary>Whether the reporter said the wing was a tandem.</summary>
    public bool IsTandem => Markers.HasFlag(AircraftMarker.Tandem);

    /// <summary>
    /// The invariant codes this classification is stored and published as, in
    /// reading order — markers first, then the class. Domain values are stored
    /// as codes and localized only at the edge, so nothing here is
    /// user-facing text, and a make or model can never appear among them.
    /// </summary>
    public IReadOnlyList<string> Codes
    {
        get
        {
            var codes = new List<string>(3);

            if (IsTandem && Class is not (AircraftClass.TandemParaglider or AircraftClass.TandemHangGlider))
            {
                codes.Add(EnumCode.Of(AircraftMarker.Tandem));
            }

            if (Markers.HasFlag(AircraftMarker.MiniWing) && Class != AircraftClass.MiniWing)
            {
                codes.Add(EnumCode.Of(AircraftMarker.MiniWing));
            }

            if (Markers.HasFlag(AircraftMarker.Speedwing) && Class != AircraftClass.Speedwing)
            {
                codes.Add(EnumCode.Of(AircraftMarker.Speedwing));
            }

            codes.Add(EnumCode.Of(Class));

            return codes;
        }
    }
}
