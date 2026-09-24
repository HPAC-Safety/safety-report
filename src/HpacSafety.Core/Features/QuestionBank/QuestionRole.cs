namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     The meaning downstream logic reads an answer for. A role is
///     <b>
///         optional
///         metadata on an ordinary question
///     </b>
///     , not a second kind of question: an
///     administrator can move a role to a different question, clear it, or delete
///     the question that carries it.
/// </summary>
/// <remarks>
///     <para>
///         Publication consent and media consent are the two system questions, and
///         the only answers read by name (ADR-0117). Every other question is
///         ordinary revision-bound data —
///         the admin review DTO reads exact asked questions and answers directly, so
///         nothing else needs a typed projection. See
///         <c>docs/data-and-persistence.md</c>.
///     </para>
/// </remarks>
public enum QuestionRole
{
	/// <summary>An ordinary question. Nothing reads it by name.</summary>
	None = 0,

	/// <summary>Gates publication entirely. Carried by the publication-consent system question.</summary>
	ConsentPublish = 1,

	/// <summary>
	///     Gates whether a published report shows its photos and video. Carried by
	///     the media-consent system question (ADR-0117).
	/// </summary>
	ConsentMedia = 2,
}
