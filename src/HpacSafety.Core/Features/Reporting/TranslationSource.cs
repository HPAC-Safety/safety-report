using HpacSafety.Core;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     How an answer's second-language value was produced. Stored as an invariant
///     code, like every other domain enum. See ADR-0080, ADR-0112, ADR-0174.
/// </summary>
public enum TranslationSource
{
	/// <summary>
	///     Produced mechanically by the Worker via <see cref="ITranslator" />. The
	///     only value anything ever writes today.
	/// </summary>
	Auto = 0,

	/// <summary>
	///     Typed or accepted by an administrator. Retired by ADR-0174: nothing
	///     writes this value anymore, but it is kept for rows already stored under
	///     it, which are never rewritten.
	/// </summary>
	Human = 1,

	/// <summary>
	///     Copied at submission from the other-language label of the choice the
	///     reporter picked. A lookup, not a translation. See ADR-0112.
	/// </summary>
	Choice = 2,
}
