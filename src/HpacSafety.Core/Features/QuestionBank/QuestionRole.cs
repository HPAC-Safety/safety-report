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
///         Publication consent and media consent are the two <b>system</b>
///         questions (ADR-0117): neither can be optional, deleted, or given up its
///         role. The reporter's and pilot's first and last names (ADR-0154) are
///         read by role the same way, but on ordinary questions — each stays
///         optional, deletable, and reassignable, and is shown only on the admin
///         report list, never publicly. Every other question is ordinary
///         revision-bound data — the admin review DTO reads exact asked questions
///         and answers directly, so nothing else needs a typed projection. See
///         <c>.spec/data-and-persistence.md</c>.
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

	/// <summary>The reporter's first name, shown on the admin report list (ADR-0154).</summary>
	ReporterFirstName = 3,

	/// <summary>The reporter's last name, shown on the admin report list (ADR-0154).</summary>
	ReporterLastName = 4,

	/// <summary>The pilot's first name, shown on the admin report list (ADR-0154).</summary>
	PilotFirstName = 5,

	/// <summary>The pilot's last name, shown on the admin report list (ADR-0154).</summary>
	PilotLastName = 6,
}
