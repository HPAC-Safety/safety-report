using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// The certification class a report is published with. The value comes from the
/// reporter's own answer and nothing else — there is no model-to-class lookup in
/// this system and nothing infers a class from a make or model. See
/// docs/aircraft-classification.md.
/// </summary>
public enum AircraftClass
{
    /// <summary>
    /// The answer could not be normalized against the vocabulary. A valid
    /// outcome, correctable by a reviewer — never a guess.
    /// </summary>
    NotDetermined = 0,

    // Paragliders
    EnA = 1,
    LowEnB = 2,
    HighEnB = 3,
    EnC = 4,
    EnD = 5,
    Ccc = 6,
    Uncertified = 7,

    // Hang gliders — not EN-rated
    SingleSurface = 20,
    DoubleSurfaceKingposted = 21,
    Topless = 22,
    Rigid = 23,

    // Mini wings and speedwings
    MiniWing = 40,
    Speedwing = 41,

    // Tandems carry the marker with the class
    TandemParaglider = 60,
    TandemHangGlider = 61,
}
