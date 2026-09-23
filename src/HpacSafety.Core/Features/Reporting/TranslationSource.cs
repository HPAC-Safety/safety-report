using HpacSafety.Core;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     How an answer's second-language value was produced. Stored as an invariant
///     code, like every other domain enum. See ADR-0080.
/// </summary>
public enum TranslationSource
{
	/// <summary>Produced mechanically by the Worker via <see cref="ITranslator" />.</summary>
	Auto = 0,

	/// <summary>Typed or accepted by an administrator.</summary>
	Human = 1,
}
