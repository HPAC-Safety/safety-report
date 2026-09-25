namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     How one language of a <see cref="QuestionChoice" />'s wording was produced.
///     Stored as an invariant code, like every other domain enum. See ADR-0129.
/// </summary>
public enum LabelSource
{
	/// <summary>Written by a person: an Administrator, a reviewer, or the reporter who typed it.</summary>
	Human = 0,

	/// <summary>Supplied by the Worker through <see cref="ITranslator" />, off the submission path.</summary>
	Auto = 1,
}
