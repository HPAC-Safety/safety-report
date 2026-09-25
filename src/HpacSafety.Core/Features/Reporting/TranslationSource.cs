using HpacSafety.Core;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     How an answer's second-language value was produced. Stored as an invariant
///     code, like every other domain enum. See ADR-0080, ADR-0112.
/// </summary>
public enum TranslationSource
{
	/// <summary>Produced mechanically by the Worker via <see cref="ITranslator" />.</summary>
	Auto = 0,

	/// <summary>Typed or accepted by an administrator.</summary>
	Human = 1,

	/// <summary>
	///     Copied at submission from the other-language label of the choice the
	///     reporter picked. A lookup, not a translation. See ADR-0112.
	/// </summary>
	Choice = 2,

	/// <summary>
	///     A yes or no answer's fixed counterpart in the other language, written at
	///     submission. A lookup, not a translation. See ADR-0127.
	/// </summary>
	Fixed = 3,
}
