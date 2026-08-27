namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
/// The meaning downstream logic reads an answer for. A role is <b>optional
/// metadata on an ordinary question</b>, not a second kind of question: an
/// administrator can move a role to a different question, clear it, or delete
/// the question that carries it.
/// </summary>
/// <remarks>
/// <para>
/// Publication consent is the one required system question, and it is the only
/// answer read by name. Every other question is ordinary revision-bound data —
/// the admin review DTO reads exact asked questions and answers directly, so
/// nothing else needs a typed projection. See
/// <c>docs/data-and-persistence.md</c>.
/// </para>
/// </remarks>
public enum QuestionRole
{
    /// <summary>An ordinary question. Nothing reads it by name.</summary>
    None = 0,

    /// <summary>Gates publication entirely. Carried by the one system question.</summary>
    ConsentPublish = 1,
}
