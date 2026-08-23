using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// The meaning downstream logic reads an answer for. A role is <b>optional
/// metadata on an ordinary question</b>, not a second kind of question: an
/// administrator can move a role to a different question, clear it, or delete
/// the question that carries it.
/// </summary>
/// <remarks>
/// <para>
/// The question set is data. Escalation, publication, and reporting still have
/// to find particular answers among it — "which of these twenty questions is
/// the injury one" — and a role is how they ask without hardcoding a key.
/// </para>
/// <para>
/// Every role is optional and its absence is a defined state, never a crash:
/// with no <see cref="PilotInjury"/> question active, severity is
/// <c>NotAnswered</c> and a report takes the ordinary review path instead of the
/// escalated one. The single exception is <see cref="ConsentPublish"/>, which is
/// carried by a system question that cannot be deleted — consent is the gate on
/// publication and there is no defined behaviour without it.
/// </para>
/// </remarks>
public enum QuestionRole
{
    /// <summary>An ordinary question. Nothing reads it by name.</summary>
    None = 0,

    /// <summary>Gates publication entirely. Carried by the one system question.</summary>
    ConsentPublish = 1,

    /// <summary>The date of the occurrence — ordering, retention, published month and year.</summary>
    OccurrenceDate = 2,

    /// <summary>The province, which is publishable where a site never is.</summary>
    Province = 3,

    /// <summary>Injury to the pilot. Serious and fatal answers escalate.</summary>
    PilotInjury = 4,

    /// <summary>Injury to the passenger. Serious and fatal answers escalate.</summary>
    PassengerInjury = 5,

    /// <summary>The type of aircraft involved.</summary>
    AircraftType = 6,

    /// <summary>The aircraft's certification, from which the published class comes.</summary>
    AircraftCertification = 7,

    /// <summary>The reporter's own account. Restricted, and never translated.</summary>
    Narrative = 8,

    OccurrenceTime = 9,
}
